using System.Collections.Generic;
using Godot;

/// <summary>
/// Exit node. Usually the ending point of a graph.
/// </summary>
public enum ExitMode
{
    Success,
    Fail
}

public partial class ExitNode : GraphNodeData
{
    public ExitMode exitMode { get; set; } = ExitMode.Success;

    public override string GetDisplayName() => "Exit";
    public override Color GetNodeColor() => Colors.Red;
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 0;
    public override bool CanBePrime() => false;

    public override void CreateUI(GraphEditorContext context)
    {
        var vbox = new VBoxContainer();

        var label = new Label { Text = "End" };
        vbox.AddChild(label);

        var option = new OptionButton();
        option.AddItem("Success", (int)ExitMode.Success);
        option.AddItem("Fail", (int)ExitMode.Fail);
        option.Selected = (int)exitMode;
        option.ItemSelected += idx => exitMode = (ExitMode)idx;
        vbox.AddChild(option);

        context.GraphNode.AddChild(vbox);
    }
}
