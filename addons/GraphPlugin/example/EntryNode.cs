using System.Collections.Generic;
using Godot;

/// <summary>
/// Entry node. Usually the starting point of a graph.
/// </summary>
public partial class EntryNode : GraphNodeData
{
    public override string GetDisplayName() => "Entry";
    public override Color GetNodeColor() => Colors.LimeGreen;
    public override int GetInputCount() => 0;
    public override int GetOutputCount() => 1;

    public override void CreateUI(GraphEditorContext context)
    {
        var label = new Label { Text = "Start" };
        context.GraphNode.AddChild(label);
    }
}
