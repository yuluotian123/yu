using System.Collections.Generic;
using Godot;

[Tool]
[GlobalClass]
public partial class GraphBlackboardNode : Node
{
    private static readonly List<GraphBlackboardNode> _registeredNodes = new();

    private string _blackboardJson = "[]";
    private List<GraphBlackboardEntry> _entries;

    [Export(PropertyHint.MultilineText)]
    public string BlackboardJson
    {
        get => _blackboardJson;
        set
        {
            _blackboardJson = string.IsNullOrWhiteSpace(value) ? "[]" : value;
            _entries = null;
        }
    }

    public static GraphBlackboardNode Current
    {
        get
        {
            for (int i = 0; i < _registeredNodes.Count; i++)
            {
                if (GodotObject.IsInstanceValid(_registeredNodes[i]))
                    return _registeredNodes[i];
            }

            return null;
        }
    }

    public List<GraphBlackboardEntry> Entries
    {
        get
        {
            if (_entries == null)
                LoadFromJson();

            return _entries;
        }
        set
        {
            _entries = value ?? new List<GraphBlackboardEntry>();
            SaveToJson();
        }
    }

    public override void _EnterTree()
    {
        if (!_registeredNodes.Contains(this))
            _registeredNodes.Add(this);

        if (_registeredNodes.Count > 1)
            GD.PushWarning("[GraphBlackboardNode] More than one global graph blackboard exists in the current tree. The first registered node will be used by default.");
    }

    public override void _ExitTree()
    {
        _registeredNodes.Remove(this);
    }

    public void SaveToJson()
    {
        if (_entries == null)
            _entries = GraphJsonHelper.DeserializeList<GraphBlackboardEntry>(BlackboardJson);

        _blackboardJson = GraphJsonHelper.SerializeList(_entries ?? new List<GraphBlackboardEntry>());
    }

    public void LoadFromJson()
    {
        _entries = GraphJsonHelper.DeserializeList<GraphBlackboardEntry>(BlackboardJson);
    }

    public bool HasKey(string key)
    {
        return GraphBlackboardValidator.FindEntry(Entries, key) != null;
    }

    public bool RemoveKey(string key)
    {
        GraphBlackboardEntry entry = GraphBlackboardValidator.FindEntry(Entries, key);
        if (entry == null)
            return false;

        Entries.Remove(entry);
        return true;
    }

    public bool TryGetValue<T>(string key, out T value)
    {
        GraphBlackboardEntry entry = GraphBlackboardValidator.FindEntry(Entries, key);
        if (entry != null)
            return entry.TryGetValue(out value);

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

        GraphBlackboardEntry entry = GraphBlackboardValidator.FindEntry(Entries, key);
        if (entry == null)
        {
            entry = new GraphBlackboardEntry
            {
                Key = key,
                Value = GraphBlackboardValueFactory.CreateForValue(value)
            };
            Entries.Add(entry);
        }
        else
        {
            entry.SetValue(value);
        }

        return true;
    }
}
