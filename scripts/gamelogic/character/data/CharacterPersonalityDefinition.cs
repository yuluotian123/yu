using Godot;

namespace GameLogic;

[Tool,GlobalClass]
public partial class CharacterPersonalityDefinition : CharacterDefinitionResource
{
    [Export] public Godot.Collections.Array<CharacterFeatureValue> Dimensions {get;set;}=new();
    [Export] public string[] Traits {get;set;}=System.Array.Empty<string>();
    [Export] public Godot.Collections.Array<CharacterFeatureValue> Preferences {get;set;}=new();
    public override string Category=>"性格与偏好";
}
