public sealed class Selector : Composite
{
    public Selector(int index, Node[] childNodes) : base(index, childNodes)
    {
    }

    protected override NodeStatus CompletedStatus => NodeStatus.Failure;

    protected override bool ShouldStop(NodeStatus childStatus)
    {
        return childStatus == NodeStatus.Success;
    }
}
