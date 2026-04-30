using System.Collections.Generic;
using Godot;

public class FlowEntryNodeData : GraphNodeData, IFlowNode
{
    public override List<string> GetGraphTypes() => new() { FlowGraphAsset.GraphTypeName };
    public override string GetDisplayName() => "Entry";
    public override Color GetNodeColor() => Colors.LimeGreen;
    public override int GetInputCount() => 0;
    public override int GetOutputCount() => 1;
    public override string GetOutputPortName(int port) => "Next";

    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context) { }
    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }
    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        completion = NodeCompletion.Next();
        return true;
    }

    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) { }

    public override void CreateUI(GraphEditorContext context)
    {
        context.GraphNode.AddChild(new Label { Text = "Start" });
    }
}
