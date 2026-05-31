public interface IBehaviorTreeNode
{
    BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta);
    void Abort(BehaviorTreeRuntime runtime, GraphExecutionContext context);
}
