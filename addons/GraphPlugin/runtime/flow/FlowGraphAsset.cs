using System.Collections.Generic;
using System.Linq;
using Godot;

[Tool]
[GlobalClass]
public partial class FlowGraphAsset : GraphAsset
{
    public const string GraphTypeName = "FlowGraph";

    public override string GraphType
    {
        get => GraphTypeName;
        set { }
    }

    public override List<string> GetAllowedNodeTypes()
    {
        return GraphTypeRegistry
            .GetNodeTypeNamesForGraphType(GraphTypeName)
            .ToList();
    }

    public override GraphConnection CreateConnection() => new FlowConnection();

    public override string GetEditorTitle() => "FlowGraph Editor";
}
