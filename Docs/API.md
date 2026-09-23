# API 계약 — 팀원이 상속하는 것

**`_Game/`에서 기능을 만들 때 보는 문서다.**

> **구현 확인 전 계약 초안이다.** 아래 선언을 그대로 `.cs`에 붙여 넣으면 컴파일되지 않는다.
> 실제 `_Net/` 코드와 대조하고, 어긋나면 네트워크 담당에게 알린다.

플레이어 이동은 여기 없다 → [MOTION.md](MOTION.md) · [PLAYER.md](PLAYER.md)

## 베이스는 3개다

| 절 | 베이스 | 쓰임 | 붙는 페이즈 |
|---|---|---|---|
| 1 | `NetObjective` | 정화 퀘스트 — 붙어 있는 동안 진행도가 찬다 | P07 |
| 2 | `NetServerAI` | 포식자 · 피식자 | P06 |
| 3 | `NetAbility` | 물고기 4종 능력 | P08 |

`NetPlayerMotor`는 **폐기했다**(2026-09-23). 이동이 클라 권위라 베이스가 필요 없다.
플레이어 이동은 `_Game`의 일반 MonoBehaviour `PlayerMotor` + Mirror `NetworkTransform`이 맡는다.

`NetInteractable`은 **지금 만들지 않는다.** 용례가 "피식자 먹기" 하나뿐이라
`NetPlayer.RequestEat`로 처리한다(4절). 자원 채집이 후순위에서 돌아와 용례가 2개가 되면 그때 추출한다.
설계는 [BACKLOG.md](BACKLOG.md) 2절.

---

## 공통 실행 규약

### `OnServer*`는 "서버 전용"이지 "1회"가 아니다

상호작용은 **수락한 요청마다**, 완료는 **인스턴스의 한 생명주기당 최대 1회**다.

### 서버가 검증하는 것

호출자 · 게임 상태 · 거리 · 쿨다운 · 대상 유효성.
**클라가 넘긴 player ID를 호출자 신원으로 믿지 않는다.**
**거리 판정에는 서버가 가진 Transform만 쓴다. 요청에 좌표를 싣지 않는다.**
플레이어 좌표는 늦게 도착하므로 허용오차를 둔다 → [MOTION.md](MOTION.md) 5절

### 훅의 실행 위치

| 접두사 | 어디서 도는가 | 여기서 할 것 |
|---|---|---|
| `OnServerXxx` | **서버에서만** | 게임 상태 변경, 스폰, 오염도 감소 |
| `OnXxxFeedback` / `OnProgressChanged` / `OnStateChangedClient` | **수신 클라에서** | 연출·사운드·파티클만. **상태를 바꾸지 말 것** |
| `CanXxx` | 서버 검증 | `true`/`false`만 반환. **부수 효과 금지** |

**상태 변경은 `OnServer*` 안에서만.** 클라에서 바꾼 값은 전파되지 않고 다음 동기화에 덮어써진다.

### 그 외

- **요청 ID 중복 처리**는 연결·대상·작업 단위로 관리하고 연결 종료 시 정리한다
- `Update()`를 `sealed`로 막을 수 없다 (Unity 메시지 함수는 override가 아니다).
  베이스의 서버 실행 가드 + 훅 규약 + 코드 리뷰로 통제한다

---

## 자주 밟는 지뢰 5개

Mirror를 쓰면서 팀이 반복해서 틀리는 것들이다. **구현 전에 한 번 읽는다.**

### ① 상태와 이벤트를 구분한다

```
상태(State)    "지금 값은 7이다"      값이 바뀔 때마다 전달. 늦게 온 사람도 받는다
이벤트(Event)  "방금 폭발이 일어났다"  그때 있던 사람에게만 한 번. 지나가면 사라진다
```

| 상태 | 이벤트 |
|---|---|
| HP · 허기 · 성장 · 구역 · 진행도 · 완료 여부 | 피격 소리 · 거품 · 완료 팝업 |

