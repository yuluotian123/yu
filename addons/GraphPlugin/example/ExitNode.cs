using System.Collections.Generic;
using Godot;

/// <summary>
/// 出口节点 - 状态机的结束点
/// </summary>
/// 
public enum ExitMode
{
    Success,
    Fail
}

public partial class ExitNode : GraphNodeData
{
    public ExitMode exitMode { get; set; } = ExitMode.Success;
    public override string GetDisplayName() => "出口";
    public override Color GetNodeColor() => Colors.Red;
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 0;
    public override bool CanBePrime() => false;
    public override void CreateUI(GraphEditorContext context)
    {
        var vbox = new VBoxContainer();

        var label = new Label { Text = "�?结束" };
        vbox.AddChild(label);

        var option = new OptionButton();
        option.AddItem("成功", (int)ExitMode.Success);
        option.AddItem("失败", (int)ExitMode.Fail);
        option.Selected = (int)exitMode;
        option.ItemSelected += (idx) => exitMode = (ExitMode)idx;
        vbox.AddChild(option);

        context.GraphNode.AddChild(vbox);
    }
}
