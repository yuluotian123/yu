using System;
using System.Collections.Generic;
using Godot;

// Definitions live in the graph asset; each execution creates its own task.
public abstract class GraphActionTask
{
    public abstract BehaviorTreeStatus Tick(double delta);
    public virtual void Cancel() { }
}

public sealed class GraphDelegateTask : GraphActionTask
{
    private readonly Func<double, BehaviorTreeStatus> _tick;
    private readonly Action _cancel;
    public GraphDelegateTask(Func<double, BehaviorTreeStatus> tick, Action cancel = null)
    { _tick = tick; _cancel = cancel; }
    public override BehaviorTreeStatus Tick(double delta) => _tick(Math.Max(0, delta));
    public override void Cancel() => _cancel?.Invoke();
}

public abstract class GraphSharedAction : BehaviorTreeActionBase
{
    public abstract GraphActionTask CreateTask(GraphActionInvocation invocation);
#if TOOLS
    public override Control CreateEditUI(GraphEditorContext context) => GraphCommonActionEditor.Build(this, context);
#endif
    public override void Execute(GraphExecutionContext context) => Execute(new GraphActionInvocation(context));
    public override void Execute(GraphActionInvocation invocation)
    {
        var task = CreateTask(invocation);
        if (task.Tick(0) == BehaviorTreeStatus.Running)
        {
            task.Cancel();
            GD.PushWarning($"{Description} needs an Action node with a task lifecycle.");
        }
    }
    public override void Validate(GraphAsset graph, string nodeId, GraphValidationResult result)
    {
        if (graph?.FindNodeById(nodeId) is not GraphActionNodeData || graph.GetIncomingConnections(nodeId, 1).Count == 0)
            base.Validate(graph, nodeId, result);
        if (this is not GraphInstantAction && graph?.FindNodeById(nodeId) is FlowTimelineNodeData)
            result.AddError($"{Description} must be placed in an Action node, not a Timeline clip/marker.", nodeId);
    }
}

public abstract class GraphInstantAction : GraphSharedAction
{
    public override void Execute(GraphActionInvocation invocation)
    {
        var timeline = invocation.Execution?.GetUserData<FlowTimelineContext>();
        if (timeline != null && timeline.Phase is not (FlowTimelinePhase.Start or FlowTimelinePhase.Event)) return;
        base.Execute(invocation);
    }
    protected abstract bool Run(GraphActionInvocation invocation);
    public override GraphActionTask CreateTask(GraphActionInvocation invocation) =>
        new GraphDelegateTask(_ => Run(invocation) ? BehaviorTreeStatus.Success : BehaviorTreeStatus.Failure);
}

public sealed class GraphActionSequenceRun
{
    public GraphActionInvocation Invocation { get; set; }
    private int _index;
    private GraphActionTask _task;
    private IBehaviorTreeAction _legacy;
    public BehaviorTreeStatus Status { get; private set; } = BehaviorTreeStatus.Running;

    public BehaviorTreeStatus Tick(IReadOnlyList<GraphActionBase> actions, GraphActionInvocation invocation,
        double delta, BehaviorTreeRuntime behavior = null)
    {
        if (Status != BehaviorTreeStatus.Running) return Status;
        double frameDelta = delta;
        while (actions != null && _index < actions.Count)
        {
            GraphActionBase action = actions[_index];
            BehaviorTreeStatus result;
            if (action is GraphSharedAction shared)
            {
                _task ??= shared.CreateTask(invocation);
                result = _task.Tick(delta);
            }
            else if (behavior != null && action is IBehaviorTreeAction legacy)
            {
                _legacy = legacy;
                // Legacy per-frame actions (patrol/jump timers) each need the full frame delta.
                result = legacy.Tick(behavior, invocation.Execution, frameDelta);
            }
            else
            {
                action?.Execute(invocation);
                result = BehaviorTreeStatus.Success;
            }
            if (result == BehaviorTreeStatus.Running) return result;
            _task = null;
            _legacy = null;
            if (result == BehaviorTreeStatus.Failure) return Status = result;
            _index++;
            delta = 0; // Time already consumed by the preceding action.
        }
        return Status = BehaviorTreeStatus.Success;
    }

    public void Cancel(GraphExecutionContext context, BehaviorTreeRuntime behavior = null)
    {
        _task?.Cancel();
        _legacy?.Abort(behavior, context);
        _task = null;
        _legacy = null;
        Status = BehaviorTreeStatus.Failure;
    }
}
