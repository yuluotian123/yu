using System.Collections.Generic;
using Godot;

public class ActionNode : GraphNodeData
{
    public override string NodeType { get; set ; } = "Action";
    public override int GetInputCount() => 1;

    public List<ActionBase> Actions {get;set;} = new List<ActionBase>();

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



    
}