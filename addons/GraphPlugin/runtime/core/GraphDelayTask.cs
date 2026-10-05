using System;

public sealed class GraphDelayTask : GraphActionTask
{
    private readonly float _seconds;
    private double _elapsed;
    public BehaviorTreeStatus Status { get; private set; }
    public GraphDelayTask(float seconds) { _seconds = seconds; Tick(0); }
    public override BehaviorTreeStatus Tick(double delta)
    {
        _elapsed += Math.Max(0, delta);
        return Status = !float.IsFinite(_seconds) || _seconds < 0 ? BehaviorTreeStatus.Failure
            : _elapsed >= _seconds ? BehaviorTreeStatus.Success : BehaviorTreeStatus.Running;
    }
}
