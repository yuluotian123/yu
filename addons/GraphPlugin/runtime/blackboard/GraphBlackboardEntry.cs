using System;
using System.Collections.Generic;
using Godot;

public sealed class GraphBlackboardEntry
{
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public GraphBlackboardValue Value { get; set; } = new GraphStringBlackboardValue();

    public bool TryGetValue<T>(out T value)
    {
        if (Value != null)
            return Value.TryGetValue(out value);

        value = default;
        return false;
    }

    public bool SetValue<T>(T value)
    {
        if (Value == null || !Value.SetValue(value))
            Value = GraphBlackboardValueFactory.CreateForValue(value);

        return true;
    }

    public GraphBlackboardEntry Clone()
    {
        return GraphJsonHelper.Deserialize<GraphBlackboardEntry>(GraphJsonHelper.Serialize(this));
    }
}

public static class GraphBlackboardValueFactory
{
    public static GraphBlackboardValue CreateForValue<T>(T value)
    {
        return CreateForObject(value);
    }

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

public static class GraphBlackboardValidator
{
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
                error = $"Blackboard entry {i} is invalid.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                error = $"Blackboard entry {i} has an empty key.";
                return false;
            }

            if (!keys.Add(entry.Key))
            {
                error = $"Blackboard key '{entry.Key}' is duplicated.";
                return false;
            }

            if (entry.Value == null)
            {
                error = $"Blackboard key '{entry.Key}' has no value.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

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
