using System.Collections.Generic;

// 구조만 본다. 노드가 무슨 행동을 하는지는 판단하지 않는다.
// 에디터가 빨간 테두리를 그리는 데 쓰고, 런타임 팩토리도 빌드 전에 같은 검사를 쓴다.
public static class BehaviorTreeValidator
{
    public static void Validate(BehaviorTreeAsset asset, List<BehaviorNodeProblem> results)
    {
        results.Clear();

        if (asset == null)
        {
            return;
        }

        Dictionary<string, BehaviorNodeData> dataById = new Dictionary<string, BehaviorNodeData>();
        HashSet<string> connectedChildIds = new HashSet<string>();

        foreach (BehaviorNodeData data in asset.Nodes)
        {
            dataById[data.Id] = data;

            foreach (string childId in data.ChildIds)
            {
                connectedChildIds.Add(childId);
            }
        }

        int rootCount = 0;

        foreach (BehaviorNodeData data in asset.Nodes)
        {
            if (!BehaviorNodeTypes.TryGet(data.NodeTypeId, out BehaviorNodeTypeInfo typeInfo))
            {
                results.Add(new BehaviorNodeProblem(data.Id, $"모르는 노드 종류다: {data.NodeTypeId}"));
                continue;
            }

            if (typeInfo.Category == BehaviorNodeCategory.Root)
            {
                rootCount++;
            }
            else if (!connectedChildIds.Contains(data.Id))
            {
                results.Add(new BehaviorNodeProblem(data.Id, "부모가 없다. 트리에서 떨어져 있어 실행되지 않는다."));
            }

            if (typeInfo.Category != BehaviorNodeCategory.Leaf && data.ChildIds.Count == 0)
            {
                results.Add(new BehaviorNodeProblem(data.Id, "자식이 없다."));
            }
        }

        if (rootCount == 0)
        {
            results.Add(new BehaviorNodeProblem(string.Empty, "Root 노드가 없다."));
        }
        else if (rootCount > 1)
        {
            results.Add(new BehaviorNodeProblem(string.Empty, $"Root 노드가 {rootCount}개다. 하나만 있어야 한다."));
        }

        AddCycleProblems(asset, dataById, results);
    }

    // 에디터는 자기 자신으로 잇는 것만 막는다. A→B→C→A 같은 순환은 만들 수 있고,
    // 그대로 팩토리에 넘기면 무한 재귀가 된다. 그래서 여기서 반드시 잡아야 한다.
    private static void AddCycleProblems(
        BehaviorTreeAsset asset,
        Dictionary<string, BehaviorNodeData> dataById,
        List<BehaviorNodeProblem> results)
    {
        HashSet<string> visited = new HashSet<string>();
        HashSet<string> inStack = new HashSet<string>();
        HashSet<string> cycleNodeIds = new HashSet<string>();

        foreach (BehaviorNodeData data in asset.Nodes)
        {
            FindCycles(data.Id, dataById, visited, inStack, cycleNodeIds);
        }

        if (cycleNodeIds.Count == 0)
        {
            return;
        }

        foreach (string cycleNodeId in cycleNodeIds)
        {
            results.Add(new BehaviorNodeProblem(cycleNodeId, "순환 연결이다. 자기 자신으로 돌아오는 경로가 있다."));
        }

        results.Add(new BehaviorNodeProblem(string.Empty, "순환 연결이 있어 트리를 만들 수 없다."));
    }

    private static void FindCycles(
        string nodeId,
        Dictionary<string, BehaviorNodeData> dataById,
        HashSet<string> visited,
        HashSet<string> inStack,
        HashSet<string> cycleNodeIds)
    {
        if (!dataById.TryGetValue(nodeId, out BehaviorNodeData data))
        {
            return;
        }

        // 지금 내려온 경로에 다시 등장했다면 순환이다. visited보다 먼저 봐야 한다.
        if (inStack.Contains(nodeId))
        {
            cycleNodeIds.Add(nodeId);
            return;
        }

        if (!visited.Add(nodeId))
        {
            return;
        }

        inStack.Add(nodeId);

        foreach (string childId in data.ChildIds)
        {
            FindCycles(childId, dataById, visited, inStack, cycleNodeIds);
        }

        inStack.Remove(nodeId);
    }
}
