using System.Collections.Generic;

// 에이전트 한 마리당 하나씩 만들어지는 개인 상태 보관함 같은 개념.
// 트리 인스턴스는 공유되므로 실행 상태는 전부 여기 모인다.
// Leaf끼리 데이터를 주고받을 땐 Blackboard 프로퍼티를 통해 읽고 쓴다.
public sealed class BehaviorTreeContext
{
    private readonly int[] childCursors;
    private readonly Dictionary<string, IBehaviorAction> actionsByNodeTypeId;

    public BehaviorTreeContext(int nodeCount, Dictionary<string, IBehaviorAction> actions, Blackboard blackboard)
    {
        childCursors = new int[nodeCount];
        actionsByNodeTypeId = actions;
        Blackboard = blackboard;
    }

    public Blackboard Blackboard { get; }

    // 직전 tick 이후 경과 시간. Time.deltaTime이 아니다 — tick은 5~10Hz로 돈다.
    public float DeltaTime { get; set; }

    public int GetChildCursor(int nodeIndex)
    {
        return childCursors[nodeIndex];
    }

    public void SetChildCursor(int nodeIndex, int cursor)
    {
        childCursors[nodeIndex] = cursor;
    }

    public bool TryGetAction(string nodeTypeId, out IBehaviorAction action)
    {
        return actionsByNodeTypeId.TryGetValue(nodeTypeId, out action);
    }
}
