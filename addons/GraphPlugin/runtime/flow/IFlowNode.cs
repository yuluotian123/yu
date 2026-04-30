public interface IFlowNode
{
    void Enter(FlowGraphRuntime runtime, GraphExecutionContext context);
    void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta);
    bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion);
    void Exit(FlowGraphRuntime runtime, GraphExecutionContext context);
}