**모든 것을 RPC로 보내지 않는다.** 지속되는 값은 상태로 둬야 늦게 온 사람에게 복원된다.

### ② Late Join은 현재 상태로 복원한다

**RPC는 늦은 접속자를 위한 상태 저장소가 아니다.**
지속 상태(진행도 · 완료 여부 · 연기 정지 · 완성 모델)는 **현재 상태를 읽어서** 그린다.

> ⚠️ **`SyncDictionary`의 초기 데이터는 변경 콜백만으로 오지 않는다.**
> 어댑터가 **초기 순회**해서 캐시를 만들고 `SnapshotChanged`를 올려야 한다.

일회성 소리·파티클은 **과거 것을 재생하지 않는다.** 추격 중 합류했다고 포효를 다시 틀지 않는다.

### ③ `requiresAuthority = false`는 인증 면제가 아니다

`[Command(requiresAuthority = false)]`를 붙였다고 검증이 끝난 게 아니다.

```
sender · 플레이어 객체 · 상태 · 대상 접근 · 거리 · 요청 빈도를 전부 검증한다
클라가 넘긴 player ID를 호출자 신원으로 믿지 않는다
```

월드마다 `requiresAuthority=false` Command를 흩뿌리지 않는다.
**요청은 소유 플레이어 객체를 통해 보내는 것이 기본이다.**

### ④ `NetworkTime.time`은 접속 직후 1~2초간 튄다

진행도·타이머의 **기준 시각을 그 구간에 잡지 않는다.**
값이 이상하면 표시를 보류한다.

> **"1~2초 기다리면 안정된다"는 보장 규칙이 아니다.** 매번 다시 계산한다.

**타이머는 매 프레임 남은 시간을 보내지 말고 종료 시각 하나만 동기화한다.**
클라가 `NetworkTime.time` 기준으로 빼서 표시한다.

### ⑤ 호스트에서 연출이 두 번 난다

호스트는 **서버이면서 클라**다. 서버에서 직접 VFX를 부르고 `ClientRpc`도 부르면 **두 번 재생된다.**

```
서버 게임 로직  ─┐
                 ├─ 분리한다. HP·AI·VFX를 두 번 처리하지 않는다
클라 표현 경로   ─┘
```

**표현 이벤트는 `eventId`로 중복 제거하고, 호스트는 수신 경로에서 1번만 재생한다.**

---

# 1. `NetObjective` — 정화 퀘스트

기획서의 *"막혀 있는 돌을 부셔 물길을 뚫리게 한다"* 가 이 베이스다.

```csharp
public abstract class NetObjective : NetworkBehaviour
{
    protected abstract float Duration { get; }
    protected virtual float SpeedFor(int n, float weightSum) => weightSum;
    protected virtual bool CanContribute(NetPlayer player) => true;
    protected abstract void OnServerCompleted();
    protected virtual void OnProgressChanged(float progress01) { }
    protected virtual void OnCompletedFeedback() { }

    public float Progress01      { get; }
    public int   ContributorCount { get; }
    public bool  IsCompleted     { get; }
    public void  SetContributing(bool on);
}
```

## 복붙 예제

```csharp
public class BlockedRock : NetObjective
{
    [SerializeField] float baseDuration = 30f;
    [SerializeField] float purifyAmount = 0.1f;   // 오염도 10% 감소

    protected override float Duration => baseDuration;

    protected override void OnServerCompleted()   // 서버에서, 생애당 최대 1회
    {
        NetSession.Instance.AddPurification(purifyAmount);
    }
}
```

**훅 안에 네트워크 코드가 한 줄도 없다. 그게 정상이다.**

## 변주 — 추가 작업 0

```csharp
// 혼자서는 꿈쩍도 안 하는 바위
protected override float SpeedFor(int n, float weightSum)
    => n >= 2 ? weightSum : 0f;

// 성장해야 열리는 목표
protected override bool CanContribute(NetPlayer p)
    => p.GrowthLevel >= 3;
```

