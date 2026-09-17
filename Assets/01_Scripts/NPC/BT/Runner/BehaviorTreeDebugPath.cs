using System;
using System.Collections.Generic;
using UnityEngine;

public class BehaviorTreeDebugPath
{
    public static IReadOnlyList<string> Build(BehaviorTree tree, BehaviorTreeContext context)
    {
        return WalkActivePath(tree, context, (t, node) => t.NodeDebugLabels[node.Index]);
    }

    // 그래프뷰가 노드를 하이라이트할 때 쓰는 값. 표시용 이름이 아니라 BehaviorNodeData.Id다.
    public static IReadOnlyList<string> BuildActiveAssetIds(BehaviorTree tree, BehaviorTreeContext context)
    {
        return WalkActivePath(tree, context, (t, node) => t.NodeAssetIds[node.Index]);
    }

    // Root부터 DebugActiveChild를 따라 내려가며 각 노드에서 값을 하나씩 뽑는다.
    private static List<string> WalkActivePath(BehaviorTree tree, BehaviorTreeContext context, Func<BehaviorTree, Node, string> select)
    {
        List<string> path = new List<string>();
        Node current = tree?.Root;

        while (current != null)
        {
            path.Add(select(tree, current));
            current = current.DebugActiveChild(context);
        }

        return path;
    }

    public static bool TryFormatIfChanged(IReadOnlyList<string> path, ref string lastFormatted, out string formatted)
    {
        formatted = string.Join(" > ", path);

        if(formatted == lastFormatted)
        {
            return false;
        }

        lastFormatted = formatted;
        return true;
    }
}
