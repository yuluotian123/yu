using System.Collections.Generic;
using Godot;

public abstract class BehaviorTreeNodeData : GraphNodeData, IBehaviorTreeNode
{
    public override List<string> GetGraphTypes() => new() { BehaviorTreeGraphAsset.GraphTypeName };
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 0;
    public override int GetInputMaxConnections(int port) => 1;
    public override bool CanBePrime() => false;
    public override Color GetNodeColor() => new(0.76f, 0.76f, 0.76f);

    public abstract BehaviorTreeStatus Tick(
        BehaviorTreeRuntime runtime,
        GraphExecutionContext context,
        double delta);

    public virtual void Abort(BehaviorTreeRuntime runtime, GraphExecutionContext context)
    {
        runtime?.ClearNodeData(Id);
    }

    public override void CreateNodeUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(160f, 0f) };
        root.AddChild(new Label
        {
            Text = GetDisplayName(),
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipText = true
        });
        context.GraphNode.AddChild(root);
    }
}
