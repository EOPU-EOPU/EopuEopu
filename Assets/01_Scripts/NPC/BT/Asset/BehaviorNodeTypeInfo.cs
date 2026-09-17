public sealed class BehaviorNodeTypeInfo
{
    public BehaviorNodeTypeInfo(string id, string displayName, BehaviorNodeCategory category)
    {
        Id = id;
        DisplayName = displayName;
        Category = category;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public BehaviorNodeCategory Category { get; }
}
