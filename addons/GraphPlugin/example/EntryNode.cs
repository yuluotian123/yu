using System.Collections.Generic;
using Godot;

/// <summary>
/// 入口节点 - 状态机的起始点
/// </summary>
public partial class EntryNode : GraphNodeData
{ 
    public override string GetDisplayName() => "入口";
    public override Color GetNodeColor() => Colors.LimeGreen;
    public override int GetInputCount() => 0;
    public override int GetOutputCount() => 1;

    public override void CreateUI(GraphNode node)
    {
        var label = new Label { Text = "▶ 开始" };
        node.AddChild(label);
    }
}
