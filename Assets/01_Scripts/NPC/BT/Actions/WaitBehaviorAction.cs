using UnityEngine;

public class WaitBehaviorAction : MonoBehaviour, IBehaviorAction
{
    [SerializeField] private float duration = 2f;

    private float elapsed;

    public string NodeTypeId => BehaviorNodeTypes.WAIT;

    public NodeStatus Tick(BehaviorTreeContext context)
    {
        // 아직 선점(preemption)이 없어서 중간에 버려지는 경우가 없다.
        // 조건 노드가 들어오면 OnEnter/OnAbort가 필요해진다.
        elapsed += context.DeltaTime;

        if (elapsed < duration)
        {
            return NodeStatus.Running;
        }

        elapsed = 0f;
        return NodeStatus.Success;
    }
}