## 강화는 개인 것, 공유되는 건 결과

`weightSum`은 붙어 있는 기여자 각자의 가중치를 **더한** 값이다.
각자 기본 1.0이고, **자기 성장·자기 능력으로 자기 가중치만** 올라간다.

```
A가 1.5로 강화 + B는 기본  →  weightSum = 2.5
A의 강화가 B의 기여율을 올리지 않는다. B 혼자 남으면 다시 1.0.
4명이 공유하는 것은 그 합으로 계산된 진행도다.
```

> **수확체감을 넣는다면 `n`에만 적용한다.**
> `weightSum`을 `n`으로 나누면 **강화된 사람의 버프가 인원수로 희석되어** 의도와 반대가 된다.

### `SpeedFor`가 `weightSum`을 받는 이유

옛 시그니처 `SpeedFor(int n)`은 "몇 명"만 알려주고 "누가"는 알려주지 않는다.
그래서 `강화된 A + 기본 B`와 `기본 C + 기본 D`가 둘 다 `n=2`로 **같은 속도**가 되어,
A의 강화가 진행바에 전혀 반영되지 않는다.

## ⚠️ `CanContribute`로 역할을 나눌 때의 함정

```csharp
protected override bool CanContribute(NetPlayer p)
    => p.FishId == FishId.모래무지;      // 역할은 생기지만...
```

**그 사람이 행동 불능이면 15초, 접속을 끊으면 영영 진행이 막힌다.**
재접속 복구는 범위 밖이라 복구 수단이 없다.

**능력은 "없으면 못 함"이 아니라 "있으면 빠름"으로 설계한다.**

## 서버 계약

- `Duration`은 **유한한 양수**. 잘못된 설정은 진행을 중단하고 오류를 기록한다
- 매 서버 틱에 **범위·생존·행동 가능 상태를 재검증**한다
- 중복 시작은 인원을 늘리지 않으며, 중단·이탈·연결 종료·파괴 시 제거한다
- 진행량은 `서버 경과시간 × 속도 / Duration`, 0~1로 제한. **전송 주기와 계산 주기는 별개다**
- 기여자가 없으면 **정지하고 진행도는 유지**한다 (리셋 여부는 기획 확정 항목)
- 완료는 **플래그를 먼저 확정하고 기여자를 비운 뒤** 훅 호출.
  예외가 나도 보상을 자동 재호출하지 않고 로그를 남긴다

## 클라 계약

초기 스냅샷 적용 후 **진행도·완료 상태로 화면을 1회 복원**하고 이후 변경 때 갱신한다.

```
지속 상태 (연기 정지·완성 모델·충돌)  → 진행도/완료 상태에서 복원
일회성 연출 (완료 사운드)             → 과거 것을 재생하지 않는다
```

**Mirror가 사용자 정의 훅의 초기 호출을 자동 보장한다고 가정하지 않는다.**

## 이 베이스의 범위

**플레이어가 붙어 있는 동안 차는 목표 전용이다.**
사람이 없어도 흘러가는 타이머는 억지로 끼우지 않는다 — 서버 완료 시각을 가진 별도 컴포넌트로 만든다.

---

# 2. `NetServerAI` — 포식자와 피식자

**둘 다 이 베이스를 쓴다.**

```csharp
public abstract class NetServerAI : NetworkBehaviour
{
    [SerializeField] protected float tickInterval = 0.1f;

    protected abstract void Think();
    protected virtual void OnStateChangedClient(int prev, int next) { }

    protected void MoveTo(Vector3 worldPos, float speed);
    protected void FaceTo(Vector3 worldPos, float turnSpeed);
    protected void SetState(int state);
    protected int  State { get; }

    protected NetPlayer FindNearestPlayer(float maxRange);
    protected IReadOnlyList<NetPlayer> PlayersInRange(float range);
}
```

## 규칙

