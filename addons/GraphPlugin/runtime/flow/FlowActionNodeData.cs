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
            availableTypes: SubTypeCache.GetSubTypes<GraphActionBase>(),
            factory: type => (GraphActionBase)System.Activator.CreateInstance(type)
        );
    }

    private string GetActionSummary()
    {
        if (Actions == null || Actions.Count == 0)
            return "No actions";

        string first = Actions[0]?.Description;
        if (string.IsNullOrWhiteSpace(first))
            first = Actions[0]?.GetType().Name ?? "Action";

        return Actions.Count == 1 ? first : $"{first} +{Actions.Count - 1}";
    }
}
