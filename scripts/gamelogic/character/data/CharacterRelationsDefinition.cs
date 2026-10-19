using Godot;

namespace GameLogic;

[Tool,GlobalClass]
public partial class CharacterRelationsDefinition : CharacterDefinitionResource
{
    [Export] public Godot.Collections.Array<CharacterScalarDefinition> Dimensions {get;set;}=new();
    [Export] public double Minimum {get;set;}=-1;
    [Export] public double Maximum {get;set;}=1;
    [Export] public int MaximumContacts {get;set;}=64;
    public override string Category=>"关系维度";
}
