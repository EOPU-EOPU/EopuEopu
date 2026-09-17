using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using GraphNode = UnityEditor.Experimental.GraphView.Node;

public class BehaviorTreeNodeView : GraphNode
{
    private const string INVALID_CLASS = "bt-node--invalid";
    private const string RUNNING_CLASS = "bt-node--running";
    private const string ORDER_LABEL_CLASS = "bt-node__order";

    private readonly Label orderLabel;

    public BehaviorTreeNodeView(BehaviorNodeData data, BehaviorNodeTypeInfo typeInfo)
    {
        Data = data;
        title = typeInfo.DisplayName;

        // GraphView가 선택·펼침 상태를 이 키로 기억한다.
        viewDataKey = data.Id;

        orderLabel = new Label();
        orderLabel.AddToClassList(ORDER_LABEL_CLASS);
        titleContainer.Add(orderLabel);

        CreatePorts(typeInfo.Category);

        // 위치를 데이터에 되쓰는 일은 WriteBack에서만 한다.
        // 여기서 쓰면 드래그 도중 에셋이 바뀌어버려 Undo 스냅샷이 무의미해진다.
        SetPosition(new Rect(data.EditorPosition, Vector2.zero));
    }

    public BehaviorNodeData Data { get; }

    public Port InputPort { get; private set; }

    public Port OutputPort { get; private set; }

    // 형제 중 실행 순서. -1이면 숨긴다 (루트이거나 부모가 없는 노드).
    public void SetSiblingOrder(int order)
    {
        orderLabel.text = order < 0 ? string.Empty : order.ToString();
    }

    public void SetProblem(string message)
    {
        tooltip = message;

        if (string.IsNullOrEmpty(message))
        {
            RemoveFromClassList(INVALID_CLASS);
            return;
        }

        AddToClassList(INVALID_CLASS);
    }

    // 디버깅용. 지금 이 노드가 선택된 에이전트의 실행 경로 위에 있는지.
    public void SetRunning(bool isRunning)
    {
        if (isRunning)
        {
            AddToClassList(RUNNING_CLASS);
            return;
        }

        RemoveFromClassList(RUNNING_CLASS);
    }

    private void CreatePorts(BehaviorNodeCategory category)
    {
        if (category != BehaviorNodeCategory.Root)
        {
            InputPort = InstantiatePort(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(bool));
            InputPort.portName = string.Empty;
            inputContainer.Add(InputPort);
        }

        if (category != BehaviorNodeCategory.Leaf)
        {
            Port.Capacity capacity = category == BehaviorNodeCategory.Composite
                ? Port.Capacity.Multi
                : Port.Capacity.Single;

            OutputPort = InstantiatePort(Orientation.Vertical, Direction.Output, capacity, typeof(bool));
            OutputPort.portName = string.Empty;
            outputContainer.Add(OutputPort);
        }

        RefreshExpandedState();
        RefreshPorts();
    }
}
