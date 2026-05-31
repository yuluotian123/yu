public abstract class BehaviorTreeActionBase : GraphActionBase, IBehaviorTreeAction
{
    public override void Execute(GraphExecutionContext context)
    {
    }

    public virtual BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        Execute(context);
        return BehaviorTreeStatus.Success;
    }

    public virtual void Abort(BehaviorTreeRuntime runtime, GraphExecutionContext context)
    {
    }
}
