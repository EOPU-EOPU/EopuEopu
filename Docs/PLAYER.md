# 플레이어 상태와 이동 — 플레이어 담당

**HFSM으로 플레이어 상태를 설계하는 사람이 읽는 문서다.**

```
먼저    LEARN/01_CONCEPTS.md   ← 네트워크가 처음이면. "권위"가 뭔지만 알면 된다
같이    MOTION.md              ← 안 읽어도 되지만, 궁금하면 이동 계약 전문
```

---

## 한 줄 요약

> **상태는 값을 반환하고, 이동은 `MoveStep`이 한다. 시간은 초가 아니라 틱으로 센다.**

이 두 가지만 지키면 **HFSM 설계는 평소대로 하면 된다.**
계층 구조도, 전이 조건도, 상태 개수도 자유다.

## 담당 경계

| 만든다 | 안 만든다 |
|---|---|
| 상태 목록과 계층 구조 | 좌표 계산 (`MoveStep` — 네트워크 담당) |
| 전이 조건 (언제 Swim→Dash인가) | 상태 동기화 (`SetState` 배선 — 네트워크 담당) |
| 상태별 이동 **계수** (`SpeedMul` 등) | HP·허기 수치 관리 (`NetPlayerVitals`) |
| 로컬 입력·카메라 | 입력 전송 (Command — 네트워크 담당) |
| 애니메이션 전이 (상태 int → 애니메이터) | — |

---

# 1. HFSM은 서버에서 돈다

**이게 이 문서에서 제일 중요한 한 가지다.**

상태 전이가 **게임 로직**이면(단순 애니메이션 전환이 아니면) 서버가 결정해야 한다.

## 왜 — 한 가지 예

`Downed` 상태의 전이 조건이 "HP가 0이 되면"이라고 하자.

```
클라에서 HFSM을 돌리면

  A의 화면:  HP 0 → Downed 진입 → 조작 잠김
  서버:      아직 HP 5 (마지막 피격 패킷이 아직 도착 안 함)

  → A는 못 움직이는데 서버는 움직일 수 있다고 본다
  → 다른 3명 화면에서 A는 멀쩡히 헤엄치고 있다
```

**HP를 서버가 갖고 있는데, 그 HP로 상태를 정하는 기계가 클라에 있으면** 항상 어긋난다.

> AI 담당의 **행동 트리도 정확히 같은 이유로 서버에서 돈다.**
> 플레이어 HFSM과 AI 행동 트리는 같은 구조다 — 둘 다 서버가 tick하고 결과만 동기화한다.

## 전체 흐름

```
클라        입력을 보낸다 (좌표가 아니라 입력)
              ↓
서버        HFSM.Tick(ctx)  →  상태 결정
              ↓
            NetPlayer.State = (int)새상태     동기화
              ↓
모든 클라   PlayerStateChanged 이벤트 수신
              ↓
_Local      애니메이터가 int를 받아 표현 전이
```

---

# 2. 이동 로직은 어디로 가나

## 지금 짤 법한 코드

```csharp
class DashState : State
{
    public void Tick()
    {
        var input = owner.input;
        owner.transform.position += input.moveDir * baseSpeed * 2.5f * Time.deltaTime;
    }                                                          //  ↑ 대시니까 2.5배
}

class SwimState : State
{
    public void Tick()
    {
        var input = owner.input;
        owner.transform.position += input.moveDir * baseSpeed * 1.0f * Time.deltaTime;
    }                                                          //  ↑ 평소니까 1배
}
```

여기서 **냄새가 하나 난다.** 두 상태의 이동 코드가 **똑같고 숫자만 다르다.**

## 바꾼 코드

```csharp
class DashState : State
{
    const int DurationTicks = 15;              // 0.5초 = 15틱 (서버 30Hz)

    // ① "나는 2.5배 빠르다" — 이동 코드 없음
    public MoveConfig Config => new MoveConfig { SpeedMul = 2.5f };

    // ② 전이 판단만 한다. 인풋 봐도 된다
    public int Tick(in FishContext ctx)
    {
        if (ctx.TicksInState >= DurationTicks) return PlayerStateId.Swim;
        return PlayerStateId.Dash;
    }
}

class SwimState : State
{
    public MoveConfig Config => new MoveConfig { SpeedMul = 1.0f };

    public int Tick(in FishContext ctx)
    {
        if (ctx.Input.DashPressed)                 return PlayerStateId.Dash;
        if (ctx.Input.MoveDir == Vector3.zero)     return PlayerStateId.Idle;
        return PlayerStateId.Swim;
    }
}
```

