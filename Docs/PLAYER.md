# 플레이어 상태와 이동 — 플레이어 담당

**HFSM으로 플레이어 상태를 설계하고 `PlayerMotor`로 물고기를 움직이는 사람이 읽는 문서다.**

```
먼저    LEARN/01_CONCEPTS.md   ← 네트워크가 처음이면. "권위"가 뭔지만 알면 된다
같이    MOTION.md              ← 이동 계약 전문. 1~4절은 읽는다
```

---

## 한 줄 요약

> **HFSM과 `PlayerMotor`는 내 PC(소유 클라)에서 돈다. 상태는 계수를 반환하고, 이동은 Motor가 한다.
> 서버가 가진 값(HP·행동 불능·감속)은 읽기만 한다.**

이동은 **클라 권위**다(2026-09-23). 싱글 게임 짜듯 짜면 되고, 네트워크가 끼는 곳은 4군데뿐이다.

```
1. 내 물고기일 때만 돈다                 NetPlayer.IsMine
2. 서버 값을 읽는다                      Vitals.IsDowned · Vitals.SlowStage
3. 상태가 바뀌면 서버에 알린다            NetPlayer.ReportState(int)
4. 서버가 부르면 움직인다                 RespawnRequested · ImpulseReceived
```

## 담당 경계

| 만든다 (`_Game`) | 안 만든다 |
|---|---|
| 상태 목록과 계층 구조 | `NetworkTransform` 설정 (네트워크 담당) |
| 전이 조건 (언제 Swim→Dash인가) | `ReportState` · TargetRpc 배선 (네트워크 담당) |
| 상태별 이동 **계수** (`SpeedMul` 등) | HP·허기·감속 수치 관리 (`NetPlayerVitals`) |
| **`PlayerMotor` — 좌표 계산 · 충돌 · 속도 상한** | 판정 (포식 거리 · 피격) — 서버 |
| 로컬 입력 · 카메라 | — |
| 애니메이션 연출 (State int → 표현) | `NetworkAnimator` — 쓰지 않는다 |

---

# 1. HFSM은 소유 클라에서 돈다 — 단, 서버 값에 걸린 전이는 서버가 정한다

Idle · Swim · Dash 같은 **이동 상태는 내 PC가 정한다.** 이동이 클라 권위이기 때문이다.

그런데 **HP·행동 불능은 서버 권위다.** 그 값으로 정하는 전이를 클라가 하면 어긋난다.

```
Downed 전이를 클라가 "HP가 0이 되면"으로 판단하면

  A의 화면:  HP 0 → Downed 진입 → 조작 잠김
  서버:      아직 HP 5 (마지막 피격이 아직 반영 전)

  → A는 못 움직이는데 서버는 살아 있다고 본다
```

그래서 경계를 이렇게 긋는다.

| 상태 | 누가 정하나 |
|---|---|
| Idle · Swim · Dash · Eat(모션) | **소유 클라 HFSM** → `ReportState`로 서버에 알림 |
| **Downed** | **서버.** `Vitals.IsDowned`가 켜지면 HFSM이 **강제로** Downed로 간다. 꺼질 때까지 못 나간다 |

## 전체 흐름

```
소유 클라   입력 → HFSM.Tick(ctx) → 상태 결정 → PlayerMotor가 이동
              │
              └─ 상태가 바뀌면  NetPlayer.ReportState(새상태)
                                  ↓
서버                             검증 (IsDowned면 거부) → NetPlayer.State SyncVar
                                  ↓
모든 클라                        PlayerStateChanged 이벤트
                                  ↓
_Local                           애니메이션 · 셰이더 · 사운드
```

> AI 담당의 행동 트리는 **여전히 서버에서 돈다.** 포식자·피식자는 서버 권위다 → [API.md](API.md) 2절

---

# 2. 이동 로직은 어디로 가나

## 지금 짤 법한 코드

