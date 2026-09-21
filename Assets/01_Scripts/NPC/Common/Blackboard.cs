using System.Collections.Generic;

public sealed class Blackboard
{
    private readonly Dictionary<string, object> values = new Dictionary<string, object>();

    // 단순 키 값 저장 (덮어 씌움)
    public void Set<T>(string key, T value)
    {
        values[key] = value;
    }

    // 주어진 키 값을 꺼내 내가 원하는 타입인지 확인 (예외를 던지지 않는게 핵심)
    public bool TryGet<T>(string key, out T value)
    {
        if(values.TryGetValue(key, out object raw) && raw is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    //값의 내용이나 타입은 신경 쓰지 않고, 그 키에 뭔가 저장되어 있는지만 확인한다 (Selector 에서 사용)
    public bool Has(string key)
    {
        return values.ContainsKey(key);
    }

    public void Clear()
    {
        values.Clear();
    }
}
