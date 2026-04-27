using System.Collections.Generic;
using System.Text.Json.Serialization;
using GameLogic;
using Godot;

public class MissionNode : GraphNodeData
{
    [JsonInclude]
    private readonly List<MissionRequireTemplate> _requires =
          new List<MissionRequireTemplate>();

    [JsonInclude]
    private MissionRequireMode _mode;

    public override List<string> GetGraphTypes()
        => new List<string> { "MissionGraph" };
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;



    public MissionPrototype<object> CreateMissionProto(string GraphName = "")
    {
        var proto = new MissionPrototype<object>(GraphName + "." + Id, _requires.ToArray(), _mode);
        return proto;
    }

    public override void CreateUI(GraphEditorContext context)
    {
        base.CreateUI(context);

        var listControl = new ReorderableListControl<MissionRequireTemplate>(
            items: _requires,
            buildItemUi: require => require.CreateEditUI(context),
            getItemLabel: require => require.GetType().Name,
            availableTypes: SubTypeCache.GetSubTypes<MissionRequireTemplate>(),
            factory: type => (MissionRequireTemplate)System.Activator.CreateInstance(type)
        );

        context.GraphNode.AddChild(listControl.Build());

    }

}
