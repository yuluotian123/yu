using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class CharacterFactDefinition : Resource
{
    [Export] public string Key { get; set; } = "private.value";
    [Export] public CharacterValueType Type { get; set; } = CharacterValueType.Number;
    [Export] public double Minimum { get; set; }
    [Export] public double Maximum { get; set; } = 100;
    [Export] public double Step { get; set; } = 1;
    [Export] public CharacterScalarDefinition Initial { get; set; }
}
