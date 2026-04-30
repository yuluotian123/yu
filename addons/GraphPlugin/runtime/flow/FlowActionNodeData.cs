using System.Collections.Generic;
using Godot;

public class FlowActionNodeData : GraphNodeData, IFlowNode
{
    public List<GraphActionBase> Actions { get; set; } = new();

    public override List<string> GetGraphTypes() => new() { FlowGraphAsset.GraphTypeName };
    public override string GetDisplayName() => "Action";
    public override Color GetNodeColor() => new(0.42f, 0.72f, 0.92f);
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;
    public override bool CanBePrime() => false;
    public override string GetOutputPortName(int port) => "Next";

    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        for (int i = 0; i < Actions.Count; i++)
            Actions[i]?.Execute(context);
    }

    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }
    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        completion = NodeCompletion.Next();
        return true;
    }

    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) { }

    public override void CreateUI(GraphEditorContext context)
    {
        var listControl = new ReorderableListControl<GraphActionBase>(
            items: Actions,
            buildItemUi: action => action.CreateEditUI(context),
            getItemLabel: action => action.Description,
            availableTypes: SubTypeCache.GetSubTypes<GraphActionBase>(),
            factory: type => (GraphActionBase)System.Activator.CreateInstance(type)
        );

        context.GraphNode.AddChild(listControl.Build());
    }
}