```csharp
class DashState : State
{
    public void Tick()
    {
        owner.transform.position += input.moveDir * baseSpeed * 2.5f * Time.deltaTime;
    }
}

class SwimState : State
{
    public void Tick()
    {
        owner.transform.position += input.moveDir * baseSpeed * 1.0f * Time.deltaTime;
    }
}
```

두 상태의 이동 코드가 **똑같고 숫자만 다르다.**
게다가 충돌·속도 상한·감속 디버프·행동 불능 잠금을 **상태마다 따로** 넣어야 한다.

## 바꾼 코드

```csharp
class DashState : State
{
    const float Duration = 0.5f;

    public MoveConfig Config => new MoveConfig { SpeedMul = 2.5f };   // 이동 코드 없음

    public int Tick(in FishContext ctx)                               // 전이 판단만
    {
        if (ctx.TimeInState >= Duration) return PlayerStateId.Swim;
        return PlayerStateId.Dash;
    }
}
```

```csharp
// _Game/PlayerMotor.cs — 좌표는 여기서만 바뀐다. 소유자만 Dynamic Rigidbody
void FixedUpdate()
{
    var cfg = hfsm.CurrentConfig;
    var dir = vitals.IsDowned ? Vector3.zero : input.MoveDir;       // 서버 값: 행동 불능
    float mul = cfg.SpeedMul * SlowMul(vitals.SlowStage);           // 서버 값: 감속

    var target = dir * baseSpeed * mul;
    rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, target, accel * Time.fixedDeltaTime);
    // 속도 상한 · 맵 경계. 지형 충돌은 물리가 처리한다
    rb.MoveRotation(...);                                           // 회전은 직접 (freezeRotation)
}

void OnImpulse(Vector3 v) => rb.AddForce(v, ForceMode.Impulse);   // 서버 요청: 넉백
```

**싱글 게임에서 Dynamic으로 짜던 방식 그대로다.** Rigidbody 설정값은 [MOTION.md](MOTION.md) 1.1절.

**서버 값을 반영하는 곳이 Motor 한 곳이라** 상태를 10개로 늘려도 Downed·감속을 빠뜨릴 일이 없다.

---

# 3. `MoveConfig` — 이동 공식의 손잡이

`_Game` 안의 로컬 구조체다. **네트워크로 보내지 않는다.**

```csharp
public struct MoveConfig
{
    public float SpeedMul;      // 속도 배율      Dash면 2.5, 평소 1.0
    public bool  InputLocked;   // 입력 무시      경직 등 로컬 상태
}
```

| 하고 싶은 것 | 표현 |
|---|---|
| 빨라진다 (Dash) | `SpeedMul` |
| 입력이 안 먹는다 (경직) | `InputLocked` |
| 느려진다 (감속 디버프) | **서버 값** `Vitals.SlowStage` — Motor가 곱한다 |
| 행동 불능 | **서버 값** `Vitals.IsDowned` — Motor가 잠근다 |
| 밀려난다 (넉백 · 후순위 낚시바늘) | **서버 요청** `ImpulseReceived` — Motor가 더한다 |

필드는 필요하면 자유롭게 추가한다. 로컬 구조체라 네트워크 계약이 아니다.

---

# 4. `FishContext` — 전이 판단에 필요한 값 묶음

```csharp
public struct FishContext
{
    public FishInput Input;          // 이번 프레임 로컬 입력 (moveDir, 버튼)
    public float     Health01;       // 서버 값. 읽기만
    public int       HungerStage;    // 서버 값. 읽기만
    public bool      IsDowned;       // 서버 값. true면 Downed 강제
    public float     TimeInState;    // 이 상태에 머문 시간(초)
}
```

`FishContext`로 받으면 **HFSM이 Unity를 모르는 순수 C#**이 된다.
테스트하기 쉽고, `MonoBehaviour` 참조가 상태 안으로 새어 들어가지 않는다.

