using Godot;

namespace GameLogic.AI;

[Tool,GlobalClass]
public partial class NpcPolicyDefinition : NpcDefinitionResource
{
    [Export] public int NodesPerSlice {get;set;}=16;
    [Export] public int MaximumExpanded {get;set;}=2048;
    [Export] public int MaximumPlanDepth {get;set;}=16;
    [Export] public int TargetLimit {get;set;}=4;
    [Export] public double CommitmentSeconds {get;set;}=.8;
    [Export] public double SwitchMargin {get;set;}=.2;
    [Export] public double RetrySeconds {get;set;}=2;
    [Export] public float DetailedDistance {get;set;}=30;
    [Export] public float AbstractDistance {get;set;}=45;
    public override string Category=>"决策与预算";
}
