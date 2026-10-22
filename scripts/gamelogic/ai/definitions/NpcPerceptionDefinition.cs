using Godot;

namespace GameLogic.AI;

[Tool,GlobalClass]
public partial class NpcPerceptionDefinition : NpcDefinitionResource
{
    [Export] public float ViewRange {get;set;}=12;
    [Export] public double SenseInterval {get;set;}=.3;
    [Export] public string[] PointTags {get;set;}=System.Array.Empty<string>();
    [Export] public bool RequireLineOfSight {get;set;}=true;
    public override string Category=>"感知";
}
