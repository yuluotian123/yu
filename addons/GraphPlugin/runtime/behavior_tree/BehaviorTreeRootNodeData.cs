using System.Collections.Generic;
using Godot;

public class BehaviorRootNodeData : BehaviorTreeNodeData
{
    public override string GetDisplayName() => "Root";
    public override string GetCategory() => "BehaviorTree";
    public override Color GetNodeColor() => new(0.35f, 0.92f, 0.62f);
    public override int GetInputCount() => 0;
    public override int GetOutputCount() => 1;
    public override int GetOutputMaxConnections(int port) => 1;
    public override bool CanBePrime() => true;

    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        List<BehaviorTreeNodeData> children = runtime.GetChildren(this);
        return children.Count == 0
            ? BehaviorTreeStatus.Failure
            : runtime.TickNode(children[0], delta);
    }
}
