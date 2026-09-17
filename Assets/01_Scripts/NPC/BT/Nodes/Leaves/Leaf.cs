public sealed class Leaf : Node
{
    private readonly string nodeTypeId;

    public Leaf(int index, string typeId) : base(index)
    {
        nodeTypeId = typeId;
    }

    public override NodeStatus Tick(BehaviorTreeContext context)
    {
        // 액션 누락은 러너가 빌드 직후 한 번 걸러낸다.
        // 여기서 로그를 찍으면 tick 주기만큼 초당 여러 번 도배된다.
        if (!context.TryGetAction(nodeTypeId, out IBehaviorAction action))
        {
            return NodeStatus.Failure;
        }

        return action.Tick(context);
    }
}
