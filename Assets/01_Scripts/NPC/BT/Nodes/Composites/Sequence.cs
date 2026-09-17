public sealed class Sequence : Composite
{
    public Sequence(int index, Node[] childNodes) : base(index, childNodes)
    {
    }

    protected override NodeStatus CompletedStatus => NodeStatus.Success;

    protected override bool ShouldStop(NodeStatus childStatus)
    {
        return childStatus == NodeStatus.Failure;
    }
}
