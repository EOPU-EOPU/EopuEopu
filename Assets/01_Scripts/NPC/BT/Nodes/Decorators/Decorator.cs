public abstract class Decorator : Node
{
    protected Decorator(int index, Node childNode) : base(index)
    {
        Child = childNode;
    }

    protected Node Child { get; }

    public override Node DebugActiveChild(BehaviorTreeContext context) => Child;   
}
