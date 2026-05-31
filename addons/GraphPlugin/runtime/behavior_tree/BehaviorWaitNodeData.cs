using Godot;

public class BehaviorWaitNodeData : BehaviorTreeNodeData
{
    public float Seconds { get; set; } = 1f;

    public override string GetDisplayName() => "Wait";
    public override string GetCategory() => "BehaviorTree/Leaf";
    public override Color GetNodeColor() => new(0.78f, 0.78f, 0.46f);

    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        if (Seconds <= 0f)
            return BehaviorTreeStatus.Success;

        BehaviorTreeWaitRuntimeData data = runtime.GetNodeData<BehaviorTreeWaitRuntimeData>(Id);
        data.Elapsed += delta;
        if (data.Elapsed < Seconds)
            return BehaviorTreeStatus.Running;

        runtime.ClearNodeData(Id);
        return BehaviorTreeStatus.Success;
    }

#if TOOLS
    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        root.AddChild(BehaviorTreeEditorUi.BuildSpinRow("Seconds", Seconds, 0, 999999, 0.05, value => Seconds = (float)value));
        return root;
    }
#endif
}
