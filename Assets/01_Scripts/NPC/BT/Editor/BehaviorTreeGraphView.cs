using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class BehaviorTreeGraphView : GraphView
{
    private const string STYLE_SHEET_PATH = "Assets/01_Scripts/NPC/BT/Editor/BehaviorTreeGraphView.uss";
    private const string UNDO_LABEL = "Behavior Tree Edit";

    private readonly List<BehaviorNodeProblem> problems = new List<BehaviorNodeProblem>();

    private BehaviorTreeAsset boundAsset;

    public BehaviorTreeGraphView()
    {
        style.flexGrow = 1f;

        // GridBackground는 0번에 넣어야 노드보다 뒤에 깔린다.
        Insert(0, new GridBackground());

        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(STYLE_SHEET_PATH);
        if (styleSheet == null)
        {
            Debug.LogError($"BehaviorTreeGraphView: 스타일시트를 찾지 못했다. 격자가 보이지 않는다. 경로: {STYLE_SHEET_PATH}");
        }
        else
        {
            styleSheets.Add(styleSheet);
        }

        graphViewChanged += OnGraphViewChanged;

        // 창 수명이 아니라 비주얼 엘리먼트 수명에 묶는다. 도메인 리로드 후 죽은 콜백이 남지 않는다.
        RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += OnUndoRedoPerformed);
        RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= OnUndoRedoPerformed);

        // Play 모드에서 선택된 에이전트의 실행 경로를 하이라이트하기 위한 폴링.
        RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.update += OnEditorUpdate);
        RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.update -= OnEditorUpdate);
    }

    // 트리 전체에 걸린 문제. 문제가 없으면 빈 문자열이 온다.
    public event Action<string> ValidationMessageChanged;

    public void Bind(BehaviorTreeAsset asset)
    {
        boundAsset = asset;
        Rebuild();
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        List<Port> compatiblePorts = new List<Port>();

        ports.ForEach(port =>
        {
            if (port.direction == startPort.direction)
            {
                return;
            }

            if (port.node == startPort.node)
            {
                return;
            }

            compatiblePorts.Add(port);
        });

        return compatiblePorts;
    }

    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        if (boundAsset == null)
        {
            evt.menu.AppendAction("에셋을 먼저 열어라 (.asset 더블클릭)", _ => { }, DropdownMenuAction.Status.Disabled);
            base.BuildContextualMenu(evt);
            return;
        }

        // 줌/팬 상태에서도 클릭한 자리에 생기도록 그래프 내부 좌표로 변환한다.
        Vector2 spawnPosition = contentViewContainer.WorldToLocal(evt.mousePosition);
        bool hasRoot = HasRootNode();

        foreach (BehaviorNodeTypeInfo typeInfo in BehaviorNodeTypes.All)
        {
            BehaviorNodeTypeInfo captured = typeInfo;
            bool blocked = hasRoot && captured.Category == BehaviorNodeCategory.Root;

            evt.menu.AppendAction(
                $"Create/{captured.DisplayName}",
                _ => CreateNode(captured, spawnPosition),
                blocked ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
        }

        base.BuildContextualMenu(evt);
    }

    private bool HasRootNode()
    {
        bool found = false;

        nodes.ForEach(node =>
        {
            if (node is BehaviorTreeNodeView nodeView && nodeView.Data.NodeTypeId == BehaviorNodeTypes.ROOT)
            {
                found = true;
            }
        });

        return found;
    }

    private void CreateNode(BehaviorNodeTypeInfo typeInfo, Vector2 position)
    {
        Undo.RegisterCompleteObjectUndo(boundAsset, UNDO_LABEL);

        BehaviorNodeData data = new BehaviorNodeData(Guid.NewGuid().ToString(), typeInfo.Id, position);
        AddElement(new BehaviorTreeNodeView(data, typeInfo));
        WriteBack();
    }

    private GraphViewChange OnGraphViewChanged(GraphViewChange change)
    {
        // 이 콜백은 변경이 '적용되기 전'에 불린다.
        // 그래서 지금 찍는 스냅샷이 변경 전 상태가 되고, 저장은 반영된 뒤인 다음 틱에 한다.
        if (boundAsset != null)
        {
            Undo.RegisterCompleteObjectUndo(boundAsset, UNDO_LABEL);
        }

        schedule.Execute(WriteBack).ExecuteLater(0);
        return change;
    }

    private void OnUndoRedoPerformed()
    {
        // 에셋이 이미 되돌려진 상태다. 그래프를 거기에 맞춰 다시 그린다.
        Rebuild();
    }

    private void Rebuild()
    {
        graphViewChanged -= OnGraphViewChanged;

        List<GraphElement> existing = new List<GraphElement>();
        graphElements.ForEach(existing.Add);
        DeleteElements(existing);

        graphViewChanged += OnGraphViewChanged;

        if (boundAsset == null)
        {
            RefreshDerivedVisuals();
            return;
        }

        Dictionary<string, BehaviorTreeNodeView> viewsById = new Dictionary<string, BehaviorTreeNodeView>();

        foreach (BehaviorNodeData data in boundAsset.Nodes)
        {
            if (!BehaviorNodeTypes.TryGet(data.NodeTypeId, out BehaviorNodeTypeInfo typeInfo))
            {
                Debug.LogError($"BehaviorTreeGraphView: 모르는 노드 종류라 건너뛴다. nodeTypeId={data.NodeTypeId}");
                continue;
            }

            BehaviorTreeNodeView nodeView = new BehaviorTreeNodeView(data, typeInfo);
            AddElement(nodeView);
            viewsById[data.Id] = nodeView;
        }

        foreach (BehaviorNodeData data in boundAsset.Nodes)
        {
            if (!viewsById.TryGetValue(data.Id, out BehaviorTreeNodeView parentView) || parentView.OutputPort == null)
            {
                continue;
            }

            foreach (string childId in data.ChildIds)
            {
                if (!viewsById.TryGetValue(childId, out BehaviorTreeNodeView childView) || childView.InputPort == null)
                {
                    continue;
                }

                AddElement(parentView.OutputPort.ConnectTo(childView.InputPort));
            }
        }

        RefreshDerivedVisuals();
    }

    private void WriteBack()
    {
        if (boundAsset == null)
        {
            return;
        }

        List<BehaviorNodeData> collected = new List<BehaviorNodeData>();
        List<BehaviorTreeNodeView> nodeViews = new List<BehaviorTreeNodeView>();
        string rootNodeId = string.Empty;

        nodes.ForEach(node =>
        {
            if (node is not BehaviorTreeNodeView nodeView)
            {
                return;
            }

            // 1 — 지금 화면상 위치를 읽어서 Data.EditorPosition에 씀. 예전엔 NodeView.SetPosition에서 드래그 매 프레임 이걸 했는데, Undo 타이밍 문제로 옮김.
            // 2 — 이 노드를 nodeViews(2바퀴에서 쓸 목록)와 collected(최종적으로 에셋에 저장될 목록)에 등록.
            // 3 — 타입이 Root고 아직 루트를 못 찾았으면 이 노드의 id를 루트로 기억. (Root가 여러 개면 첫 번째로 발견된 것만 루트가 된다는 게 여기서 정해진다.)
            nodeView.Data.EditorPosition = nodeView.GetPosition().position; // 1

            nodeViews.Add(nodeView); // 2
            collected.Add(nodeView.Data); // 2

            if (nodeView.Data.NodeTypeId == BehaviorNodeTypes.ROOT && string.IsNullOrEmpty(rootNodeId))
            {
                rootNodeId = nodeView.Data.Id; // 3
            }
        });

        // 4 - "clear하고 처음부터 다시 채우기" 패턴. 예전 자식 목록은 다 지운다.
        // 5 - Leaf는 OutputPort 자체가 없다(카테고리에 따라 아예 안 만들었으니), 그러니 자식을 못 두고 바로 다음 노드.
        // 6 - OutputPort.connections는 GraphView가 관리하는 "이 포트에 실제로 꽂힌 엣지들". 각 엣지의 edge.input(반대쪽 끝, 자식의 입력 포트)에서 .node로 그 포트를 가진 노드뷰를 거내 children에 모은다.
        // 7 - X좌표 왼쪽부터 오른쪽 순 정렬.
        // 8 - 정렬된 순서 그대로 AddChild. 이 순서가 곧 저장되는 실행 순서다.
        foreach (BehaviorTreeNodeView nodeView in nodeViews)
        {
            nodeView.Data.ClearChildren(); // 4

            if (nodeView.OutputPort == null) // 5
            {
                continue;
            }

            List<BehaviorTreeNodeView> children = new List<BehaviorTreeNodeView>();
            foreach (Edge edge in nodeView.OutputPort.connections) // 6
            {
                if (edge.input != null && edge.input.node is BehaviorTreeNodeView childView)
                {
                    children.Add(childView);
                }
            }

            // 자식 실행 순서 = 화면상 왼쪽에서 오른쪽 순.
            children.Sort((left, right) => left.GetPosition().x.CompareTo(right.GetPosition().x)); // 7

            foreach (BehaviorTreeNodeView childView in children)
            {
                nodeView.Data.AddChild(childView.Data.Id); // 8
            }
        }

        // 9 - 지금까지 계산한 collected(전체 노드 목록)와 rootNodeld를 아까 본 그 유일한 쓰기 문으로 통째로 밀어 넣는다.
        // 10 - "에셋이 바뀌었다"고 유니티에 표시, 이게 없으면 Ctrl + s나 창 닫기가 이 변경을 저장 안 해준다.
        // 11 - 에셋이 최신화가 됐으니, 순서 번호와 빨간 테두리(검증 결과)도 그에 맞게 다시 그린다.
        boundAsset.EditorReplaceNodes(collected, rootNodeId); // 9
        EditorUtility.SetDirty(boundAsset); // 10

        RefreshDerivedVisuals(); // 11
    }

    // 에셋에서 파생되는 '표시'만 갱신한다. 에셋을 바꾸지 않으므로 어디서 불러도 안전하다.
    private void RefreshDerivedVisuals()
    {
        Dictionary<string, BehaviorTreeNodeView> viewsById = new Dictionary<string, BehaviorTreeNodeView>();

        nodes.ForEach(node =>
        {
            if (node is not BehaviorTreeNodeView nodeView)
            {
                return;
            }

            viewsById[nodeView.Data.Id] = nodeView;
            nodeView.SetSiblingOrder(-1);
            nodeView.SetProblem(string.Empty);
        });

        if (boundAsset == null)
        {
            ValidationMessageChanged?.Invoke(string.Empty);
            return;
        }

        foreach (BehaviorNodeData data in boundAsset.Nodes)
        {
            for (int i = 0; i < data.ChildIds.Count; i++)
            {
                if (viewsById.TryGetValue(data.ChildIds[i], out BehaviorTreeNodeView childView))
                {
                    childView.SetSiblingOrder(i);
                }
            }
        }

        BehaviorTreeValidator.Validate(boundAsset, problems);

        Dictionary<string, string> messagesByNode = new Dictionary<string, string>();
        List<string> treeMessages = new List<string>();

        foreach (BehaviorNodeProblem problem in problems)
        {
            if (string.IsNullOrEmpty(problem.NodeId))
            {
                treeMessages.Add(problem.Message);
                continue;
            }

            messagesByNode[problem.NodeId] = messagesByNode.TryGetValue(problem.NodeId, out string existing)
                ? $"{existing}\n{problem.Message}"
                : problem.Message;
        }

        foreach (KeyValuePair<string, string> pair in messagesByNode)
        {
            if (viewsById.TryGetValue(pair.Key, out BehaviorTreeNodeView nodeView))
            {
                nodeView.SetProblem(pair.Value);
            }
        }

        ValidationMessageChanged?.Invoke(string.Join("   ", treeMessages));
    }

    // Hierarchy에서 선택된 오브젝트가 지금 이 에셋으로 도는 에이전트면, 그 실행 경로를 노드에 색칠한다.
    private void OnEditorUpdate()
    {
        BehaviorTreeRunner runner = FindMatchingRunner();

        if (runner == null)
        {
            ApplyRunningHighlight(Array.Empty<string>());
            return;
        }

        IReadOnlyList<string> activeAssetIds = BehaviorTreeDebugPath.BuildActiveAssetIds(runner.Tree, runner.Context);
        ApplyRunningHighlight(activeAssetIds);
    }

    // 지금 열려있는 에셋을 실제로 돌리고 있는 선택된 에이전트. 없으면 null.
    private BehaviorTreeRunner FindMatchingRunner()
    {
        if (!Application.isPlaying || boundAsset == null)
        {
            return null;
        }

        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            return null;
        }

        BehaviorTreeRunner runner = selected.GetComponent<BehaviorTreeRunner>();
        if (runner == null || runner.TreeAsset != boundAsset || runner.Tree == null)
        {
            return null;
        }

        return runner;
    }

    private void ApplyRunningHighlight(IReadOnlyList<string> activeAssetIds)
    {
        HashSet<string> activeSet = new HashSet<string>(activeAssetIds);

        nodes.ForEach(node =>
        {
            if (node is BehaviorTreeNodeView nodeView)
            {
                nodeView.SetRunning(activeSet.Contains(nodeView.Data.Id));
            }
        });
    }
}