**`Tick`에서 인풋을 보는 건 그대로다.** 다만 **전이를 판단하는 데만** 쓰고 좌표는 건드리지 않는다.

## 이동 코드는 한 곳으로 모인다

```csharp
// _Net/MoveStep.cs — 네트워크 담당이 만든다
public static MoveState Step(MoveState s, MoveInput input, MoveConfig cfg, float dt)
{
    if (cfg.InputLocked) input.MoveDir = Vector3.zero;          // Downed면 입력 무시

    var velocity = input.MoveDir * baseSpeed * cfg.SpeedMul;    // ← 여기서 배율 적용
    s.Position += velocity * dt;

    // 속도 상한 · 맵 경계 · 지형 충돌도 전부 여기서
    return s;
}
```

두 상태에 중복돼 있던 이동 코드가 **하나로 합쳐졌다.** 상태는 `SpeedMul` 값만 다르게 준다.

## 왜 이렇게 하나

`MoveStep`은 **서버와 내 PC에서 똑같이 돌아야 한다.** 그래야 조작이 즉시 반응한다(예측).

```
이동 코드가 State 5개에 흩어져 있으면
   → 5개 전부가 서버·클라에서 똑같이 돌아야 한다
   → 하나라도 어긋나면 캐릭터가 계속 튄다

이동 코드가 MoveStep 하나면
   → 그 하나만 맞추면 된다
```

**상태를 10개로 늘려도 `MoveStep`은 그대로다.** `SpeedMul` 값만 늘어난다.

---

# 3. `MoveConfig` — 이동 공식의 손잡이

이동 코드가 아니라 **숫자 묶음**이다. "지금 이동이 어떻게 동작하는가"를 적은 것.

```csharp
public struct MoveConfig
{
    public float SpeedMul;      // 속도 배율      Dash면 2.5, 평소 1.0
    public bool  InputLocked;   // 입력 무시      Downed면 true
    public uint  Revision;      // 버전 (옛 값 판별용. 담당자는 신경 안 써도 된다)
}
```

## 상태가 이동에 주는 영향은 3종류다

| 하고 싶은 것 | 표현 |
|---|---|
| 빨라진다 / 느려진다 (Dash, 감속 디버프) | `SpeedMul` |
| 입력이 안 먹는다 (Downed, 경직) | `InputLocked` |
| 내 의지와 무관하게 끌려간다 (후순위: 낚시바늘) | `ExternalPull` — 그때 추가 |

**이 셋으로 표현이 안 되는 상태가 나오면 필드를 추가하면 된다.**
어떤 필드가 필요한지는 **P02에서 네트워크 담당과 같이 확정**한다.

> **"Dash 방향을 진입 시점에 고정하고 싶다"** 같은 것도 이 방식으로 된다.
> 서버가 진입할 때 방향을 저장해두고 `MoveConfig`에 실어 보낸다.

---

# 4. `FishContext` — 전이 판단에 필요한 값 묶음

Unity나 Mirror 것이 아니라 **우리가 만들 구조체**다. 이름은 바꿔도 된다.

```csharp
public struct FishContext
{
    public MoveInput Input;          // 이번 틱 입력 (moveDir, 버튼)
    public float     Health01;       // 0~1
    public int       HungerStage;    // 0~10 정수
    public int       TicksInState;   // 이 상태에 머문 틱 수
    public bool      IsDowned;
}
```

## 왜 묶어서 넘기나

보통은 이렇게 짠다.

```csharp
class DashState : State
{
    PlayerController owner;                  // ← MonoBehaviour 참조

    public void Tick()
    {
        if (owner.hp <= 0) machine.Change(DownedState);
    }
}
```

**문제는 `owner`가 `MonoBehaviour`라는 것이다.**

```
서버에서 이 HFSM을 돌리려면?      → MonoBehaviour가 씬에 있어야 한다
전이 규칙을 테스트하려면?          → Unity를 띄워야 한다
나중에 위치를 옮기려면?            → 전부 뜯어야 한다
```

`FishContext`로 받으면 **HFSM이 Unity를 전혀 모르는 순수 C#**이 된다.

```csharp
class DashState : State
{
    // owner 참조 없음. 필요한 건 전부 ctx로 들어온다
    public int Tick(in FishContext ctx)
    {
        if (ctx.Health01 <= 0)       return PlayerStateId.Downed;
        if (ctx.TicksInState >= 15)  return PlayerStateId.Swim;
        return PlayerStateId.Dash;
    }
}
```

