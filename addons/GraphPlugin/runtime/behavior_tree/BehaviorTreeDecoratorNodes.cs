using System.Collections.Generic;
using Godot;

public abstract class BehaviorDecoratorNodeData : BehaviorTreeNodeData
{
    public override int GetOutputCount() => 1;
    public override int GetOutputMaxConnections(int port) => 1;
    public override string GetInputPortName(int port) => "Parent";
    public override string GetOutputPortName(int port) => "Child";
    public override string GetCategory() => "BehaviorTree/Decorator";
    public override Color GetNodeColor() => new(0.75f, 0.55f, 0.96f);

    protected BehaviorTreeNodeData GetChild(BehaviorTreeRuntime runtime)
    {
        List<BehaviorTreeNodeData> children = runtime.GetChildren(this);
        return children.Count > 0 ? children[0] : null;
    }
}

public class BehaviorInverterNodeData : BehaviorDecoratorNodeData
{
    public override string GetDisplayName() => "Inverter";

    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        BehaviorTreeNodeData child = GetChild(runtime);
        if (child == null)
            return BehaviorTreeStatus.Failure;

        BehaviorTreeStatus status = runtime.TickNode(child, delta);
        return status switch
        {
            BehaviorTreeStatus.Success => BehaviorTreeStatus.Failure,
            BehaviorTreeStatus.Failure => BehaviorTreeStatus.Success,
            _ => BehaviorTreeStatus.Running
        };
    }
}

public class BehaviorForceSuccessNodeData : BehaviorDecoratorNodeData
{
    public override string GetDisplayName() => "Force Success";

    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        BehaviorTreeNodeData child = GetChild(runtime);
        if (child == null)
            return BehaviorTreeStatus.Success;

        BehaviorTreeStatus status = runtime.TickNode(child, delta);
        return status == BehaviorTreeStatus.Running ? BehaviorTreeStatus.Running : BehaviorTreeStatus.Success;
    }
}

public class BehaviorForceFailureNodeData : BehaviorDecoratorNodeData
{
    public override string GetDisplayName() => "Force Failure";

    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        BehaviorTreeNodeData child = GetChild(runtime);
        if (child == null)
            return BehaviorTreeStatus.Failure;

        BehaviorTreeStatus status = runtime.TickNode(child, delta);
        return status == BehaviorTreeStatus.Running ? BehaviorTreeStatus.Running : BehaviorTreeStatus.Failure;
    }
}

public class BehaviorCooldownNodeData : BehaviorDecoratorNodeData
{
    public float Seconds { get; set; } = 1f;
    public bool StartOnSuccessOnly { get; set; } = true;

    public override string GetDisplayName() => "Cooldown";

    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        BehaviorTreeCooldownRuntimeData data = runtime.GetNodeData<BehaviorTreeCooldownRuntimeData>(Id);
        data.Remaining = Mathf.Max(0f, (float)(data.Remaining - delta));
        if (data.Remaining > 0d)
            return BehaviorTreeStatus.Failure;

        BehaviorTreeNodeData child = GetChild(runtime);
        if (child == null)
            return BehaviorTreeStatus.Failure;

        BehaviorTreeStatus status = runtime.TickNode(child, delta);
        if (status != BehaviorTreeStatus.Running &&
            (!StartOnSuccessOnly || status == BehaviorTreeStatus.Success))
        {
            data.Remaining = Mathf.Max(0f, Seconds);
        }

        return status;
    }

#if TOOLS
    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        root.AddChild(BehaviorTreeEditorUi.BuildSpinRow("Seconds", Seconds, 0, 999999, 0.05, value => Seconds = (float)value));
        var successOnly = new CheckBox
        {
            Text = "Start on success only",
            ButtonPressed = StartOnSuccessOnly
        };
        successOnly.Toggled += value => StartOnSuccessOnly = value;
        root.AddChild(successOnly);
        return root;
    }
#endif
}
