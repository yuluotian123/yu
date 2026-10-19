using Godot;
namespace GameLogic.AI;

[Tool, GlobalClass]
public partial class NpcFactSchema : NpcDefinitionResource
{
    [Export] public Godot.Collections.Array<NpcFactDefinition> Facts { get; set; } = new();
    public override string Category => "私有事实";
}