## 누가 채우나

**서버가 매 틱 채워서 넘긴다.** 담당자는 받아쓰기만 하면 된다.

```csharp
// _Net 쪽 — 네트워크 담당이 배선한다
var ctx = new FishContext {
    Input        = 이번_틱_입력,
    Health01     = vitals.Health01,
    HungerStage  = vitals.HungerStage,
    TicksInState = 상태에_머문_틱,
    IsDowned     = vitals.IsDowned,
};

int nextState = hfsm.Tick(in ctx);        // ← 플레이어 담당이 만든 것
player.SetState(nextState);
```

> `in`은 구조체를 복사 없이 넘기고 안에서 수정을 막는 한정자다.
> 전이 판단은 읽기만 하므로 `in`이 맞다.

---

# 5. 서버가 한 틱에 하는 일

```
1. 입력 큐에서 이번 틱 입력을 꺼낸다
      ↓
2. HFSM.Tick(ctx)                       ← 상태 결정 (Dash 유지? Swim 전환?)
      ↓
3. 현재 상태에서 MoveConfig를 얻는다      ← SpeedMul = 2.5
      ↓
4. MoveStep.Step(state, input, cfg, dt)  ← 여기서만 좌표가 바뀐다
      ↓
5. 스냅샷 전송: 위치 + 상태(int) + MoveConfig
```

**2번과 4번이 분리되어 있는 게 핵심이다.**

---

# 6. 시간은 틱으로 센다

서버는 고정 스텝(**30Hz**)으로 돈다.

```
0.5초  →  15틱
1초    →  30틱
3초    →  90틱
```

`Time.deltaTime`은 PC마다 프레임마다 다르지만 **틱은 서버·클라가 같은 값**이다.
**쿨다운 · 지속시간 · 경직 · 무적시간 전부 틱으로 센다.**

```csharp
const int DashDurationTicks = 15;     // ✅
const float DashDuration = 0.5f;      // ❌ 초 단위로 세면 PC마다 달라진다
```

> **로컬 연출(페이드·트윈·게이지 보간)에 `Time.deltaTime`을 쓰는 건 괜찮다.**
> 금지 대상은 **게임 진행**을 로컬 시계로 재는 것이다.

---

# 7. "그럼 내 Dash가 늦게 반응하지 않나?"

맞다. `MoveConfig`는 서버 스냅샷으로 오니 **RTT만큼 늦는다.**

**답은 표현과 효과를 나누는 것이다.**

```
버튼 누름 · 애니메이션 · 이펙트 · 사운드   →  즉시 로컬에서 재생   ✅
실제 속도 변화 · 위치 이동                 →  서버 확정 후         ✅
```

체감상 **"누르자마자 대시 모션이 나가고 몸이 곧바로 따라붙는"** 그림이 된다.
100ms에서는 거의 티가 안 난다.

## 더 정확하게 하려면

소유 클라도 **같은 HFSM을 돌려서 예측**할 수 있다. 즉발감이 완벽해진다.

**다만 그러면 HFSM 전체가 결정론·재조정 대상이 된다.**

```
HFSM이 난수·Time.time을 쓰면 안 되고
재조정할 때 HFSM 상태도 같이 되감아야 하고
서버와 클라의 HFSM이 한 줄이라도 다르면 매 틱 재조정이 터진다
```

**6주에는 권하지 않는다.** 먼저 위 방식으로 만들고, P09에서 측정해보고 정말 안 되면 그때 올린다.

---

# 8. 옵저버 패턴

**`NetEvents`가 이미 옵저버다.** 별도 옵저버를 만들면 계약을 우회하는 경로가 생긴다.

| 용도 | 판정 |
|---|---|
| `_Local` 내부 연출 동기화 (애니메이션 ↔ 사운드 ↔ VFX) | **자체 옵저버 OK** |
| 게임 상태를 나르기 (HP·상태·오염도) | **`NetEvents`로 통일** |

```csharp
// 게임 상태는 이렇게 받는다
void OnEnable()
{
    NetEvents.PlayerStateChanged += OnStateChanged;
    OnStateChanged(NetEvents.LocalPlayer, 0, NetEvents.LocalPlayer.State);  // 현재 값도 읽는다
}
```

---

# 9. 지금 할 일 — 네트워크 몰라도 된다

**진행을 멈출 필요 없다.** 오히려 지금 해두면 나중에 합치기 쉬운 게 있다.

