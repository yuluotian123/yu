using Godot;

namespace GameLogic;

[Tool,GlobalClass]
public partial class CharacterAffectDefinition : CharacterDefinitionResource
{
    [Export] public Godot.Collections.Array<CharacterScalarDefinition> Dimensions {get;set;}=new();
    [Export] public double RecoveryPerHour {get;set;}=.1;
    public override string Category=>"临时情绪";
}
