# 이동 계약

**플레이어 이동을 만들고 고치는 사람이 보는 문서다.**

이동은 **클라 권위**다(2026-09-23 결정). 소유 클라가 자기 물고기를 직접 움직이고,
Mirror `NetworkTransform`이 그 결과를 서버와 다른 클라에 나른다.
남의 물고기는 NT가 받은 위치를 **보간**해서 그린다.

**HP·허기·성장·행동 불능·부활·AI·정화·능력은 여전히 서버 권위다.** 이동만 바뀌었다.

> 이전 서버 권위 계약(예측·재조정·`MoveStep`)은 [archive/2026-09-18_server-auth-motion.md](archive/2026-09-18_server-auth-motion.md)에 보관했다.
> **어떤 순서로 만드는지**는 [ROADMAP.md](ROADMAP.md) P02.

## 왜 바꿨나

```
서버 권위 + 예측·재조정   네트워크 담당 1명이 1주차를 통째로 쓴다. 가장 큰 일정 위험
클라 권위 + NT           Mirror 기본 컴포넌트. 1~2일
```

4인 협동 PvE라 좌표 조작으로 얻는 이득이 사실상 없다.
대신 생기는 비용 두 가지를 이 문서가 다룬다.

```
1. 서버가 판정에 쓰는 좌표 = 소유 클라가 보내온 좌표      → 5절 허용오차
2. 서버가 플레이어 위치를 직접 쓸 수 없다                 → 4절 소유자 경유
```

---

# 1. 플레이어 프리팹 구성

```
Player (prefab)
├── NetworkIdentity
├── NetworkTransformReliable    syncDirection = ClientToServer
├── NetPlayer                   _Net  — 닉네임·슬롯·FishId·State, 요청 Command, TargetRpc
├── NetPlayerVitals             _Net  — HP·허기·성장·IsDowned·SlowStage (서버 권위)
├── Rigidbody                   프리팹 기본값 isKinematic = true. 소유자만 Dynamic으로 전환 (1.1절)
└── PlayerMotor                 _Game — 일반 MonoBehaviour. 소유 클라에서만 동작
```

## 1.1 Rigidbody와 충돌 — 소유자만 Dynamic

**프리팹은 하나, 이동 코드도 하나다.** 소유 여부에 따라 Rigidbody 설정만 갈린다.

| | 내 물고기 (소유자) | 남의 물고기 (원격 복제본 · 서버 쪽 사본) |
|---|---|---|
| Rigidbody | **Dynamic** | **Kinematic** |
| 위치를 정하는 것 | `PlayerMotor` + 물리 (지형 충돌은 물리가 처리) | NT가 받은 위치를 씀 |
| `PlayerMotor` | 켜짐 | 꺼짐 |

- **프리팹 기본값은 kinematic이다.** 소유 권한이 확정된 순간(`OnStartAuthority`)에 소유자만 Dynamic으로 바꾼다.
  기본값이 Dynamic이면 권한 정보가 오기 전 몇 프레임 동안 원격 복제본이 물리로 움직인다
- Mirror `NetworkRigidbodyReliable`이 이 전환을 대신해 주는지 **설치한 버전에서 확인한다.** 되면 그걸 쓴다
- 원격 복제본이 kinematic인 이유는 **NT가 쓰는 Transform을 물리가 건드리지 못하게** 하는 것뿐이다. 이동 로직을 따로 만드는 게 아니다

**소유자 Dynamic 설정 — 시작값**

| 항목 | 값 | 이유 |
|---|---|---|
| `useGravity` | false | 물속. 부력은 필요하면 Motor가 계산 |
| `freezeRotation` | true | 충돌 토크로 빙글빙글 돌지 않게. 회전은 `MoveRotation`으로 직접 |
| `interpolation` | Interpolate | 카메라 떨림 방지 |
| `collisionDetection` | Continuous | 빠르게 가다 얇은 벽 관통 방지 |
| `linearDamping` | 튜닝 | 물의 저항감 |

**충돌 레이어**