- **`Think`는 서버에서만 결정한다.** 클라 AI 컴포넌트는 비활성화하고 애니메이션·보간만 실행한다
- `MoveTo`는 **이동 목표와 초당 속도를 저장**한다. 서버 `FixedUpdate`가 실제 이동한다.
  **`Think` 호출마다 한 걸음 옮기는 구현은 피한다**
- `FaceTo`의 `turnSpeed` 단위는 **도/초**
- 상태 ID는 `int` 상수 — `public const int Chase = 1;`
- **순찰 목표는 스폰 시 유효한 맵 위치로 초기화한다** (안 하면 원점으로 헤엄친다)
- 초기 클라 상태도 복원하되 **추격 중 합류했다고 포효를 다시 재생하지 않는다**
- **판정용 난수는 직접 굴리지 않는다.** 클라에서 굴리면 4명이 다른 결과를 본다.
  연출용 난수(파티클 흔들림)는 상관없다

## 프리팹 구성

```
포식자
├── NetworkIdentity
├── NetworkTransformReliable    syncDirection = ServerToClient
├── Rigidbody                   isKinematic = true, useGravity = false
└── 포식자AI : NetServerAI

피식자
├── NetworkIdentity             ← 오브젝트당 1개
├── NetworkTransformReliable    syncDirection = ServerToClient
├── Rigidbody                   isKinematic = true, useGravity = false
├── 피식자AI : NetServerAI       ← 헤엄치기
└── 먹히기 (일반 컴포넌트)        ← 소비 여부 플래그
```

- **위치는 서버가 정하고 NT가 나른다.** 클라에서는 `Think`와 이동이 돌지 않고, NT 보간 위치를 그리기만 한다
- **클라에서 Transform을 건드리지 않는다.** 다음 스냅샷에 덮인다
- **Rigidbody는 kinematic이다.** 물리와 NT가 Transform을 서로 잡아당기면 떤다 → [LEARN/02_KINEMATIC.md](LEARN/02_KINEMATIC.md)
- **플레이어와 물리 충돌하지 않는다.** 콜라이더는 트리거로 두거나 레이어로 끈다 → [MOTION.md](MOTION.md) 1.1절
- 공격 판정용 트리거 Collider는 붙여도 되지만 **판정은 서버에서만** 처리한다.
  클라의 `OnTrigger*`로 피해를 주지 않는다 — 트리거는 4대 PC 모두에서 발생한다
- 송신 주기 시작값은 [SPEC.md](SPEC.md) 4절 권위표를 따른다
- 애니메이션·포효는 `State`(int)를 받아 `OnStateChangedClient`에서 로컬로 구동한다. `NetworkAnimator`는 쓰지 않는다

## 피식자는 컴포넌트가 2개다

피식자는 **헤엄치면서 먹히기도** 한다. C#은 다중 상속이 안 되지만,
Mirror는 하나의 `NetworkIdentity` 아래 `NetworkBehaviour`를 여러 개 허용한다. 위 구성의 AI + 먹히기가 그것이다.

**포식 판정 자체는 `_Net`이 한다**(4절).

> **피식자 개체 수는 전송량 위험이다.** 무리로 배치하면 네트워크 오브젝트가 급증한다.
> 개체 수를 정한 뒤 전송량을 측정한다.

---

# 3. `NetAbility` — 물고기 능력

**능력은 서버 판정이다.** 결과가 다른 플레이어에게도 보여야 하므로 로컬 처리로 둘 수 없다.

```csharp
public abstract class NetAbility : NetworkBehaviour
{
    [SerializeField] protected float cooldown = 5f;

    protected virtual bool CanUse(NetPlayer player) => true;
    protected abstract bool OnServerUse(NetPlayer player);   // 수락 시 true
    protected virtual void OnUseFeedback(NetPlayer player) { }

    public void  RequestUse();
    public float CooldownRemaining01 { get; }
}
```

## 규칙

- 쿨다운·사용 조건·효과 적용은 **전부 서버가 판정한다.** 클라의 요청은 입력일 뿐이다
- `OnServerUse`가 `false`면 **아무 상태도 바꾸지 않고 쿨다운도 갱신하지 않는다.**
  거부 요청에도 **요청 빈도 제한은 적용한다**
