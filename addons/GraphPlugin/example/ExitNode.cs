using System.Collections.Generic;
using Godot;

/// <summary>
/// 出口节点 - 状态机的结束点
/// </summary>
[Tool]
public partial class ExitNode : GraphNodeData
{
    public ExitNode()
    {
        NodeType = "Exit";
    }

    public override string GetDisplayName() => "出口";
    public override Color GetNodeColor() => Colors.Red;
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 0;

    public override void CreateUI(GraphNode node)
    {
        var label = new Label { Text = "■ 结束" };
        node.AddChild(label);
    }
}
