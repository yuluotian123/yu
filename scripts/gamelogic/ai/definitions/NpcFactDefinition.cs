using Godot;
namespace GameLogic.AI;

[Tool, GlobalClass]
public partial class NpcFactDefinition : Resource
{
    [Export] public string Key { get; set; } = "private.value";
    [Export] public NpcValueType Type { get; set; } = NpcValueType.Number;
    [Export] public double Minimum { get; set; }
    [Export] public double Maximum { get; set; } = 100;
    [Export] public double Step { get; set; } = 1;
    [Export] public NpcScalarDefinition Initial { get; set; }
}
