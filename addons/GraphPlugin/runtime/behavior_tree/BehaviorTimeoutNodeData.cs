using Godot;

public sealed class BehaviorTimeoutNodeData : BehaviorDecoratorNodeData
{
    public float Seconds { get; set; } = 5;
    public override string GetDisplayName() => "Timeout";
    public override void Validate(GraphAsset graph, GraphValidationResult result)
    {
        if (!float.IsFinite(Seconds) || Seconds <= 0) result.AddError("Timeout Seconds must be positive.", Id);
    }
    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        var child = GetChild(runtime);
        if (child == null) return BehaviorTreeStatus.Failure;
        var state = runtime.GetNodeData<BehaviorTreeWaitRuntimeData>(Id);
        state.Elapsed += System.Math.Max(0, delta);
        if (state.Elapsed >= Seconds)
        {
            runtime.AbortSubtree(child);
            runtime.ClearNodeData(Id);
            return BehaviorTreeStatus.Failure;
        }
        var status = runtime.TickNode(child, delta);
        if (status != BehaviorTreeStatus.Running) runtime.ClearNodeData(Id);
        return status;
    }
#if TOOLS
    public override Control CreateInspectorUI(GraphEditorContext context) =>
        BehaviorTreeEditorUi.BuildSpinRow("Seconds", Seconds, 0.01, 999999, 0.05,
            value => { Seconds = (float)value; context.CurrentGraph?.MarkDirty(); });
#endif
}
