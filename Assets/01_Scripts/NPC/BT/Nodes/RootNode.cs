using UnityEngine.UIElements;

public sealed class RootNode : Node
{
    private readonly Node child;
    public RootNode(int index, Node childNode) : base(index)
    {
        child = childNode;
    }

    public override NodeStatus Tick(BehaviorTreeContext context)
    {
        return child.Tick(context);
    }

    public override Node DebugActiveChild(BehaviorTreeContext context) => child;    
}
