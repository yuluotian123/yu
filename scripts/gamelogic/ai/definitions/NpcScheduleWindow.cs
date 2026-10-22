using Godot;

namespace GameLogic.AI;

[Tool,GlobalClass]
public partial class NpcScheduleWindow : Resource
{
    [Export] public string WindowId {get;set;}="";
    [Export] public double StartHour {get;set;}=8;
    [Export] public double EndHour {get;set;}=17;
    [Export] public string[] Tags {get;set;}=System.Array.Empty<string>();
    [Export] public int Priority {get;set;}
    [Export] public WorldLocationDefinition Location {get;set;}
    [Export] public string LocationBinding {get;set;}="";
    [Export] public NpcGoalDefinition Goal {get;set;}
    [Export] public NpcGoalDefinition FallbackGoal {get;set;}
    [Export] public string[] AllowedOperations {get;set;}=System.Array.Empty<string>();
    [Export] public double CommitmentSeconds {get;set;}
    [Export] public bool Interruptible {get;set;}=true;
    [Export] public bool Emergency {get;set;}
    [Export] public CharacterExpressionDefinition Activation {get;set;}
}
