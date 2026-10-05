using System.Collections.Generic;
using Godot;

public class FlowActionNodeData : GraphNodeData, IFlowNode
{
    public List<GraphActionBase> Actions { get; set; } = new();

    public override List<string> GetGraphTypes() => new() { FlowGraphAsset.GraphTypeName };
    public override string GetDisplayName() => "Action Sequence";
    public override string GetMenuName() => "Action Sequence";
    public override Color GetNodeColor() => new(0.42f, 0.72f, 0.92f);
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 2;
    public override bool CanBePrime() => false;

    public override void Validate(GraphAsset graph, GraphValidationResult result)
    {
        if (Actions == null)
            return;
        foreach (GraphActionBase action in Actions)
            action?.Validate(graph, Id, result);
    }
    public override string GetOutputPortName(int port) => port == 0 ? "Success" : "Failure";

    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        var run = new GraphActionSequenceRun();
        runtime.SetNodeData(Id, run);
        run.Tick(Actions, new GraphActionInvocation(context), 0);
    }

    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) =>
        runtime.GetNodeData<GraphActionSequenceRun>(Id).Tick(Actions, new GraphActionInvocation(context), delta);
    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        var status = runtime.GetNodeData<GraphActionSequenceRun>(Id).Status;
        completion = status == BehaviorTreeStatus.Failure ? NodeCompletion.False("Failure") : NodeCompletion.Next("Success");
        return status != BehaviorTreeStatus.Running;
    }

    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        if (runtime.TryGetNodeData(Id, out GraphActionSequenceRun run)) run.Cancel(context);
        runtime.SetNodeData(Id, null);
    }

    public override void CreateNodeUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(160f, 0f) };
        root.AddChild(new Label
        {
            Text = GetActionSummary(),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        context.GraphNode.AddChild(root);
    }

    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(260f, 0f) };
        root.AddThemeConstantOverride("separation", 6);
        root.AddChild(new Label { Text = "Actions" });

        var listControl = CreateActionList(context);
        root.AddChild(listControl.Build());
        return root;
    }

    public override void CreateUI(GraphEditorContext context)
    {
        context.GraphNode.AddChild(CreateActionList(context).Build());
    }

    private ReorderableListControl<GraphActionBase> CreateActionList(GraphEditorContext context)
    {
        return new ReorderableListControl<GraphActionBase>(
            items: Actions,
            buildItemUi: action => action.CreateEditUI(context),
            getItemLabel: action => action.Description,
            availableTypes: GraphCallableCatalog.Actions(GraphCallableUsage.Flow),
            factory: type => (GraphActionBase)System.Activator.CreateInstance(type)
        );
    }

    private string GetActionSummary()
    {
        if (Actions == null || Actions.Count == 0)
            return "No actions";

        string first = Actions[0]?.Description;
#if TOOLS
        first = GraphCallableCatalog.ItemLabel(Actions[0], first);
#endif
        if (string.IsNullOrWhiteSpace(first))
            first = Actions[0]?.GetType().Name ?? "Action";

        return Actions.Count == 1 ? first : $"{first} +{Actions.Count - 1}";
    }
}
