using Godot;
namespace GameLogic.AI;

public enum NpcEffectMode { Set, Add }
[Tool, GlobalClass]
public partial class NpcEffectDefinition : Resource
{
    [Export] public string Key { get; set; } = "";
    [Export] public NpcEffectMode Mode { get; set; }
    [Export] public CharacterExpressionDefinition Value { get; set; }
}
