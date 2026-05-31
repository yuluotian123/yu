using System.Collections.Generic;
using Godot;

public class BehaviorActionNodeData : BehaviorTreeNodeData
{
    public List<GraphActionBase> Actions { get; set; } = new();

    public override string GetDisplayName() => "Action";
    public override string GetCategory() => "BehaviorTree/Leaf";
    public override Color GetNodeColor() => new(0.42f, 0.78f, 0.88f);

    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        if (Actions == null || Actions.Count == 0)
            return BehaviorTreeStatus.Success;

        for (int i = 0; i < Actions.Count; i++)
        {
            GraphActionBase action = Actions[i];
            if (action == null)
                continue;

            if (action is IBehaviorTreeAction behaviorAction)
            {
                BehaviorTreeStatus status = behaviorAction.Tick(runtime, context, delta);
                if (status != BehaviorTreeStatus.Success)
                    return status;

                continue;
            }

            action.Execute(context);
        }

        return BehaviorTreeStatus.Success;
    }

    public override void Abort(BehaviorTreeRuntime runtime, GraphExecutionContext context)
    {
        if (Actions != null)
        {
            for (int i = 0; i < Actions.Count; i++)
            {
                if (Actions[i] is IBehaviorTreeAction behaviorAction)
                    behaviorAction.Abort(runtime, context);
            }
        }

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
            availableTypes: SubTypeCache.GetSubTypes<BehaviorTreeActionBase>(),
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
        if (string.IsNullOrWhiteSpace(first))
            first = Actions[0]?.GetType().Name ?? "Action";

        return Actions.Count == 1 ? first : $"{first} +{Actions.Count - 1}";
    }
}
