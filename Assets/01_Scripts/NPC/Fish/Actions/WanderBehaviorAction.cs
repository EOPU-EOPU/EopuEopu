using UnityEngine;

public class WanderBehaviorAction : MonoBehaviour, IBehaviorAction
{
    // 기획이 미확정이라 전부 임의의 placeholder 값이다.
    [SerializeField] private float horizontalRadius = 10f;
    [SerializeField] private float verticalRange = 3f;
    [SerializeField] private float maxDuration = 15f;

    private IMovement movement;
    private Vector3 homePosition;
    private Vector3 currentDestination;
    private float elapsed;
    private bool isWandering;

    public string NodeTypeId => BehaviorNodeTypes.WANDER;

    public NodeStatus Tick(BehaviorTreeContext context)
    {
        if (movement == null)
        {
            return NodeStatus.Failure;
        }

        if (!isWandering)
        {
            // SERVER ONLY — 랜덤은 서버에서 한 번만 굴려야 한다.
            // BT를 서버에서만 틱하므로 구조상 이미 만족한다.
            currentDestination = PickDestination();
            movement.SetDestination(currentDestination);
            elapsed = 0f;
            isWandering = true;
            return NodeStatus.Running;
        }

        elapsed += context.DeltaTime;

        // 선회 반경이 목적지까지의 거리보다 크면 영원히 궤도를 돈다.
        // 방어 코드가 아니라 호를 그리는 이동에서 실제로 일어나는 기하학적 실패다.
        if (movement.HasReachedDestination || elapsed >= maxDuration)
        {
            isWandering = false;
            return NodeStatus.Success;
        }

        return NodeStatus.Running;
    }

    private void Awake()
    {
        movement = GetComponent<IMovement>();
        if (movement == null)
        {
            Debug.LogError($"{name}: WanderBehaviorAction에 IMovement 구현체가 붙어 있지 않다.", this);
        }

        homePosition = transform.position;
    }

    private Vector3 PickDestination()
    {
        Vector2 circle = Random.insideUnitCircle * horizontalRadius;
        float height = Random.Range(-verticalRange, verticalRange);

        return homePosition + new Vector3(circle.x, height, circle.y);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 home = Application.isPlaying ? homePosition : transform.position;

        // 실제 추첨 범위는 원기둥이지만, 수평 반경과 수직 범위를 한눈에 보려고 타원체로 그린다.
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(home, Quaternion.identity, new Vector3(horizontalRadius, verticalRange, horizontalRadius));
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.6f);
        Gizmos.DrawWireSphere(Vector3.zero, 1f);
        Gizmos.matrix = previousMatrix;

        if (!isWandering)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, currentDestination);
        Gizmos.DrawWireSphere(currentDestination, 0.3f);
    }
}
