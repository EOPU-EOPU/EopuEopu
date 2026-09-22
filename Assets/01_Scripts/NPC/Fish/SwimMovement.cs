using UnityEngine;

// NavMesh를 쓰지 않는 커스텀 3D 유영이다. NavMesh는 평면 표면 기반이라 수심을 표현하지 못한다.
// SERVER ONLY — 서버에서만 활성화한다. 클라이언트에서는 꺼두고 서버가 보낸 위치를 보간해 표시만 한다.
[RequireComponent(typeof(Rigidbody))]
public class SwimMovement : MonoBehaviour, IMovement
{
    // 기획이 미확정이라 전부 임의의 placeholder 값이다.
    [SerializeField] private float maxSpeed = 3f;
    [SerializeField] private float turnSpeed = 120f;
    [SerializeField] private float arriveDistance = 0.5f;

    private Rigidbody rb;
    private Vector3 destination;

    public bool HasReachedDestination
    {
        get
        {
            return (destination - rb.position).sqrMagnitude <= arriveDistance * arriveDistance;
        }
    }

    public void SetDestination(Vector3 target)
    {
        destination = target;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError($"{name}: SwimMovement에 Rigidbody가 붙어 있지 않다.", this);
        }

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;

        // 목적지를 받기 전에는 제자리에 있어야 한다.
        destination = rb.position;
    }

    private void FixedUpdate()
    {
        if (HasReachedDestination)
        {
            return;
        }

        Vector3 direction = (destination - rb.position).normalized;

        // 뱃머리를 먼저 돌리고 그쪽으로 전진한다.
        // 목적지로 직선으로 당기지 않기 때문에 호를 그리며 물고기처럼 움직인다.
        Quaternion newRotation = Quaternion.RotateTowards(
            rb.rotation,
            Quaternion.LookRotation(direction),
            turnSpeed * Time.fixedDeltaTime);

        // MoveRotation/MovePosition은 지연 적용이라 rb.rotation을 바로 다시 읽으면 갱신 전 값이 나온다.
        // 그래서 전진 방향은 위에서 계산한 newRotation을 그대로 써야 한다.
        Vector3 newPosition = rb.position + (newRotation * Vector3.forward) * (maxSpeed * Time.fixedDeltaTime);

        rb.MoveRotation(newRotation);
        rb.MovePosition(newPosition);
    }
}
