using Godot;
namespace GameLogic.AI;

[Tool, GlobalClass]
// Storage-only compatibility for graphs authored before requirements moved into inputs.
public partial class NpcResourceClaimDefinition : Resource
{
    [Export] public string Input { get; set; } = "";
    [Export] public string Capability { get; set; } = "capacity";
}
