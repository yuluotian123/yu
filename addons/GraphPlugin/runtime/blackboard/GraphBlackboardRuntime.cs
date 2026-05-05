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
            DeclaredKeys = new HashSet<string>(Map.Keys, System.StringComparer.Ordinal);
        }

        private BlackboardFrame(IList<GraphBlackboardEntry> entries, HashSet<string> declaredKeys)
        {
            Entries = GraphBlackboardValidator.CloneEntries(entries);
            Map = BuildMap(Entries);
            DeclaredKeys = new HashSet<string>(declaredKeys, System.StringComparer.Ordinal);
        }

        public List<GraphBlackboardEntry> Entries { get; }
        public Dictionary<string, GraphBlackboardEntry> Map { get; }
        public HashSet<string> DeclaredKeys { get; }

        public BlackboardFrame Clone()
        {
            return new BlackboardFrame(Entries, DeclaredKeys);
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

    /// <summary>
    /// 写入黑板值。当前作用域已经声明过该 key 时会优先更新声明者；否则写入当前图的本地黑板。
    /// </summary>
    /// <remarks>
    /// 这个方法只处理单个 <see cref="GraphBlackboardRuntime"/> 内部的本地图、父图和全局黑板。
    /// 如果需要跨运行时子图查找声明者，请使用 <see cref="GraphRuntimeBlackboardWriter.SetValueRecursive{T}(IGraphRuntimeScope, string, T)"/>。
    /// </remarks>
    public bool SetValue<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (TrySetDeclaredValue(key, value))
            return true;

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

    /// <summary>
    /// 只更新已经声明过的 key，不创建新条目。
    /// </summary>
    /// <remarks>
    /// 子图参数归属调整后，外部系统可以先尝试写入声明该 key 的作用域。
    /// 这样 `IsOnFloor` 这类只在子图声明的参数不会被误创建到父图黑板里。
    /// </remarks>
    public bool TrySetDeclaredLocalValue<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        for (int i = _localStack.Count - 1; i >= 0; i--)
        {
            BlackboardFrame frame = _localStack[i];
            if (!frame.DeclaredKeys.Contains(key))
                continue;

            if (!frame.Map.TryGetValue(key, out GraphBlackboardEntry entry))
                continue;

            entry.SetValue(value);
            return true;
        }

        return false;
    }

    public bool TrySetDeclaredValue<T>(string key, T value)
    {
        if (TrySetDeclaredLocalValue(key, value))
            return true;

        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (_globalMap.TryGetValue(key, out GraphBlackboardEntry globalEntry))
        {
            globalEntry.SetValue(value);
            return true;
        }

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
