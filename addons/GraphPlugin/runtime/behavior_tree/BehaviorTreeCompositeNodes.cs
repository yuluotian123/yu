using System.Collections.Generic;
using Godot;

public abstract class BehaviorCompositeNodeData : BehaviorTreeNodeData
{
    public override int GetOutputCount() => 1;
    public override string GetInputPortName(int port) => "Parent";
    public override string GetOutputPortName(int port) => "Children";
    public override string GetCategory() => "BehaviorTree/Composite";
    public override Color GetNodeColor() => new(0.42f, 0.64f, 0.96f);
}

public class BehaviorSelectorNodeData : BehaviorCompositeNodeData
{
    public override string GetDisplayName() => "Selector";

    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        return BehaviorTreeSelectorUtility.TickSelector(this, runtime, delta, memory: false, random: false, weighted: false);
    }
}

public class BehaviorMemorySelectorNodeData : BehaviorCompositeNodeData
{
    public override string GetDisplayName() => "Memory Selector";

    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        return BehaviorTreeSelectorUtility.TickSelector(this, runtime, delta, memory: true, random: false, weighted: false);
    }
}

public class BehaviorRandomSelectorNodeData : BehaviorCompositeNodeData
{
    public override string GetDisplayName() => "Random Selector";

    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        return BehaviorTreeSelectorUtility.TickSelector(this, runtime, delta, memory: true, random: true, weighted: false);
    }
}

public class BehaviorWeightedSelectorNodeData : BehaviorCompositeNodeData
{
    public override string GetDisplayName() => "Weighted Selector";

    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        return BehaviorTreeSelectorUtility.TickSelector(this, runtime, delta, memory: true, random: false, weighted: true);
    }
}

public class BehaviorSequenceNodeData : BehaviorCompositeNodeData
{
    public override string GetDisplayName() => "Sequence";

    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        return BehaviorTreeSequenceUtility.TickSequence(this, runtime, delta, memory: false);
    }
}

public class BehaviorMemorySequenceNodeData : BehaviorCompositeNodeData
{
    public override string GetDisplayName() => "Memory Sequence";

    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        return BehaviorTreeSequenceUtility.TickSequence(this, runtime, delta, memory: true);
    }
}

public class BehaviorParallelNodeData : BehaviorCompositeNodeData
{
    public BehaviorTreeParallelPolicy Policy { get; set; } = BehaviorTreeParallelPolicy.RequireAll;

    public override string GetDisplayName() => "Parallel";

    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        List<BehaviorTreeNodeData> children = runtime.GetChildren(this);
        if (children.Count == 0)
            return BehaviorTreeStatus.Success;

        int successCount = 0;
        int failureCount = 0;

        for (int i = 0; i < children.Count; i++)
        {
            BehaviorTreeStatus status = runtime.TickNode(children[i], delta);
            switch (status)
            {
                case BehaviorTreeStatus.Success:
                    successCount++;
                    break;
                case BehaviorTreeStatus.Failure:
                    failureCount++;
                    break;
            }
        }

        BehaviorTreeStatus result;
        if (Policy == BehaviorTreeParallelPolicy.RequireOne)
            result = successCount > 0
                ? BehaviorTreeStatus.Success
                : failureCount == children.Count ? BehaviorTreeStatus.Failure : BehaviorTreeStatus.Running;
        else
            result = failureCount > 0
                ? BehaviorTreeStatus.Failure
                : successCount == children.Count ? BehaviorTreeStatus.Success : BehaviorTreeStatus.Running;

        if (result != BehaviorTreeStatus.Running)
            runtime.AbortChildrenExcept(this, null);

        return result;
    }

#if TOOLS
    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        root.AddChild(BehaviorTreeEditorUi.BuildEnumRow("Policy", Policy, value => Policy = value));
        return root;
    }
#endif
}
