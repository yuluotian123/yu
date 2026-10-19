using Godot;

namespace GameLogic.AI;

[Tool,GlobalClass]
public partial class NpcNeedDefinition : Resource
{
    [Export] public string NeedId {get;set;}="";
    [Export] public string DisplayName {get;set;}="";
    [Export] public double Minimum {get;set;}
    [Export] public double Maximum {get;set;}=1;
    [Export] public double Initial {get;set;}=.2;
    [Export] public double ChangePerHour {get;set;}=.04;
    [Export] public bool HigherIsUrgent {get;set;}=true;
    [Export] public double EnterThreshold {get;set;}=.65;
    [Export] public double ExitThreshold {get;set;}=.25;
    [Export] public double UrgencyExponent {get;set;}=1;
    [Export] public NpcExpressionDefinition RateMultiplier {get;set;}
}
