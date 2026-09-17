using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class BehaviorTreeEditorWindow : EditorWindow
{
    private const string WINDOW_TITLE = "Behavior Tree";

    // 도메인 리로드를 넘어 살아남는 유일한 상태. 그래프는 이 에셋에서 다시 만든다.
    [SerializeField] private BehaviorTreeAsset currentAsset;

    private BehaviorTreeGraphView graphView;
    private Label assetNameLabel;
    private Label validationLabel;

    [MenuItem("EopuEopu/Behavior Tree Editor")]
    public static void Open()
    {
        OpenWindow();
    }

    [OnOpenAsset]
    public static bool OnOpenAsset(int instanceId, int line)
    {
        BehaviorTreeAsset asset = EditorUtility.InstanceIDToObject(instanceId) as BehaviorTreeAsset;
        if (asset == null)
        {
            return false;
        }

        OpenWindow().SetAsset(asset);
        return true;
    }

    private static BehaviorTreeEditorWindow OpenWindow()
    {
        BehaviorTreeEditorWindow window = GetWindow<BehaviorTreeEditorWindow>();
        window.titleContent = new GUIContent(WINDOW_TITLE);
        return window;
    }

    private void CreateGUI()
    {
        Toolbar toolbar = new Toolbar();
        assetNameLabel = new Label();
        toolbar.Add(assetNameLabel);
        toolbar.Add(new ToolbarButton(SaveAsset) { text = "Save" });

        validationLabel = new Label();
        validationLabel.style.color = new StyleColor(new Color(0.95f, 0.45f, 0.45f));
        toolbar.Add(validationLabel);

        rootVisualElement.Add(toolbar);

        graphView = new BehaviorTreeGraphView();
        rootVisualElement.Add(graphView);

        // Bind가 첫 검증 메시지를 쏘므로 구독이 먼저다.
        graphView.ValidationMessageChanged += OnValidationMessageChanged;
        graphView.Bind(currentAsset);

        RefreshAssetLabel();
    }

    private void OnValidationMessageChanged(string message)
    {
        validationLabel.text = message;
    }

    private void OnDisable()
    {
        SaveAsset();
    }

    private void SetAsset(BehaviorTreeAsset asset)
    {
        currentAsset = asset;

        // CreateGUI가 아직 안 돌았으면 거기서 Bind 한다.
        graphView?.Bind(asset);
        RefreshAssetLabel();
    }

    private void SaveAsset()
    {
        if (currentAsset == null)
        {
            return;
        }

        AssetDatabase.SaveAssets();
    }

    private void RefreshAssetLabel()
    {
        // CreateGUI 전에 SetAsset이 먼저 올 수 있다.
        if (assetNameLabel == null)
        {
            return;
        }

        assetNameLabel.text = currentAsset == null ? "(에셋 없음)" : currentAsset.name;
    }
}