**채우는 건 `PlayerMotor`(또는 옆의 컨트롤러)다.** 로컬 입력과 `NetPlayer.Vitals`에서 읽어 매 `FixedUpdate` 채운다.

---

# 5. 소유 클라가 한 `FixedUpdate`에 하는 일

```
1. 로컬 입력을 읽는다
2. FishContext를 채운다 (입력 + Vitals)
3. HFSM.Tick(ctx)          ← IsDowned면 Downed 강제
4. 상태가 바뀌었으면 NetPlayer.ReportState
5. PlayerMotor가 이동      ← 좌표는 여기서만 바뀐다
```

**원격 복제본(남의 물고기)에서는 1~5가 전부 돌지 않는다.** NT가 위치를, `State` SyncVar가 상태를 준다.

---

# 6. 시간

**이동 느낌에 쓰는 시간은 로컬 초여도 된다.** Dash 지속시간, 가속 곡선, 경직 시간.
결과 좌표는 NT가 나르므로 4명이 같은 시계를 볼 필요가 없다.

**게임 규칙에 쓰는 시간은 서버다.** 능력 쿨다운 · 디버프 지속 · 부활 시각.
Dash가 **능력**(쿨다운이 있고 남에게 영향)이 되면 `NetAbility`로 간다 → [API.md](API.md) 3절

> `Time.time`으로 **게임 규칙**을 재지 않는다는 전역 규칙은 그대로다.

---

# 7. 서버가 정하는 효과는 RTT/2만큼 늦다

감속 디버프, 능력 가속처럼 **서버가 SyncVar로 주는 값**은 늦게 걸리고 늦게 풀린다.
재조정이 없으니 튀지는 않는다. 100ms에서는 거의 티가 안 난다.

**표현은 먼저, 효과는 서버 확정 후.**

```
버튼 누름 · 애니메이션 · 이펙트 · 사운드   →  즉시 로컬에서
서버가 판정하는 효과 (능력 가속 등)        →  SyncVar 도착 후 Motor가 반영
```

---

# 8. 옵저버 패턴

**`NetEvents`가 이미 옵저버다.** 별도 옵저버를 만들면 계약을 우회하는 경로가 생긴다.

| 용도 | 판정 |
|---|---|
| `_Local` 내부 연출 동기화 (애니메이션 ↔ 사운드 ↔ VFX) | **자체 옵저버 OK** |
| 게임 상태를 나르기 (HP·상태·오염도) | **`NetEvents`로 통일** |

```csharp
void OnEnable()
{
    NetEvents.PlayerStateChanged += OnStateChanged;
    OnStateChanged(NetEvents.LocalPlayer, 0, NetEvents.LocalPlayer.State);  // 현재 값도 읽는다
}
```

## 애니메이션

**`NetworkAnimator`는 쓰지 않는다.** `State` int를 받아 `_Local`에서 표현을 바꾼다.
헤엄 모션은 `FishAnimation` 셰이더라 동기화할 게 없고, 꼬리 속도는 보간된 위치 변화량으로 로컬에서 구한다.
State int로 안 되는 애니메이션이 생기면 그때 다시 결정한다 → [MOTION.md](MOTION.md) 3절

---

# 9. 지금 할 일 — 네트워크 몰라도 된다

**싱글 씬에서 전부 만들 수 있다.** P02에서 그대로 붙는다.

## ① HFSM을 순수 C#으로

```csharp
public class FishStateMachine          // MonoBehaviour를 상속하지 않는다
{
    public int        Tick(in FishContext ctx);
    public MoveConfig CurrentConfig { get; }
}
```

## ② 상태 목록을 int 상수로

```csharp
public static class PlayerStateId
{
    public const int Idle   = 0;
    public const int Swim   = 1;
    public const int Dash   = 2;
    public const int Eat    = 3;
    public const int Downed = 4;
}
```

동기화는 int로만 한다. enum을 쓰면 int 변환을 명시한다.

