public abstract class Node
{
    protected Node(int index)
    {
        Index = index;
    }

    // 팩토리가 빌드하면서 부여한다. 에이전트별 실행 상태를 배열에서 찾는 키다.
    // 노드 자신은 상태를 갖지 않는다 — 트리 인스턴스를 여러 에이전트가 공유하기 때문이다.
    public int Index { get; }

    public abstract NodeStatus Tick(BehaviorTreeContext context);

    public virtual Node DebugActiveChild(BehaviorTreeContext context) => null;
}
