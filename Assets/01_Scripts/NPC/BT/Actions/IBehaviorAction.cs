// 실제 행동 코드는 노드 밖에 산다. Leaf는 이 인터페이스를 호출만 한다.
// 나중에 BT 패키지로 갈아타도 이쪽 구현체는 그대로 살아남는다.
public interface IBehaviorAction
{
    string NodeTypeId { get; }

    NodeStatus Tick(BehaviorTreeContext context);
}
