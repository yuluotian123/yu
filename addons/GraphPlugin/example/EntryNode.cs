using System.Collections.Generic;
using Godot;

/// <summary>
/// 鍏ュ彛鑺傜偣 - 鐘舵€佹満鐨勮捣濮嬬偣
/// </summary>
public partial class EntryNode : GraphNodeData
{ 
    public override string GetDisplayName() => "鍏ュ彛";
    public override Color GetNodeColor() => Colors.LimeGreen;
    public override int GetInputCount() => 0;
    public override int GetOutputCount() => 1;

    public override void CreateUI(GraphEditorContext context)
    {
        var label = new Label { Text = "Start" };
        context.GraphNode.AddChild(label);
    }
}
