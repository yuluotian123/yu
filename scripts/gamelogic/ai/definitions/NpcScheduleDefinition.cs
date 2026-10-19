using Godot;

namespace GameLogic.AI;

[Tool,GlobalClass]
public partial class NpcScheduleDefinition : NpcDefinitionResource
{
    [Export] public Godot.Collections.Array<NpcScheduleWindow> Windows {get;set;}=new();
    public override string Category=>"日程窗口";
}
