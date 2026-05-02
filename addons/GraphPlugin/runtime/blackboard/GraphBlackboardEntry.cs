using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// 黑板中的一条键值数据。
/// </summary>
public sealed class GraphBlackboardEntry
{
    /// <summary>黑板键名。运行时按这个字符串读写值。</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>给设计者看的说明文字，不参与运行时逻辑。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>可序列化的黑板值对象。</summary>
    public GraphBlackboardValue Value { get; set; } = new GraphStringBlackboardValue();

    /// <summary>尝试把当前值转换为指定类型。</summary>
    public bool TryGetValue<T>(out T value)
    {
        if (Value != null)
            return Value.TryGetValue(out value);

        value = default;
        return false;
    }

    /// <summary>设置当前值；如果值类型不匹配，会替换为新的黑板值对象。</summary>
    public bool SetValue<T>(T value)
    {
        if (Value == null || !Value.SetValue(value))
            Value = GraphBlackboardValueFactory.CreateForValue(value);

        return true;
    }

    /// <summary>深拷贝条目，用于运行时本地黑板栈隔离。</summary>
    public GraphBlackboardEntry Clone()
    {
        return GraphJsonHelper.Deserialize<GraphBlackboardEntry>(GraphJsonHelper.Serialize(this));
    }
}

/// <summary>
/// 根据普通 C# 值创建合适的黑板值对象。
/// </summary>
public static class GraphBlackboardValueFactory
{
    /// <summary>根据泛型值创建黑板值。</summary>
    public static GraphBlackboardValue CreateForValue<T>(T value)
    {
        return CreateForObject(value);
    }

    /// <summary>根据运行时对象创建黑板值。</summary>
    public static GraphBlackboardValue CreateForObject(object value)
    {
        return value switch
        {
            GraphBlackboardValue blackboardValue => blackboardValue.Clone(),
            bool boolValue => new GraphBoolBlackboardValue { Value = boolValue },
            int intValue => new GraphIntBlackboardValue { Value = intValue },
            float floatValue => new GraphFloatBlackboardValue { Value = floatValue },
            double doubleValue => new GraphFloatBlackboardValue { Value = (float)doubleValue },
            string stringValue => new GraphStringBlackboardValue { Value = stringValue },
            Vector2 vector2Value => new GraphVector2BlackboardValue { Value = vector2Value },
            Color colorValue => new GraphColorBlackboardValue { Value = colorValue },
            _ => new GraphStringBlackboardValue { Value = value?.ToString() ?? string.Empty }
        };
    }
}

/// <summary>
/// 黑板条目结构验证工具。
/// </summary>
public static class GraphBlackboardValidator
{
    /// <summary>验证黑板 key 是否为空、重复，以及 value 是否存在。</summary>
    public static bool TryValidate(IList<GraphBlackboardEntry> entries, out string error)
    {
        if (entries == null)
        {
            error = string.Empty;
            return true;
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < entries.Count; i++)
        {
            GraphBlackboardEntry entry = entries[i];
            if (entry == null)
            {
                error = $"黑板条目第 {i} 项为空。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                error = $"黑板条目第 {i} 项的 key 为空。";
                return false;
            }

            if (!keys.Add(entry.Key))
            {
                error = $"黑板 key 重复：{entry.Key}。";
                return false;
            }

            if (entry.Value == null)
            {
                error = $"黑板 key {entry.Key} 没有值对象。";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    /// <summary>按 key 查找黑板条目。</summary>
    public static GraphBlackboardEntry FindEntry(IList<GraphBlackboardEntry> entries, string key)
    {
        if (entries == null || string.IsNullOrWhiteSpace(key))
            return null;

        for (int i = 0; i < entries.Count; i++)
        {
            GraphBlackboardEntry entry = entries[i];
            if (entry != null && string.Equals(entry.Key, key, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }

    /// <summary>深拷贝黑板条目列表。</summary>
    public static List<GraphBlackboardEntry> CloneEntries(IList<GraphBlackboardEntry> entries)
    {
        var results = new List<GraphBlackboardEntry>();
        if (entries == null)
            return results;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null)
                results.Add(entries[i].Clone());
        }

        return results;
    }
}