## ① HFSM을 순수 C#으로

```csharp
// MonoBehaviour를 상속하지 않는다
public class FishStateMachine
{
    public int Tick(in FishContext ctx);
}
```

`MonoBehaviour.Update()` 안에 상태 전이를 박아두면 나중에 전부 뜯어야 한다.
**이건 네트워크 지식 없이도 할 수 있고, 어차피 좋은 설계다.**

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

## ③ 전이 조건 설계는 그대로

"언제 Idle→Swim인가", "Downed에서 나가는 조건은" 같은 건 **네트워크와 무관한 게임 설계**다.
계속 하면 된다.

## ④ 입력·카메라는 목업으로

**입력 프레임 계약은 P02에 나온다.** 그 전에는 로컬 목업으로 감각만 잡고,
**목업을 그대로 인계하지 않는다.**

---

# 10. 하면 안 되는 것

| 하지 말 것 | 대신 |
|---|---|
| 상태 안에서 `transform` 옮기기 | `MoveConfig`로 계수만 반환 |
| 상태 안에서 velocity 계산 | 〃 |
| `Time.deltaTime`으로 지속시간 재기 | 틱으로 세기 (`TicksInState`) |
| `Time.time`으로 쿨다운 재기 | 〃 |
| 상태 전이에 난수 쓰기 | 필요하면 서버 전용 경로로. 클라가 굴리면 4명이 다름 |
| HP·허기를 로컬에서 깎기 | 서버가 준 값을 `ctx`로 받아 읽기만 |
| `MonoBehaviour`에 상태 전이 박기 | 순수 C# 클래스로 분리 |
| 게임 상태용 자체 옵저버 만들기 | `NetEvents` 구독 |

---

# 11. 코드가 어디 들어가나 — **P02 확정 항목**

⚠️ **아직 안 정해졌다.** 의존 방향 때문에 결정이 필요하다.

```
현재 어셈블리 의존:   _Game  →  _Net       (_Net은 _Game을 참조할 수 없다)

그런데 HFSM은:
  - 게임 규칙이다        → _Game이 자연스럽다
  - 서버가 tick해야 한다  → _Net이 호출해야 한다
```

**후보 2가지:**

| | 방식 | 장단 |
|---|---|---|
| **A** | `_Contracts`에 인터페이스, `_Game`에 구현, `_Net`이 인터페이스로 호출 | 경계가 깨끗. 배선 코드 필요 |
| **B** | `_Net`에 베이스(`NetPlayerBrain` 등)를 두고 `_Game`이 상속 | 기존 베이스 4종과 같은 패턴. 베이스가 5개가 된다 |

**B가 기존 구조와 일관되지만**, `CLAUDE.md`에 *"새 추상화는 용례 2개 이상일 때만"* 이라는 규칙이 있어
베이스를 하나 더 만드는 게 정당한지 판단이 필요하다.

**P02에서 네트워크 담당과 확정한다.** 그 전까지는 **순수 C# HFSM만 만들어두면** 어느 쪽이든 붙는다.

---

# 12. P02에서 합의할 것

```
1. 상태 목록 확정 (int 상수)
2. 각 상태가 이동에 주는 영향 → MoveConfig 필드로 표현 가능한가
3. FishContext에 뭐가 들어가나
4. 계층(Hierarchical) 구조를 서버에서 어떻게 tick할지
5. 코드 위치 (위 11절 A/B)
6. 입력 프레임(MoveInput) 필드 확정  ← 입력·카메라 작업이 여기 걸려 있다
```

---

# 체크리스트

작업하면서 스스로 확인할 것.

- [ ] HFSM이 `MonoBehaviour`를 상속하지 않는다
- [ ] 상태 안에 `transform` · velocity 계산이 없다
- [ ] 지속시간·쿨다운을 **틱**으로 센다
- [ ] 상태 ID가 `int` 상수다
- [ ] 전이 판단에 필요한 값이 전부 `FishContext`로 들어온다
- [ ] 상태 전이에 난수가 없다
- [ ] 게임 상태를 `NetEvents`로 받는다

---

## 막히면

**"이거 서버가 정해야 하나요?" 싶으면 먼저 물어볼 것.**
판단 기준은 [SPEC.md](SPEC.md) 4절 권위 분배표.

이동이 **튀거나 되돌아가 보이면** 직접 보정하려 들지 말고 네트워크 담당에게 넘긴다.
여기서 덧칠하면 원인이 가려진다.