| 조합 | 물리 충돌 | 이유 |
|---|---|---|
| 플레이어 ↔ 지형 | **켬** | Dynamic을 쓰는 이유 |
| 플레이어 ↔ 플레이어 | **끔** | 각자 자기 물고기만 계산해서 서로 밀어내는 게 성립하지 않는다 |
| 플레이어 ↔ 포식자 · 피식자 | **끔 (트리거만)** | 먹으러 다가가다 튕기지 않게. 포식자는 내 화면에서 과거 위치라 밀리는 위치도 어긋난다 |

플레이어와 AI의 접촉은 **트리거로 표시만** 하고, 포식·피격 판정은 서버가 한다(5절).

## `NetworkTransform` 설정 — 시작값

| 항목 | 값 | 이유 |
|---|---|---|
| 컴포넌트 | **`NetworkTransformReliable`** | 순서가 보장되어 4절 순간이동이 옛 좌표에 덮이지 않는다. 대역폭도 적다 |
| `syncDirection` | **ClientToServer** | 소유 클라가 권위 |
| 위치 · 회전 | 동기화 | |
| 스케일 | **끈다** | 몸집 변화는 기획에서 제외됐다 |
| 보간 | 위치·회전 켬 | |

> 필드 이름은 **설치한 Mirror 버전에서 확인한다.** 버전마다 이름이 조금씩 다르다.
> 손실 2%에서 원격이 끊겨 보이면 `NetworkTransformUnreliable`과 비교하고 결과를 [STATUS.md](../STATUS.md)에 남긴다.

**`NetworkAnimator`는 쓰지 않는다.** 애니메이션은 `NetPlayer.State` SyncVar를 받아 각 클라가 로컬에서 구동한다(3절).

---

# 2. 소유 클라 — `PlayerMotor`

**이동 코드는 `PlayerMotor` 한 곳에만 둔다.** HFSM 상태는 속도 배율 같은 계수만 넘긴다 → [PLAYER.md](PLAYER.md)

```csharp
// _Game — 네트워크 코드 없음. Mirror 타입을 쓰지 않는다
public class PlayerMotor : MonoBehaviour
{
    // NetPlayer.IsMine이 아니면 스스로 비활성화한다
    // FixedUpdate: 입력 → HFSM 계수 → 서버 상태(IsDowned·SlowStage) 반영 → 목표 속도 → rb.linearVelocity
}
```

## 규칙

- **싱글 게임처럼 Dynamic Rigidbody로 짠다.** 지형 충돌은 물리가 처리한다 → [LEARN/02_KINEMATIC.md](LEARN/02_KINEMATIC.md)
- 조종은 **목표 속도로 `rb.linearVelocity`를 끌어가는 방식**을 권한다. `AddForce`만으로 조종하면 미끄럽다.
  외력(넉백·유속)은 `AddForce`로 더해도 된다
- `FixedUpdate`에서 계산한다. **이동은 내 PC에서만 계산하므로 결정론이 필요 없다**
- **`OnCollisionEnter`로 게임 판정을 하지 않는다.** 포식·피격은 서버가 한다
- 서버가 정한 값은 **읽기만** 한다

| 서버 상태 | Motor가 하는 일 |
|---|---|
| `Vitals.IsDowned` | 입력을 무시한다 (속도 0으로 감속) |
| `Vitals.SlowStage` | 정수 단계를 속도 배율로 바꿔 곱한다 |

- 속도 상한 · 맵 경계는 **Motor 안에서** 처리한다. 지형 충돌은 물리에 맡긴다

## 시간

**이동 느낌에 쓰는 시간은 로컬 시간이어도 된다.** Dash 지속시간, 가속 곡선 같은 것.
결과 좌표는 NT가 나르기 때문에 4명이 같은 값을 볼 필요가 없다.

**게임 규칙에 쓰는 시간은 여전히 서버다.** 능력 쿨다운 · 부활 시각 · 디버프 종료 시각.

---

# 3. 원격 표시

**원격 복제본(남의 물고기)은 NT가 보간한 위치만 그린다.**

