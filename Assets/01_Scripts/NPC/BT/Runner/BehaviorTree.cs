using System.Collections.Generic;

// 팩토리가 만들어낸 실행 가능한 트리. 여러 에이전트가 공유한다.
public sealed class BehaviorTree
{
    public BehaviorTree(
        Node root,
        int nodeCount,
        IReadOnlyList<string> leafNodeTypeIds,
        IReadOnlyList<string> nodeDebugLabels,
        IReadOnlyList<string> nodeAssetIds)
    {
        Root = root;
        NodeCount = nodeCount;
        LeafNodeTypeIds = leafNodeTypeIds;
        NodeDebugLabels = nodeDebugLabels;
        NodeAssetIds = nodeAssetIds;
    }

    public Node Root { get; }

    // 에이전트별 실행 상태 배열의 크기.
    public int NodeCount { get; }

    // 러너가 필요한 액션이 다 붙어 있는지 빌드 직후 확인하는 용도.
    public IReadOnlyList<string> LeafNodeTypeIds { get; }

    public IReadOnlyList<string> NodeDebugLabels { get; }

    // Node.Index -> BehaviorNodeData.Id. 런타임 그래프의 노드를 에셋/그래프뷰의 노드와 잇는 유일한 통로.
    public IReadOnlyList<string> NodeAssetIds { get; }
}
