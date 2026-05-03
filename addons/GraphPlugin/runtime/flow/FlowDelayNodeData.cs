using System.Collections.Generic;
using Godot;

public class FlowDelayNodeData : GraphNodeData, IFlowNode
{
    public float Seconds { get; set; } = 1f;

    public override List<string> GetGraphTypes() => new() { FlowGraphAsset.GraphTypeName };
    public override string GetDisplayName() => $"Delay {Seconds:0.##}s";
    public override Color GetNodeColor() => new(0.52f, 0.62f, 0.9f);
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;
    public override bool CanBePrime() => false;
    public override string GetOutputPortName(int port) => "Completed";

    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        runtime.SetNodeData(Id, new DelayRuntimeData());
    }

    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta)
    {
        var data = runtime.GetNodeData<DelayRuntimeData>(Id);
        data.Elapsed += (float)delta;
    }

    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        if (Seconds <= 0f ||
            runtime.TryGetNodeData<DelayRuntimeData>(Id, out var data) &&
            data.Elapsed >= Seconds)
        {
            completion = NodeCompletion.Completed();
            return true;
        }

        completion = default;
        return false;
    }

    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        runtime.ClearNodeData(Id);
    }

    public override void CreateNodeUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(140f, 0f) };
        root.AddChild(new Label
        {
            Text = $"{Seconds:0.##}s",
            HorizontalAlignment = HorizontalAlignment.Center
        });
        context.GraphNode.AddChild(root);
    }

    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(240f, 0f) };
        root.AddThemeConstantOverride("separation", 6);
        root.AddChild(new Label { Text = "Delay" });
        root.AddChild(CreateSecondsSpin(context));
        return root;
    }

    public override void CreateUI(GraphEditorContext context)
    {
        context.GraphNode.AddChild(CreateSecondsSpin(context));
    }

    private SpinBox CreateSecondsSpin(GraphEditorContext context)
    {
        var spin = new SpinBox
        {
            MinValue = 0,
            MaxValue = 999999,
            Step = 0.05,
            Value = Seconds
        };
        spin.ValueChanged += value =>
        {
            Seconds = (float)value;
            if (context.GraphNode != null)
                context.GraphNode.Title = GetDisplayName();
        };
        return spin;
    }

    private sealed class DelayRuntimeData
    {
        public float Elapsed;
    }
}
