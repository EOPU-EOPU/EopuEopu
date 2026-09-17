public sealed class Inverter : Decorator
{
    public Inverter(int index, Node childNode) : base(index, childNode)
    {
    }

    public override NodeStatus Tick(BehaviorTreeContext context)
    {
        NodeStatus childStatus = Child.Tick(context);

        if (childStatus == NodeStatus.Success)
        {
            return NodeStatus.Failure;
        }

        if (childStatus == NodeStatus.Failure)
        {
            return NodeStatus.Success;
        }

        return NodeStatus.Running;
    }
}
