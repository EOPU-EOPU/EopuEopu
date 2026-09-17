public abstract class Composite : Node
{
    private readonly Node[] children;

    protected Composite(int index, Node[] childNodes) : base(index)
    {
        children = childNodes;
    }

    protected abstract NodeStatus CompletedStatus { get; }

    protected abstract bool ShouldStop(NodeStatus childStatus);

    public override NodeStatus Tick(BehaviorTreeContext context)
    {
        // 저장된 커서에서 이어서 돈다. 이미 끝난 자식은 다시 tick 하지 않는다.
        int cursor = context.GetChildCursor(Index);

        while (cursor < children.Length)
        {
            NodeStatus childStatus = children[cursor].Tick(context);

            if (childStatus == NodeStatus.Running)
            {
                context.SetChildCursor(Index, cursor);
                return NodeStatus.Running;
            }

            if (ShouldStop(childStatus))
            {
                context.SetChildCursor(Index, 0);
                return childStatus;
            }

            cursor++;
        }

        context.SetChildCursor(Index, 0);
        return CompletedStatus;
    }

    public override Node DebugActiveChild(BehaviorTreeContext context)
    {
        int cursor = context.GetChildCursor(Index);
        return cursor < children.Length ? children[cursor] : null;
    }
}