- 클라가 제안한 "강화 속도"는 입력값으로 받지 않는다.
  최종 효과는 서버가 `FishTypeDefinition`으로 계산한다
- 모든 빌드의 **설정 버전을 일치**시키고 연결 시 config hash가 다르면 거부한다

## 서버 판정 능력의 조건

```
그 결과가 나 말고 다른 사람 화면에도 보여야 한다

"정화를 더 빨리 진행한다"  → 서버 판정 ✅  진행바가 4명에게 같게 보인다
"위험을 먼저 감지한다"      → 로컬 표시 ❌  _Local/에서 끝낸다
```

## 개인 것과 공유되는 것

- `GrowthLevel`은 **플레이어별 상태**이고 강화 배율도 그 플레이어에게만 적용된다
- **공유되는 것은 능력의 *결과*다.** 강화된 A가 정화를 빨리 진행하면 4명이 같은 속도로 본다
- 진행형 목표에 영향을 주는 능력은 `SpeedFor(n, weightSum)`의 **A 자신의 가중치**로만 들어간다(1절)
- 능력이 이동에 영향을 준다면(가속 등) **5절 감속 디버프와 같은 방식**이다 —
  서버가 정수 단계 SyncVar를 올리고 소유 클라 `PlayerMotor`가 읽는다

## ⚠️ 명세가 아직 없다

기획서에 능력이 적힌 것은 **갈겨니의 "위험 조기 경보"** 하나뿐이고,
**갈겨니는 플레이어 물고기 목록(붕어·잉어·모래무지·참종개)에 없다.**
게다가 그 능력은 로컬 표시 형태라 서버 판정 결정과 맞지 않는다.

**2주차 말 기획 결정 항목 중 가장 급하다.** P08 전체가 여기 걸려 있다.

---

# 4. `NetPlayer` · 포식 · 생성

```csharp
public class NetPlayer : NetworkBehaviour, IPlayerView
{
    public static NetPlayer Local { get; }
    public static NetPlayer Find(uint netId);
    public static IReadOnlyList<NetPlayer> All { get; }

    public string  Nickname    { get; }
    public int     SlotIndex   { get; }  // 0~3, 서버 할당
    public bool    IsMine      { get; }
    public int     State       { get; }
    public int     FishId      { get; }  // 스탯·능력의 키
    public float   Health01    { get; }  // 0~1. 절대값은 서버 내부
    public int     HungerStage { get; }  // 0~10 정수 단계
    public int     GrowthLevel { get; }
    public bool    IsDowned    { get; }
    public Vector3 Position    { get; }  // 표시 전용
    public NetPlayerVitals Vitals { get; }

    [Server] public void SetState(int state);   // Downed 강제 등 서버 전이

    public void ReportState(int state);         // 소유 클라만 호출. 서버가 검증 후 State에 반영
    public void RequestEat(uint targetNetId);   // 소유 클라만 호출. 좌표를 싣지 않는다

    // 서버 → 소유 클라. 소유 클라의 PlayerMotor가 구독한다
    public event Action<Vector3, Quaternion> RespawnRequested;
    public event Action<Vector3>             ImpulseReceived;
    [Server] public void Respawn(Vector3 pos, Quaternion rot);   // 내부에서 TargetRpc
    [Server] public void ApplyImpulse(Vector3 impulse);           // 내부에서 TargetRpc
}

public static class NetSpawner
{
    public static GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot);
    public static void Despawn(GameObject go);
}
```

- `Local`/`Find`는 없으면 **null**, `All`은 읽기 전용 조회
- `Spawn`/`Despawn`은 **서버에서만**. 내부에서 서버 활성 상태를 확인한다
- **로컬 UI·파티클의 `Instantiate`/`Destroy`는 허용한다**
- `[Server]` 속성은 **선언부에** 붙이고 호출은 서버 훅 안에서 한다
- `ReportState`는 **`IsDowned` 중에는 거부**한다. Downed 진입·해제는 서버만 한다
- **서버는 플레이어 Transform을 직접 쓰지 않는다.** 위치를 바꿔야 하면 `Respawn` / `ApplyImpulse`로 소유자에게 요청한다 → [MOTION.md](MOTION.md) 4절

