using UnityEngine;

// "이번 프레임에 이만큼 움직여라"가 아니라 "목표가 여기다"를 알리는 인터페이스다.
// BT는 5~10Hz로 돌고 이동은 매 프레임 부드러워야 하므로, 보간은 구현체의 Update()가 맡는다.
// 나중에 물리 기반이나 네트워크 이동으로 갈아끼울 때 바뀌는 건 구현체뿐이다.
public interface IMovement
{
    bool HasReachedDestination { get; }

    void SetDestination(Vector3 destination);
}
