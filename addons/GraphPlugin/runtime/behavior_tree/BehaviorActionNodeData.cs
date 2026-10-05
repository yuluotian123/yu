using System.Collections.Generic;
using Godot;

public class BehaviorActionNodeData : BehaviorTreeNodeData
{
    public List<GraphActionBase> Actions { get; set; } = new();

    public override string GetDisplayName() => "Action";
    public override string GetCategory() => "BehaviorTree/Leaf";
    public override Color GetNodeColor() => new(0.42f, 0.78f, 0.88f);

    public override void Validate(GraphAsset graph, GraphValidationResult result)
    {
        if (Actions == null)
            return;
        foreach (GraphActionBase action in Actions)
            action?.Validate(graph, Id, result);
    }

    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        var run = runtime.GetNodeData<GraphActionSequenceRun>(Id);
        var status = run.Tick(Actions, new GraphActionInvocation(context), delta, runtime);
        if (status != BehaviorTreeStatus.Running) runtime.ClearNodeData(Id);
        return status;
    }

    public override void Abort(BehaviorTreeRuntime runtime, GraphExecutionContext context)
    {
        runtime.GetNodeData<GraphActionSequenceRun>(Id).Cancel(context, runtime);
        base.Abort(runtime, context);
    }

    public override void CreateNodeUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(160f, 0f) };
        root.AddChild(new Label
        {
            Text = GetActionSummary(),
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipText = true
        });
        context.GraphNode.AddChild(root);
    }

#if TOOLS
    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 6);

        var listControl = new ReorderableListControl<GraphActionBase>(
            items: Actions,
            buildItemUi: action => action.CreateEditUI(context),
            getItemLabel: action => action.Description,
            availableTypes: GraphCallableCatalog.Actions(GraphCallableUsage.BehaviorTree),
            factory: type => (GraphActionBase)System.Activator.CreateInstance(type)
        );
        root.AddChild(listControl.Build());
        return root;
    }
#endif

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
