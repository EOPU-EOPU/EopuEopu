using System.Collections.Generic;
using UnityEngine;

//트리 에셋의 틀
[CreateAssetMenu(fileName = "BehaviorTree_", menuName = "EopuEopu/Behavior Tree")]
public class BehaviorTreeAsset : ScriptableObject
{
    [SerializeField] private List<BehaviorNodeData> nodes = new List<BehaviorNodeData>();
    [SerializeField] private string rootNodeId;

    public IReadOnlyList<BehaviorNodeData> Nodes => nodes;

    public string RootNodeId => rootNodeId;

#if UNITY_EDITOR
    public void EditorReplaceNodes(List<BehaviorNodeData> newNodes, string newRootNodeId)
    {
        nodes = newNodes;
        rootNodeId = newRootNodeId;
    }
#endif
}
