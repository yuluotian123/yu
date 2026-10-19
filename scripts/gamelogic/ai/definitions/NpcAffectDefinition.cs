using Godot;

namespace GameLogic.AI;

[Tool,GlobalClass]
public partial class NpcAffectDefinition : NpcDefinitionResource
{
    [Export] public Godot.Collections.Array<NpcScalarDefinition> Dimensions {get;set;}=new();
    [Export] public double RecoveryPerHour {get;set;}=.1;
    public override string Category=>"临时情绪";
}
