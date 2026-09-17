using System.Collections.Generic;
using UnityEngine;

public static class BehaviorTreeFactory
{
    // 실패하면 null을 돌려준다. 호출자가 그때 컴포넌트를 꺼야 한다.
    public static BehaviorTree Build(BehaviorTreeAsset asset)
    {
        if (asset == null)
        {
            Debug.LogError("BehaviorTreeFactory: 트리 에셋이 없다.");
            return null;
        }

        if (!PassesValidation(asset))
        {
            return null;
        }

        Dictionary<string, BehaviorNodeData> dataById = new Dictionary<string, BehaviorNodeData>();
        foreach (BehaviorNodeData data in asset.Nodes)
        {
            dataById[data.Id] = data;
        }

        List<string> leafNodeTypeIds = new List<string>();
        List<string> nodeDebugLabels = new List<string>();
        List<string> nodeAssetIds = new List<string>();
        int nodeCount = 0;

        Node root = BuildNode(asset.RootNodeId, dataById, leafNodeTypeIds, nodeDebugLabels, nodeAssetIds, ref nodeCount);
        if (root == null)
        {
            Debug.LogError($"BehaviorTreeFactory: {asset.name} 트리를 만들지 못했다.");
            return null;
        }

        return new BehaviorTree(root, nodeCount, leafNodeTypeIds, nodeDebugLabels, nodeAssetIds);
    }

    // 트리 전체에 걸린 문제(루트 없음·중복, 순환)만 빌드를 막는다.
    // 고아 노드처럼 실행에 지장 없는 문제는 경고만 하고 넘어간다.
    private static bool PassesValidation(BehaviorTreeAsset asset)
    {
        List<BehaviorNodeProblem> problems = new List<BehaviorNodeProblem>();
        BehaviorTreeValidator.Validate(asset, problems);

        bool blocked = false;

        foreach (BehaviorNodeProblem problem in problems)
        {
            if (string.IsNullOrEmpty(problem.NodeId))
            {
                Debug.LogError($"BehaviorTreeFactory: {asset.name} — {problem.Message}");
                blocked = true;
                continue;
            }

            Debug.LogWarning($"BehaviorTreeFactory: {asset.name} — {problem.Message}");
        }

        return !blocked;
    }

    private static Node BuildNode(
        string nodeId,
        Dictionary<string, BehaviorNodeData> dataById,
        List<string> leafNodeTypeIds,
        List<string> nodeDebugLabels,
        List<string> nodeAssetIds,
        ref int nodeCount)
    {
        if (!dataById.TryGetValue(nodeId, out BehaviorNodeData data))
        {
            return null;
        }

        if (!BehaviorNodeTypes.TryGet(data.NodeTypeId, out BehaviorNodeTypeInfo typeInfo))
        {
            Debug.LogError($"BehaviorTreeFactory: 모르는 노드 종류다. nodeTypeId={data.NodeTypeId}");
            return null;
        }

        int index = nodeCount;
        nodeCount++;
        nodeDebugLabels.Add(typeInfo.DisplayName);
        nodeAssetIds.Add(data.Id);

        switch (data.NodeTypeId)
        {
            case BehaviorNodeTypes.ROOT:
            {
                Node child = BuildOnlyChild(data, dataById, leafNodeTypeIds, nodeDebugLabels, nodeAssetIds, ref nodeCount);
                return child == null ? null : new RootNode(index, child);
            }

            case BehaviorNodeTypes.INVERTER:
            {
                Node child = BuildOnlyChild(data, dataById, leafNodeTypeIds, nodeDebugLabels, nodeAssetIds, ref nodeCount);
                return child == null ? null : new Inverter(index, child);
            }

            case BehaviorNodeTypes.SEQUENCE:
                return new Sequence(index, BuildChildren(data, dataById, leafNodeTypeIds, nodeDebugLabels, nodeAssetIds, ref nodeCount));

            case BehaviorNodeTypes.SELECTOR:
                return new Selector(index, BuildChildren(data, dataById, leafNodeTypeIds, nodeDebugLabels, nodeAssetIds, ref nodeCount));

            default:
                // 나머지는 전부 Leaf다. Leaf를 추가할 때 이 팩토리는 고치지 않는다.
                if (typeInfo.Category != BehaviorNodeCategory.Leaf)
                {
                    Debug.LogError($"BehaviorTreeFactory: 만들 줄 모르는 노드다. nodeTypeId={data.NodeTypeId}");
                    return null;
                }

                leafNodeTypeIds.Add(data.NodeTypeId);
                return new Leaf(index, data.NodeTypeId);
        }
    }

    // 자식이 하나여야 하는 노드(Root, Inverter)
    private static Node BuildOnlyChild(
        BehaviorNodeData data,
        Dictionary<string, BehaviorNodeData> dataById,
        List<string> leafNodeTypeIds,
        List<string> nodeDebugLabels,
        List<string> nodeAssetIds,
        ref int nodeCount)
    {
        if (data.ChildIds.Count == 0)
        {
            Debug.LogError($"BehaviorTreeFactory: '{data.NodeTypeId}' 노드에 자식이 없다.");
            return null;
        }

        return BuildNode(data.ChildIds[0], dataById, leafNodeTypeIds, nodeDebugLabels, nodeAssetIds, ref nodeCount);
    }

    // 자식이 여러개 가능한 노드(Sequence, Selector)
    private static Node[] BuildChildren(
        BehaviorNodeData data,
        Dictionary<string, BehaviorNodeData> dataById,
        List<string> leafNodeTypeIds,
        List<string> nodeDebugLabels,
        List<string> nodeAssetIds,
        ref int nodeCount)
    {
        List<Node> children = new List<Node>();

        foreach (string childId in data.ChildIds)
        {
            Node child = BuildNode(childId, dataById, leafNodeTypeIds, nodeDebugLabels, nodeAssetIds, ref nodeCount);
            if (child != null)
            {
                children.Add(child);
            }
        }

        return children.ToArray();
    }
}
