using System.Collections.Generic;
using Godot;

public class FlowConditionNodeData : GraphNodeData, IFlowNode
{
    public GraphConditionUseMode UseMode { get; set; } = GraphConditionUseMode.And;
    public List<GraphConditionBase> Conditions { get; set; } = new();

    public override List<string> GetGraphTypes() => new() { FlowGraphAsset.GraphTypeName };
    public override string GetDisplayName() => "Condition";
    public override Color GetNodeColor() => new(0.95f, 0.72f, 0.28f);
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 2;
    public override bool CanBePrime() => false;
    public override string GetOutputPortName(int port) => port == 0 ? "True" : "False";

    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
    }

    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }
    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        completion = IsMet(context) ? NodeCompletion.True() : NodeCompletion.False();
        return true;
    }

    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) { }

    public override void CreateUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();

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
            availableTypes: SubTypeCache.GetSubTypes<GraphConditionBase>(),
            factory: type => (GraphConditionBase)System.Activator.CreateInstance(type)
        );

        root.AddChild(listControl.Build());
        context.GraphNode.AddChild(root);
    }

    private bool IsMet(GraphExecutionContext context)
    {
        if (Conditions == null || Conditions.Count == 0)
            return true;

        if (UseMode == GraphConditionUseMode.Or)
        {
            for (int i = 0; i < Conditions.Count; i++)
            {
                if (Conditions[i]?.IsMet(context) == true)
                    return true;
            }

            return false;
        }

        for (int i = 0; i < Conditions.Count; i++)
        {
            if (Conditions[i]?.IsMet(context) != true)
                return false;
        }

        return true;
    }
}
