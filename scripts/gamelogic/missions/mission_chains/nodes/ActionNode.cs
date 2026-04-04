using System.Collections.Generic;
using Godot;

public class ActionNode : GraphNodeData
{
    public override int GetInputCount() => 1;
    public override bool CanBePrime() => false;

    public List<ActionBase> Actions { get; set; } = new List<ActionBase>();

    public override void CreateUI(GraphNode node)
    {
        var listControl = new ReorderableListControl<ActionBase>(
            items: Actions,
            buildItemUi: action => action.CreateEditUI(),
            getItemLabel: action => action.GetType().Name,
            availableTypes: SubTypeCache.GetSubTypes<ActionBase>(),
            factory: type => (ActionBase)System.Activator.CreateInstance(type)
        );

        node.AddChild(listControl.Build());
    }

    public override void Execute()
    {
        foreach (var a in Actions) a?.Execute();
    }

}