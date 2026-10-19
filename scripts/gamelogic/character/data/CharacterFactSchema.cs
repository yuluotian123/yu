using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class CharacterFactSchema : CharacterDefinitionResource
{
    [Export] public Godot.Collections.Array<CharacterFactDefinition> Facts { get; set; } = new();
    public override string Category => "私有事实";
}
