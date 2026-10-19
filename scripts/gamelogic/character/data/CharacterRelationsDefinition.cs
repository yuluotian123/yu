using Godot;

namespace GameLogic;

[Tool,GlobalClass]
public partial class CharacterRelationsDefinition : CharacterDefinitionResource
{
    [Export] public Godot.Collections.Array<CharacterFeatureValue> Dimensions {get;set;}=new();
    [Export] public int MaximumContacts {get;set;}=64;
    public override string Category=>"关系维度";
}
