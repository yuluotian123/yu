using System.Collections.Generic;
using Godot;

public class BehaviorConditionNodeData : BehaviorTreeNodeData
{
    public GraphConditionUseMode UseMode { get; set; } = GraphConditionUseMode.And;
    public List<GraphConditionBase> Conditions { get; set; } = new();

    public override string GetDisplayName() => "Condition";
    public override string GetCategory() => "BehaviorTree/Leaf";
    public override Color GetNodeColor() => new(0.95f, 0.72f, 0.28f);

    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        return GraphConditionEvaluator.IsMet(Conditions, UseMode, context)
            ? BehaviorTreeStatus.Success
            : BehaviorTreeStatus.Failure;
    }

    public override void CreateNodeUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(160f, 0f) };
        string mode = UseMode == GraphConditionUseMode.And ? "All" : "Any";
        root.AddChild(new Label
        {
            Text = Conditions == null || Conditions.Count == 0
                ? $"{mode}, always"
                : $"{mode}: {Conditions[0]?.Description ?? "Condition"}",
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

        var useOption = new OptionButton();
        useOption.AddItem("All (And)", (int)GraphConditionUseMode.And);
        useOption.AddItem("Any (Or)", (int)GraphConditionUseMode.Or);
        useOption.Selected = (int)UseMode;
        useOption.ItemSelected += index => UseMode = (GraphConditionUseMode)index;
        root.AddChild(useOption);

        var listControl = new ReorderableListControl<GraphConditionBase>(
            items: Conditions,
            buildItemUi: condition => condition.CreateEditUI(context),
            getItemLabel: condition => condition.Description,
            availableTypes: SubTypeCache.GetSubTypes<BehaviorTreeConditionBase>(),
            factory: type => (GraphConditionBase)System.Activator.CreateInstance(type)
        );
        root.AddChild(listControl.Build());
        return root;
    }
#endif
}
