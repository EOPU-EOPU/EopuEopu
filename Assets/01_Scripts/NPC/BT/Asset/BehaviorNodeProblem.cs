public readonly struct BehaviorNodeProblem
{
    public BehaviorNodeProblem(string nodeId, string message)
    {
        NodeId = nodeId;
        Message = message;
    }

    // 빈 문자열이면 특정 노드가 아니라 트리 전체의 문제다.
    public string NodeId { get; }

    public string Message { get; }
}
