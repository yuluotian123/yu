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
}
