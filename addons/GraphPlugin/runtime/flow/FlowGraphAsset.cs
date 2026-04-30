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
        return GraphNodeFactory
            .GetNodesForGraphType(GraphTypeName)
            .ToList();
    }

    public override string GetEditorTitle() => "FlowGraph Editor";
}
