#if TOOLS
using Godot;
using Godot.Collections;

// Presents graph settings to the native Inspector without exposing serialized graph JSON.
[Tool]
public partial class GraphSettingsInspectorObject : RefCounted
{
    private GraphAsset _graph;
    public void Bind(GraphAsset graph) => _graph = graph;
    public GraphAsset Graph => _graph;

    public override Array<Dictionary> _GetPropertyList() => new()
    {
        Property("Graph/Name", Variant.Type.String),
        Property("Graph/Type", Variant.Type.String, readOnly: true),
        Property("Graph/Action Dependencies", Variant.Type.Int, PropertyHint.Enum, "HostBound,Reusable"),
        Property("Graph/Nodes", Variant.Type.Int, readOnly: true),
        Property("Graph/Connections", Variant.Type.Int, readOnly: true)
    };
    private static Dictionary Property(string name, Variant.Type type, PropertyHint hint = PropertyHint.None,
        string hintString = "", bool readOnly = false) => new()
    {
        ["name"] = name, ["type"] = (int)type, ["hint"] = (int)hint, ["hint_string"] = hintString,
        ["usage"] = (int)(PropertyUsageFlags.Editor | (readOnly ? PropertyUsageFlags.ReadOnly : PropertyUsageFlags.None))
    };
    public override Variant _Get(StringName property)
    {
        if (!GodotObject.IsInstanceValid(_graph)) return default;
        return property.ToString() switch
        {
            "Graph/Name" => _graph.ResourceName,
            "Graph/Type" => _graph.GraphType,
            "Graph/Action Dependencies" => (int)_graph.ActionDependencyMode,
            "Graph/Nodes" => _graph.Nodes.Count,
            "Graph/Connections" => _graph.Connections.Count,
            _ => default
        };
    }
    public override bool _Set(StringName property, Variant value)
    {
        if (!GodotObject.IsInstanceValid(_graph)) return false;
        switch (property.ToString())
        {
            case "Graph/Name": _graph.ResourceName = value.AsString(); break;
            case "Graph/Action Dependencies": _graph.ActionDependencyMode = (GraphActionDependencyMode)value.AsInt32(); break;
            default: return false;
        }
        _graph.MarkDirty();
        return true;
    }
}
#endif
