using Godot;
namespace GameLogic.AI;

[Tool, GlobalClass]
public partial class NpcResourceClaimDefinition : Resource
{
    [Export] public string Input { get; set; } = "";
    [Export] public string Capability { get; set; } = "capacity";
}