## ③ `PlayerMotor`를 싱글에서

싱글 게임처럼 Dynamic Rigidbody로 만든다 → [MOTION.md](MOTION.md) 1.1 · 2절
서버 값(`IsDowned` · `SlowStage`)과 외력은 **가짜 값을 넣는 자리**만 만들어 둔다. P02에서 `NetPlayer`에 연결된다.

## ④ 입력 · 카메라

로컬 입력을 `FishInput` 구조체로 모아 `FishContext`에 넣는다. 네트워크로 보내지 않는다.

---

# 10. 하면 안 되는 것

| 하지 말 것 | 대신 |
|---|---|
| 상태 안에서 `transform` 옮기기 | `MoveConfig`로 계수만 반환. 이동은 Motor |
| `OnCollisionEnter`로 피격·포식 판정 | 서버 판정. 클라 충돌은 표현만 |
| 원격 복제본을 Dynamic으로 두기 | 프리팹 기본 kinematic, 소유자만 Dynamic |
| 원격 복제본에서 Motor · HFSM 돌리기 | `IsMine`이 아니면 비활성화 |
| HFSM이 HP로 Downed 판단 | `IsDowned`(서버 값)로 강제 |
| HP·허기·감속을 로컬에서 바꾸기 | 서버가 준 값을 읽기만 |
| 서버 판정이 필요한 효과(포식 성공 등)를 로컬에서 확정 | 요청하고 서버 결과를 기다린다 |
| 상태 전이에 난수로 **게임 결과** 정하기 | 서버 경로로. 연출용 난수는 괜찮다 |
| `MonoBehaviour`에 상태 전이 박기 | 순수 C# 클래스로 분리 |
| 게임 상태용 자체 옵저버 만들기 | `NetEvents` 구독 |

---

# 11. 코드가 어디 들어가나

**`_Game`에 둔다**(2026-09-23 확정). HFSM과 Motor가 소유 클라에서 돌기 때문에 `_Net`이 tick할 필요가 없다.
새 베이스 클래스는 만들지 않는다.

```
_Game   FishStateMachine · 상태들 · PlayerMotor · FishInput     ← 플레이어 담당
          │ 참조
_Net    NetPlayer (ReportState · 이벤트 · Vitals) · NetworkTransform   ← 네트워크 담당
```

`PlayerMotor`는 일반 `MonoBehaviour`다. **Mirror 타입을 직접 쓰지 않고 `NetPlayer`의 공개 멤버만 쓴다.**

---

# 12. P02에서 합의할 것

```
1. 상태 목록 확정 (int 상수)
2. 서버가 강제하는 상태(Downed)와 소유 클라가 정하는 상태의 경계
3. PlayerMotor가 읽는 서버 값 (IsDowned · SlowStage) 과 외력 입력구 (ImpulseReceived)
4. 부활 순간이동 처리 (RespawnRequested → Motor 속도 초기화)
5. 애니메이션: State int → _Local 구동
```

---

# 체크리스트

- [ ] HFSM이 `MonoBehaviour`를 상속하지 않는다
- [ ] 상태 안에 `transform` · velocity 계산이 없다 — 이동은 Motor에만
- [ ] Motor · HFSM이 `IsMine`일 때만 돈다
- [ ] Downed는 `IsDowned`로만 들어가고 나온다
- [ ] 감속·행동 불능·외력을 Motor 한 곳에서 반영한다
- [ ] 상태 ID가 `int` 상수다
- [ ] 게임 상태를 `NetEvents`로 받는다

---

## 막히면

**"이거 서버가 정해야 하나요?" 싶으면 먼저 물어볼 것.**
판단 기준은 [SPEC.md](SPEC.md) 4절 권위 분배표.

**남의 화면에서** 이동이 끊기거나 미끄러지면 직접 보정하려 들지 말고 네트워크 담당에게 넘긴다.
NT 설정 문제일 가능성이 높다.