## 포식은 원자적이어야 한다

**같은 피식자를 2명이 동시에 먹었을 때 둘 다 성장하면 안 된다.**

```
1. 검증   Alive · 대상이 피식자인가 · 아직 소비 안 됐나 · 서버 거리 · 쿨다운
2. 선점   피식자를 Consumed로 표시          ← 반영보다 먼저
3. 반영   허기 회복 + 성장. 같은 처리 구간에서 한 번만
4. 연출   살아 있는 세션 객체가 NetFx.Broadcast
5. 삭제   Despawn
```

- **2번을 3번보다 먼저** 하기 때문에 다음 요청이 실패한다
- 검증과 커밋 사이에 **`await`/`yield`를 넣지 않는다.** 그 틈에 두 번째 요청이 들어온다
- 중복 요청은 **기존 결과만 돌려주며** 성장을 다시 지급하지 않는다
- **대상이 이미 사라졌으면 정상 실패로 응답**한다. null 참조 예외로 처리하지 않는다
- 클라는 서버 성공 전에 대상을 삭제하거나 레벨을 올리지 않는다
- **포식자에게는 포식 요청을 허용하지 않는다**

> 자원 채집이 후순위에서 돌아오면 이 순서가 그대로 재사용된다.
> 그때 공통부분을 `NetInteractable`로 추출한다.

---

# 5. `NetPlayerVitals` — HP · 허기 · 성장 · 자동 부활

```csharp
public class NetPlayerVitals : NetworkBehaviour
{
    public float Health01    { get; }
    public int   HungerStage { get; }   // 0~10 정수 단계
    public int   GrowthLevel { get; }
    public bool  IsDowned    { get; }
    public int   SlowStage   { get; }   // 감속 단계. 0 = 없음. PlayerMotor가 읽는다

    [Server] public void ApplyDamage(float amount, int sourceId);
    [Server] public void ApplyDebuff(int debuffId, float duration);
    [Server] public void Feed(int stages);     // 허기 회복 (포식 시)
    [Server] public void Grow(int amount);     // 성장 (포식 시)
}
```

## 허기는 0~10 정수 단계다

| | 방식 |
|---|---|
| 서버 내부 | 연속 감소 (감소율 + 기준 시각) |
| 전송 | **정수 단계가 바뀔 때만.** 10단계라 전송량이 거의 0 |
| HUD | 클라가 로컬에서 부드럽게 트윈 |

**연속값을 동기화하지 않는다.** 실수 오차로 서버와 클라가 다른 값을 본다.

- **허기가 0이면 HP가 계속 깎인다**
- **회복 수단은 피식자 포식 하나뿐이다**

## 피격과 행동 불능

- 포식자 피해는 **서버의 현재 위치와 서버 쿨다운**을 사용한다.
  플레이어 좌표가 늦게 도착하므로 **히트박스를 줄여** 판정한다 → [MOTION.md](MOTION.md) 5절
- 같은 공격의 다중 Collider 접촉은 `attackInstanceId + targetNetId`로 중복 제거
- HP는 0~maxHP로 제한. 디버프는 **효과 ID · 강도 · 종료 서버 시각**으로 관리
- **HP 0이면 행동 불능.** 소유 클라 `PlayerMotor`가 `IsDowned`를 보고 입력을 잠그고, 서버는 상호작용을 거부한다
- **죽는 즉시 오브젝트를 파괴하지 않는다.** 논리 상태는 Downed로 두고 표현만 사망 애니메이션이다

## 자동 부활 — 15초

