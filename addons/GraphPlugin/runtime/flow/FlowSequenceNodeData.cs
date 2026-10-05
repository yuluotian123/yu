using System.Collections.Generic;
using System.Linq;
using Godot;

public sealed class FlowSequenceNodeData : GraphNodeData, IFlowNode
{
    public int Outputs { get; set; } = 2;
    public override List<string> GetGraphTypes() => new() { FlowGraphAsset.GraphTypeName, GameLogic.CharacterGraphAsset.CharacterGraphTypeName };
    public override string GetMenuName() => "顺序触发（Sequence）";
    public override string GetDisplayName() => GetMenuName();
    public override string GetCategory() => "流程";
    public override Color GetNodeColor() => new(0.52f, 0.62f, 0.9f);
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => Mathf.Clamp(Outputs, 1, 8);
    public override string GetOutputPortName(int port) => $"Then {port}";
    public override bool CanBePrime() => false;
    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        // Start branches in order; a running branch does not delay the next branch.
        for (int i = 0; i < GetOutputCount(); i++) runtime.PropagateFromOutput(Id, i);
    }
    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }
    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    { completion = new NodeCompletion(-1); return true; }
    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) { }
    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        root.AddChild(new Label { Text = "依次启动各分支，不等待分支完成。" });
        var count = new SpinBox { MinValue = 1, MaxValue = 8, Step = 1, Value = Outputs };
        count.ValueChanged += value =>
        {
            int connected = context?.CurrentGraph?.GetOutgoingConnections(Id).Select(c => c.FromPort + 1).DefaultIfEmpty(1).Max() ?? 1;
            Outputs = Mathf.Clamp(Mathf.Max((int)value, connected), 1, 8);
            count.SetValueNoSignal(Outputs);
            context?.CurrentGraph?.MarkDirty();
#if TOOLS
            Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(context?.GraphNode) && !context.GraphNode.IsQueuedForDeletion())
                    GraphNodeViewBuilder.RefreshNodeUI(this, context.GraphNode, context);
            }).CallDeferred();
#endif
        };
        root.AddChild(count); return root;
    }
}
