using System.Collections.Generic;

public static class BehaviorNodeTypes
{
    // 저장된 트리는 이 문자열로 노드 종류를 식별한다.
    // 클래스 이름을 바꿔도 에셋이 깨지지 않도록 타입 이름 대신 고정 상수를 쓴다.
    public const string ROOT = "Root";
    public const string SEQUENCE = "Sequence";
    public const string SELECTOR = "Selector";
    public const string INVERTER = "Inverter";
    public const string LOG = "Log";
    public const string WAIT = "Wait";
    public const string WANDER = "Wander";

    // Leaf를 추가할 때 고치는 곳은 여기 하나다.
    // 항목 하나 + 같은 NodeTypeId를 내놓는 IBehaviorAction 컴포넌트 하나면 끝이고,
    // 에디터 메뉴·포트·팩토리는 손대지 않는다.
    private static readonly BehaviorNodeTypeInfo[] types =
    {
        new BehaviorNodeTypeInfo(ROOT,     "Root",     BehaviorNodeCategory.Root),
        new BehaviorNodeTypeInfo(SEQUENCE, "Sequence", BehaviorNodeCategory.Composite),
        new BehaviorNodeTypeInfo(SELECTOR, "Selector", BehaviorNodeCategory.Composite),
        new BehaviorNodeTypeInfo(INVERTER, "Inverter", BehaviorNodeCategory.Decorator),
        new BehaviorNodeTypeInfo(LOG,      "Log",      BehaviorNodeCategory.Leaf),
        new BehaviorNodeTypeInfo(WAIT,     "Wait",     BehaviorNodeCategory.Leaf),
        new BehaviorNodeTypeInfo(WANDER,   "Wander",   BehaviorNodeCategory.Leaf)
    };

    public static IReadOnlyList<BehaviorNodeTypeInfo> All => types;

    public static bool TryGet(string nodeTypeId, out BehaviorNodeTypeInfo info)
    {
        foreach (BehaviorNodeTypeInfo candidate in types)
        {
            if (candidate.Id == nodeTypeId)
            {
                info = candidate;
                return true;
            }
        }

        info = null;
        return false;
    }
}
