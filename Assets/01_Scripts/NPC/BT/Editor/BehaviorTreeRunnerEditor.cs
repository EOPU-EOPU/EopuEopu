using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BehaviorTreeRunner))]
public class BehaviorTreeRunnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var runner = (BehaviorTreeRunner)target;
        if(!Application.isPlaying || runner.Tree == null)
        {
            return;
        }

        var path = BehaviorTreeDebugPath.Build(runner.Tree, runner.Context);
        EditorGUILayout.LabelField("실행경로", string.Join(" > ", path));
        Repaint();
    }
}
