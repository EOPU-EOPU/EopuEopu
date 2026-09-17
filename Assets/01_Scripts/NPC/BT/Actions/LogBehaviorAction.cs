using UnityEngine;

public class LogBehaviorAction : MonoBehaviour, IBehaviorAction
{
    [SerializeField] private string message = "Log";

    public string NodeTypeId => BehaviorNodeTypes.LOG;

    public NodeStatus Tick(BehaviorTreeContext context)
    {
        Debug.Log($"[BT] {name}: {message}", this);
        return NodeStatus.Success;
    }
}