```
1. 서버가 부활 완료 시각을 기록      ← 시각 하나만 동기화. 남은 시간을 매 프레임 안 보낸다
2. 시각이 되면 HP 회복 + 디버프 해제 + IsDowned 해제
3. 서버 → 소유자  NetPlayer.Respawn(pos, rot)
4. 소유자가 순간이동하고 NT 텔레포트 경로로 모든 관찰자의 보간 버퍼를 비운다
```

**서버가 Transform을 직접 옮기면 소유자가 보내던 옛 좌표에 덮여 죽은 자리로 돌아간다.**
**4번이 없으면 남의 화면에서 맵을 가로질러 미끄러진다.**

→ 상세는 [MOTION.md](MOTION.md) 4절

## 성장

**피식자를 먹으면 `GrowthLevel`이 오르고 그 플레이어의 능력만 강화된다.**

- **몸집이 커지는 기획은 제외했다**(2026-09-19).
  `PlayerMotor`는 `GrowthLevel`을 읽지 않고 충돌 반경도 변하지 않는다
- 나중에 크기 변화를 넣는다면 **이동 계약 변경으로 취급하고 T16 · T18 · T19를 다시 돌린다**

## 이동에 영향을 주는 디버프

**현재 해당하는 것은 감속 디버프 하나뿐이다.**

- 서버가 `SlowStage`(정수 단계)를 올리고 내린다. **연속값을 넣지 않는다**
- 소유 클라 `PlayerMotor`가 SyncVar를 읽어 속도 배율로 바꾼다.
  RTT/2만큼 늦게 걸리고 늦게 풀리지만, 재조정이 없으니 튀지 않는다
- **순수 시각 효과 디버프(화면 흐림 등)와 구분**해서 관리한다. 후자는 `_Local/`에서 끝난다

---

# 6. `NetSession` — 전역 상태

```csharp
public class NetSession : NetworkBehaviour
{
    public static NetSession Instance { get; }
    public float Pollution01 { get; }   // 1 = 100% 오염. 목표는 0
    public int   CurrentZone { get; }   // 0 상류 / 1 중류 / 2 하류
    public int   Phase       { get; }   // Playing / Won

    [Server] public void AddPurification(float amount01);
    [Server] public void SetZone(int zone);
}
```

- **오염도는 세션 전역 단일 상태다.** 목표 진행도와 별개 값이며,
  목표 진행도를 그대로 오염도로 쓰지 않는다
- **구역은 1차 범위에서 표시 전용이다.** 판정은 서버 좌표로 하고 클라는 결과만 받는다.
  경계에서 HUD가 깜빡이지 않도록 **히스테리시스**를 둔다
- **`Phase`는 `Playing`과 `Won` 둘뿐이다.** 전원 사망 재시작은 제외됐다(2026-09-21)

---

# 7. UI 계약 — `_Local/`로 가는 통로

## 어셈블리 경계

```
_Contracts   Unity 기본 타입 + 순수 C#만        IPlayerView · NetEvents
    ↑ 참조
_Net         Mirror + _Contracts               어댑터 · 동기화 · 검증 · 베이스
_Game        _Net + _Contracts                 베이스 상속
_Local       _Contracts                        UI · 카메라 · 연출. Mirror 없음
```

**`NetEvents`를 `_Net` 안에 그대로 두면 이 경계가 완성되지 않는다.** 분리는 P07 작업 항목이다.

```csharp
public interface IPlayerView
{
    string  Nickname    { get; }
    int     SlotIndex   { get; }
    bool    IsMine      { get; }
    int     State       { get; }
    int     FishId      { get; }
    float   Health01    { get; }   // HUD 체력 바
    int     HungerStage { get; }   // HUD 허기 게이지 (0~10)
    int     GrowthLevel { get; }
    bool    IsDowned    { get; }
    Vector3 Position    { get; }   // 미니맵. 표시 전용
}

// _Contracts의 NetEvents
public static IPlayerView LocalPlayer { get; }
public static IReadOnlyList<IPlayerView> Players { get; }
public static float Pollution01  { get; }
public static int   CurrentZone  { get; }
public static int   SessionPhase { get; }

public static event Action SnapshotChanged;
public static event Action<IPlayerView> PlayerJoined;
public static event Action<IPlayerView> PlayerLeft;
public static event Action<IPlayerView, int, int> PlayerStateChanged;
public static event Action<IPlayerView> PlayerVitalsChanged;  // HP·허기·성장·행동 불능
public static event Action<float> PollutionChanged;
public static event Action<int>   ZoneChanged;
public static event Action<int>   SessionPhaseChanged;
public static event Action<string, Vector3> Fx;

public static void RaiseFx(string fxId, Vector3 worldPos);
```