- 원격에서 `PlayerMotor`를 **실행하지 않는다.** 내 PC가 남의 이동을 다시 계산하면 NT와 서로 잡아당겨 떤다
- 원격 Rigidbody는 kinematic이다. NT가 쓰는 Transform을 물리가 건드리지 않게 한다(1.1절)
- 속도가 필요한 연출(꼬리 흔들기 속도)은 **보간된 위치의 변화량**으로 로컬에서 계산한다

## 상태와 애니메이션

```
소유 클라 HFSM  ──ReportState(int)──▶  서버가 검증 후 NetPlayer.State SyncVar
                                        (IsDowned 중에는 서버가 Downed로 고정)
                                              │
                            모든 클라: PlayerStateChanged 이벤트
                                              │
                            _Local: 셰이더 파라미터 · Animator 전환
```

물고기 헤엄 모션은 `FishAnimation` 셰이더 버텍스 애니메이션이라 **동기화할 게 없다.**
`NetworkAnimator`는 파라미터를 계속 보내고, 트리거는 늦게 들어온 사람에게 복원되지 않는다.
**State int로 표현이 안 되는 애니메이션이 생기면 그때 다시 결정한다.**

---

# 4. 서버가 플레이어 위치에 개입할 때

**서버는 플레이어 Transform을 직접 쓰지 않는다.**
소유자가 계속 좌표를 보내고 있어서, 서버가 쓴 값은 다음 스냅샷에 덮인다.
**서버는 소유자에게 요청하고, 소유자가 움직인다.**

| 상황 | 서버 | 소유 클라 |
|---|---|---|
| 행동 불능 | `IsDowned = true` | Motor가 입력을 잠근다 |
| 자동 부활 | `TargetRpc Respawn(pos, rot)` | 순간이동 + NT 텔레포트 경로로 전파 |
| 감속 디버프 | `SlowStage` 변경 | Motor가 배율 적용 |
| 넉백 · 후순위 낚시바늘 | `TargetRpc ApplyImpulse(vec)` | Motor가 `AddForce(vec, Impulse)` |

## 부활 순간이동

```
1. 서버가 부활 시각에 HP 회복 + IsDowned = false
2. 서버 → 소유자  TargetRpc Respawn(pos, rot)
3. 소유자가 rb.position · transform.position을 같이 옮기고 rb.linearVelocity = 0, NT의 텔레포트 경로를 호출
4. 모든 관찰자의 NT 보간 버퍼가 비워진다
```

> `interpolation = Interpolate`인 Dynamic 바디는 `rb.position`만 바꾸면 **한 스텝 동안 스르륵 이동해 보인다.** 3번처럼 둘 다 옮긴다.

**4번이 없으면 남의 화면에서 죽은 자리부터 부활 지점까지 맵을 가로질러 미끄러진다.**
Mirror NT의 텔레포트/버퍼 초기화 API는 **설치한 버전에서 확인**하고 T33으로 검증한다.

> **서버가 먼저 Transform을 옮기지 않는다.** 소유자가 보내던 옛 좌표 스냅샷이 뒤늦게 도착해 다시 덮어쓴다.
> 순서는 항상 `서버 요청 → 소유자 이동 → NT 전파`다.

## 넉백 통로를 지금 만드는 이유

`ApplyImpulse`는 지금 용례가 넉백 하나지만, **후순위 낚시바늘이 같은 통로를 쓴다** → [BACKLOG.md](BACKLOG.md) 4절.
Motor에 외력 입력구가 처음부터 있으면 바늘을 넣을 때 Motor 구조를 다시 뜯지 않는다.

---

# 5. 판정 좌표

**판정은 서버가 한다. 쓰는 좌표는 서버가 가진 플레이어 Transform이다.**
그 값은 소유 클라가 보낸 것이고, **RTT/2 + NT 보간 지연만큼 늦다.**

## 규칙

- **요청에 좌표를 실어 보내지 않는다.** `RequestEat(targetNetId)`처럼 대상만 보낸다.
  서버는 자기가 가진 Transform으로 거리를 잰다
