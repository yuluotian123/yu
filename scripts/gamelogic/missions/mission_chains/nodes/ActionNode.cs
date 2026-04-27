using System.Collections.Generic;
using Godot;

public class ActionNode : GraphNodeData
{
    public override int GetInputCount() => 1;
    public override bool CanBePrime() => false;

    public List<ActionBase> Actions { get; set; } = new List<ActionBase>();

    public override void CreateUI(GraphEditorContext context)
    {
        var listControl = new ReorderableListControl<ActionBase>(
            items: Actions,
            buildItemUi: action => action.CreateEditUI(context),
            getItemLabel: action => action.GetType().Name,
            availableTypes: SubTypeCache.GetSubTypes<ActionBase>(),
            factory: type => (ActionBase)System.Activator.CreateInstance(type)
        );

        context.GraphNode.AddChild(listControl.Build());
    }

    public override void Execute(GraphExecutionContext context)
    {
        foreach (var a in Actions) a?.Execute(context);
    }

}
