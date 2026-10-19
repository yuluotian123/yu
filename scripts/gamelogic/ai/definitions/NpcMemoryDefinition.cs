using Godot;

namespace GameLogic.AI;

[Tool,GlobalClass]
public partial class NpcMemoryDefinition : NpcDefinitionResource
{
    [Export] public string[] Categories {get;set;}=System.Array.Empty<string>();
    [Export] public int Capacity {get;set;}=32;
    [Export] public double LifetimeHours {get;set;}=24;
    public override string Category=>"记忆规则";
}
