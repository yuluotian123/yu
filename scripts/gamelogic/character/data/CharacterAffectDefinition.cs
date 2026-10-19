using Godot;

namespace GameLogic;

[Tool,GlobalClass]
public partial class CharacterAffectDefinition : CharacterDefinitionResource
{
    [Export] public Godot.Collections.Array<CharacterFeatureValue> Dimensions {get;set;}=new();
    public override string Category=>"临时情绪";
}
