using Godot;

namespace GameLogic.AI;

[Tool,GlobalClass]
public partial class NpcRelationsDefinition : NpcDefinitionResource
{
    [Export] public Godot.Collections.Array<NpcScalarDefinition> Dimensions {get;set;}=new();
    [Export] public double Minimum {get;set;}=-1;
    [Export] public double Maximum {get;set;}=1;
    [Export] public int MaximumContacts {get;set;}=64;
    public override string Category=>"关系维度";
}