- 플레이어가 **판정하는 쪽**(포식)은 허용오차를 **더한다** — 내 화면에선 닿았는데 안 먹히는 걸 막는다
- 플레이어가 **판정당하는 쪽**(포식자 피격)은 히트박스를 **줄인다** — 내 화면에선 피했는데 맞는 걸 줄인다
- 비정상 이동(속도 상한 초과, 순간이동)은 **거부하지 않고 로그만** 남긴다. 안티치트는 범위 밖이다

| 항목 | 시작값 |
|---|---|
| 포식 거리 허용오차 | +0.75m |
| 포식자 피격 히트박스 축소 | 80% |
| 비정상 속도 로그 기준 | 이동 상한 × 1.5 |

> 수치는 **측정 전 시작값**이다. 측정 후 조정하고 [STATUS.md](../STATUS.md)에 남긴다.

지연 보상(되감기 판정)은 범위 밖이다.

---

# 6. 호스트

호스트의 플레이어는 서버와 같은 프로세스라 **지연이 0이다.**

- 포식자 피격이 호스트에게 가장 정확하다. 시연 범위에서는 감수한다
- **원격 표시(T18)와 판정 허용오차(T19)는 호스트 화면으로 판정할 수 없다.** 반드시 원격 Client에서 본다

호스트는 서버 게임 로직과 클라 표현 경로를 **분리한다** — HP·AI·VFX를 두 번 처리하지 않는다.

---

# 7. 자주 터지는 곳

| 증상 | 원인 | 확인 |
|---|---|---|
| 4마리가 같이 움직인다 | 원격에서 `PlayerMotor`가 켜져 있다 | `IsMine` 검사 |
| 남의 물고기가 떤다 | 원격에서 Motor · 물리가 Transform을 건드린다 | 원격 Rigidbody kinematic, Motor 비활성 (1.1절) |
| 스폰 직후 남의 물고기가 잠깐 움직인다 | 프리팹 기본값이 Dynamic | 기본값 kinematic, 소유자만 전환 (1.1절) |
| 내 물고기가 벽에 부딪혀 빙글 돈다 · 벽을 뚫는다 | Dynamic 설정 누락 | `freezeRotation` · `Continuous` (1.1절) |
| 피식자에 부딪혀 튕긴다 | 플레이어 ↔ AI 물리 충돌이 켜져 있다 | 충돌 레이어 (1.1절) |
| 부활 직후 남의 화면에서 미끄러진다 | NT 보간 버퍼를 안 비웠다 | 4절 3·4번 |
| 부활했는데 죽은 자리로 돌아간다 | 서버가 Transform을 직접 옮겼다 | 4절 순서 |
| 가까이 갔는데 못 먹는다 | 서버 좌표 지연 > 허용오차 | 5절 허용오차 조정 |
| 감속이 안 걸린다 / 안 풀린다 | Motor가 `SlowStage`를 안 읽는다 | 2절 표 |
| 호스트만 잘 된다 | 원격에서 테스트를 안 했다 | 6절 |

---

# 8. 바꿔도 되는 것과 안 되는 것

**바꿔도 되는 것:** NT 송신 주기 · 보간 버퍼 배수 · 허용오차 · 히트박스 축소율 · NT 종류(Reliable/Unreliable) · Dynamic 물리 수치

**바꾸면 안 되는 것:**

```
이동은 소유 클라가 계산한다
소유자만 Dynamic, 원격 복제본은 kinematic이다
원격은 NT 보간만 한다 — Motor를 돌리지 않는다
서버는 플레이어 Transform을 직접 쓰지 않는다 — 소유자에게 요청한다
판정은 서버가 서버의 Transform으로 한다 — 요청에 좌표를 싣지 않는다
HP·허기·성장·행동 불능·부활은 서버 권위다
```

이동 계약을 바꾸면 **T16 · T18 · T19 · T33을 원격 Client에서 다시 돌린 뒤** 커밋한다.