## 규칙

**HUD가 요구하는 값(HP·허기·오염도·구역·미니맵)은 전부 이 계약을 통해서만 `_Local/`에 전달된다.**
`_Local/`이 `NetPlayer`나 `transform`을 직접 읽어 HUD를 그리는 경로를 만들지 않는다.

`Position`은 **표시 전용**이다. 미니맵·화면 밖 화살표에만 쓰고 **거리 판정에 쓰지 않는다.**

## `RaiseFx`와 `NetFx.Broadcast`는 다르다

```
NetEvents.RaiseFx      현재 클라 프로세스에서만 이벤트 발생. 네트워크로 안 나간다
NetFx.Broadcast        서버가 지속 세션 객체의 RPC로 방송 → 각 클라가 RaiseFx 호출
```

곧 Despawn할 객체로 방송하지 않는다. **호스트는 수신 경로에서 1번만 재생한다.**

## 구독 규칙

```csharp
void OnEnable()
{
    NetEvents.PollutionChanged += Redraw;   // 앞으로의 변경
    Redraw(NetEvents.Pollution01);          // 지금 상태  ← 빼먹으면 버그
}
void OnDisable()
{
    NetEvents.PollutionChanged -= Redraw;   // 해제 안 하면 누수
}
```

**구독만 하면 이미 들어와 있던 값을 놓친다.**
오염도가 이미 60%인데 다음 변경까지 화면은 100%다.

---

## 변경 기록

API 동결은 **목표이지 영구 불변 약속이 아니다.**
바꿔야 하면 **구현·예제·이 문서를 같은 커밋에서** 맞춘다.

### 2026-09-23 이동 클라 권위 전환

| 변경 | 내용 |
|---|---|
| 삭제 | `NetPlayerMotor` 베이스. 이동은 `_Game/PlayerMotor` + `NetworkTransformReliable` |
| 삭제 | `NetPlayer.Motor` |
| 추가 | `NetPlayer.Vitals` · `ReportState` · `Respawn` · `ApplyImpulse` · `RespawnRequested` · `ImpulseReceived` |
| 추가 | `NetPlayerVitals.SlowStage` |
| 변경 | 부활: `MotionEpoch` → 서버가 소유자에게 요청, 소유자가 순간이동 |
| 변경 | 거리 판정: 서버 시뮬레이션 좌표 → 서버가 가진 Transform + 허용오차 |

### 2026-09-21 범위 축소

| 변경 | 내용 |
|---|---|
| 삭제 | `NetInteractable`(용례 1개 → `RequestEat`), `NetPlayerInventory`, `NetSharedStorage` |
| 삭제 | 동료 구조(`Revive(rescuer)`), 전원 사망 재시작(`RestartSession`, Phase 4단계) |
| 변경 | 허기 `float Hunger01` → `int HungerStage` (0~10 정수) |
| 변경 | `Revive(rescuer)` → 15초 자동 부활 |
| 이전 | 이동 계약 전체 → [MOTION.md](MOTION.md) |
| 이전 | 삭제 항목 설계 → [BACKLOG.md](BACKLOG.md) |

**`SpeedFor(int n, float weightSum)`는 기존 계약이 깨지는 변경**이므로
파생 클래스가 생기기 전에 확정한다. 지금 `.cs`가 0개라 가장 싸다.
