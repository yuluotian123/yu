using System.Collections.Generic;
using Godot;

public class FlowReturnNodeData : GraphNodeData, IFlowNode
{
    public string Label { get; set; } = "Finished";

    public override List<string> GetGraphTypes() => new() { FlowGraphAsset.GraphTypeName };
    public override string GetDisplayName() => string.IsNullOrWhiteSpace(Label) ? "Return" : $"Return: {Label}";
    public override Color GetNodeColor() => new(0.66f, 0.56f, 0.92f);
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;
    public override bool CanBePrime() => false;
    public override string GetOutputPortName(int port) => "Return";

    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context) { }
    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }
    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        completion = NodeCompletion.Return(Label);
        return true;
    }

    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) { }

    public override void CreateNodeUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(150f, 0f) };
        root.AddChild(new Label
        {
            Text = string.IsNullOrWhiteSpace(Label) ? "Return" : Label,
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipText = true
        });
        context.GraphNode.AddChild(root);
    }

    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(240f, 0f) };
        root.AddThemeConstantOverride("separation", 6);
        root.AddChild(CreateLabelEdit(context));
        return root;
    }

    public override void CreateUI(GraphEditorContext context)
    {
        context.GraphNode.AddChild(CreateLabelEdit(context));
    }

    private LineEdit CreateLabelEdit(GraphEditorContext context)
    {
        var edit = new LineEdit
        {
            PlaceholderText = "Return label",
            Text = Label
        };
        edit.TextChanged += value =>
        {
            Label = value;
            if (context.GraphNode != null)
                context.GraphNode.Title = GetDisplayName();
        };
        return edit;
    }
}
