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

    public override void CreateUI(GraphEditorContext context)
    {
        var edit = new LineEdit
        {
            PlaceholderText = "Return label",
            Text = Label
        };
        edit.TextChanged += value => Label = value;
        context.GraphNode.AddChild(edit);
    }
}
