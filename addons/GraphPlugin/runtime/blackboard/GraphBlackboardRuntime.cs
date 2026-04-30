using System.Collections.Generic;
using Godot;

public sealed class GraphBlackboardRuntime
{
    private sealed class BlackboardFrame
    {
        public BlackboardFrame(IList<GraphBlackboardEntry> entries)
        {
            Entries = GraphBlackboardValidator.CloneEntries(entries);
            Map = BuildMap(Entries);
        }

        public List<GraphBlackboardEntry> Entries { get; }
        public Dictionary<string, GraphBlackboardEntry> Map { get; }

        public BlackboardFrame Clone()
        {
            return new BlackboardFrame(Entries);
        }
    }

    private readonly List<BlackboardFrame> _localStack = new();
    private readonly Dictionary<string, GraphBlackboardEntry> _globalMap;

    public GraphBlackboardRuntime(GraphBlackboardNode globalBlackboard = null)
    {
        GlobalBlackboard = globalBlackboard ?? GraphBlackboardNode.Current;
        _globalMap = GlobalBlackboard == null
            ? new Dictionary<string, GraphBlackboardEntry>(System.StringComparer.Ordinal)
            : BuildMap(GlobalBlackboard.Entries);

        if (GlobalBlackboard == null)
            GD.PushWarning("[GraphBlackboardRuntime] No GraphBlackboardNode was found. Global blackboard reads will use defaults.");
    }

    public GraphBlackboardNode GlobalBlackboard { get; }
    public int LocalDepth => _localStack.Count;

    public GraphBlackboardRuntime Fork()
    {
        var runtime = new GraphBlackboardRuntime(GlobalBlackboard);
        for (int i = 0; i < _localStack.Count; i++)
            runtime._localStack.Add(_localStack[i].Clone());

        return runtime;
    }

    public GraphBlackboardRuntime ForkSharedLocals()
    {
        var runtime = new GraphBlackboardRuntime(GlobalBlackboard);
        for (int i = 0; i < _localStack.Count; i++)
            runtime._localStack.Add(_localStack[i]);

        return runtime;
    }

    public void PushLocal(GraphAsset graph)
    {
        _localStack.Add(new BlackboardFrame(graph?.BlackboardEntries));
    }

    public bool PopLocal()
    {
        if (_localStack.Count <= 0)
            return false;

        _localStack.RemoveAt(_localStack.Count - 1);
        return true;
    }

    public bool HasKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        for (int i = _localStack.Count - 1; i >= 0; i--)
        {
            if (_localStack[i].Map.ContainsKey(key))
                return true;
        }

        return _globalMap.ContainsKey(key);
    }

    public bool TryGetValue<T>(string key, out T value)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            for (int i = _localStack.Count - 1; i >= 0; i--)
            {
                if (_localStack[i].Map.TryGetValue(key, out GraphBlackboardEntry entry))
                    return entry.TryGetValue(out value);
            }

            if (_globalMap.TryGetValue(key, out GraphBlackboardEntry globalEntry))
                return globalEntry.TryGetValue(out value);
        }

        value = default;
        return false;
    }

    public T GetValue<T>(string key, T defaultValue = default)
    {
        return TryGetValue(key, out T value) ? value : defaultValue;
    }

    public bool SetValue<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (_localStack.Count > 0)
        {
            SetEntryValue(_localStack[_localStack.Count - 1], key, value);
            return true;
        }

        if (GlobalBlackboard != null)
        {
            bool success = GlobalBlackboard.SetValue(key, value);
            if (success)
                SetEntryValue(_globalMap, key, value);

            return success;
        }

        GD.PushWarning($"[GraphBlackboardRuntime] Can not set '{key}' because there is no local graph blackboard or global GraphBlackboardNode.");
        return false;
    }

    public bool SetGlobalValue<T>(string key, T value)
    {
        if (GlobalBlackboard == null)
        {
            GD.PushWarning($"[GraphBlackboardRuntime] Can not set global key '{key}' because there is no GraphBlackboardNode.");
            return false;
        }

        bool success = GlobalBlackboard.SetValue(key, value);
        if (success)
            SetEntryValue(_globalMap, key, value);

        return success;
    }

    private static Dictionary<string, GraphBlackboardEntry> BuildMap(IList<GraphBlackboardEntry> entries)
    {
        var map = new Dictionary<string, GraphBlackboardEntry>(System.StringComparer.Ordinal);
        if (entries == null)
            return map;

        for (int i = 0; i < entries.Count; i++)
        {
            GraphBlackboardEntry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                continue;

            map[entry.Key] = entry;
        }

        return map;
    }

    private static void SetEntryValue<T>(BlackboardFrame frame, string key, T value)
    {
        SetEntryValue(frame.Map, frame.Entries, key, value);
    }

    private static void SetEntryValue<T>(IDictionary<string, GraphBlackboardEntry> map, string key, T value)
    {
        SetEntryValue(map, null, key, value);
    }

    private static void SetEntryValue<T>(IDictionary<string, GraphBlackboardEntry> map, IList<GraphBlackboardEntry> entries, string key, T value)
    {
        map.TryGetValue(key, out GraphBlackboardEntry entry);
        if (entry == null)
        {
            entry = new GraphBlackboardEntry
            {
                Key = key,
                Value = GraphBlackboardValueFactory.CreateForValue(value)
            };
            entries?.Add(entry);
            map[key] = entry;
            return;
        }

        entry.SetValue(value);
    }
}
