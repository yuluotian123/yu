using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

[Tool]
[GlobalClass]
public partial class MissionGraph : FlowGraphAsset
{
    public new const string GraphTypeName = "MissionGraph";

    public override string GraphType
    {
        get => GraphTypeName;
        set { }
    }

    public override string GetEditorTitle() => ResourcePath + "_" + GraphType + "编辑器";

    public override GraphConnection CreateConnection() => new FlowConnection();

    public override List<string> GetAllowedNodeTypes()
    {
        var result = GraphTypeRegistry
            .GetNodeTypeNamesForGraphType(GraphTypeName)
            .ToList();

        AddIfRegistered(result, nameof(FlowEntryNodeData));
        AddIfRegistered(result, nameof(FlowActionNodeData));
        return result.Distinct(StringComparer.Ordinal).ToList();
    }

    private static void AddIfRegistered(List<string> result, string nodeType)
    {
        if (GraphTypeRegistry.TryGetNodeDefinition(nodeType, out _) && !result.Contains(nodeType))
            result.Add(nodeType);
    }
}
