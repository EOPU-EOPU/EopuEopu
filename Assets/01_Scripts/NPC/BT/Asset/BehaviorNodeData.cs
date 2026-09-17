using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
//노드 한 개가 파일에 저장되는 데이터
public class BehaviorNodeData
{
    [SerializeField] private string id;
    [SerializeField] private string nodeTypeId;
    [SerializeField] private Vector2 editorPosition;
    [SerializeField] private List<string> childIds = new List<string>();

    // Unity 역직렬화가 쓰는 생성자. 직접 호출하지 말 것.
    public BehaviorNodeData()
    {
    }

    public BehaviorNodeData(string nodeId, string typeId, Vector2 position)
    {
        id = nodeId;
        nodeTypeId = typeId;
        editorPosition = position;
    }

    public string Id => id;

    public string NodeTypeId => nodeTypeId;

    // 에디터 상에 위치를 저장하고 불러오기 위한 포지션 (순수하게 에디터 표시용),
    // 예외로 에디터에서 자식 노드중에 왼쪽부터 실행하기 때문에 X좌표 순으로 정렬하여 사용한다. (완전히 장식용은 아님)
    public Vector2 EditorPosition
    {
        get => editorPosition;
        set => editorPosition = value;
    }

    public IReadOnlyList<string> ChildIds => childIds;

    public void ClearChildren()
    {
        childIds.Clear();
    }

    public void AddChild(string childId)
    {
        childIds.Add(childId);
    }
}
