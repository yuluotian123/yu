using Godot;
namespace GameLogic.AI;

/// <summary>Decision thresholds are separate from the character's physical needs.</summary>
[Tool, GlobalClass]
public partial class NpcNeedPolicyDefinition : NpcDefinitionResource
{
    [Export] public string NeedId {get;set;}="";
    [Export] public bool HigherIsUrgent {get;set;}=true;
    [Export] public double EnterThreshold {get;set;}=.65;
    [Export] public double ExitThreshold {get;set;}=.25;
    [Export] public double UrgencyExponent {get;set;}=1;
    public override string Category=>"需求决策规则";
}
