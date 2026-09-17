using System.Collections.Generic;
using UnityEngine;

public class BehaviorTreeRunner : MonoBehaviour
{
    [SerializeField] private BehaviorTreeAsset treeAsset;
    [SerializeField] private float tickInterval = 0.1f;
    [SerializeField] private bool logPathChanges;

    private BehaviorTree tree;
    private BehaviorTreeContext context;
    private float timeSinceLastTick;
    private string lastLoggedPath;

    public BehaviorTree Tree => tree;
    public BehaviorTreeContext Context => context;
    public BehaviorTreeAsset TreeAsset => treeAsset;

    private void Awake()
    {
        if (treeAsset == null)
        {
            Debug.LogError($"{name}: BehaviorTreeRunner에 트리 에셋이 지정되지 않았다.", this);
            enabled = false;
            return;
        }

        tree = BehaviorTreeFactory.Build(treeAsset);
        if (tree == null)
        {
            enabled = false;
            return;
        }

        Dictionary<string, IBehaviorAction> actions = new Dictionary<string, IBehaviorAction>();
        foreach (IBehaviorAction action in GetComponents<IBehaviorAction>())
        {
            actions[action.NodeTypeId] = action;
        }

        if (!HasEveryRequiredAction(actions))
        {
            enabled = false;
            return;
        }

        context = new BehaviorTreeContext(tree.NodeCount, actions);
    }

    // SERVER ONLY — BT는 호스트에서만 돈다.
    // 클라이언트에서는 이 컴포넌트를 꺼두고 서버가 보내준 상태를 표시만 한다.
    private void Update()
    {
        timeSinceLastTick += Time.deltaTime;

        if (timeSinceLastTick < tickInterval)
        {
            return;
        }

        context.DeltaTime = timeSinceLastTick;
        timeSinceLastTick = 0f;

        tree.Root.Tick(context);

        if(logPathChanges)
        {
            var path = BehaviorTreeDebugPath.Build(tree, context);
            if (BehaviorTreeDebugPath.TryFormatIfChanged(path, ref lastLoggedPath, out string formatted))
            {
                Debug.Log($"{name}: {formatted}", this);
            }
        }
    }

    private bool HasEveryRequiredAction(Dictionary<string, IBehaviorAction> actions)
    {
        bool complete = true;

        foreach (string nodeTypeId in tree.LeafNodeTypeIds)
        {
            if (actions.ContainsKey(nodeTypeId))
            {
                continue;
            }

            Debug.LogError($"{name}: '{nodeTypeId}' Leaf에 대응하는 IBehaviorAction 컴포넌트가 없다.", this);
            complete = false;
        }

        return complete;
    }
}
