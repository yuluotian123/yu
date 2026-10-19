using Godot;
namespace GameLogic.AI;

[Tool, GlobalClass]
public partial class NpcGoalDefinition : NpcDefinitionResource
{
    [Export] public CharacterExpressionDefinition Activation { get; set; }
    [Export] public CharacterExpressionDefinition Utility { get; set; }
    [Export] public CharacterExpressionDefinition Completion { get; set; }
    public override string Category => "目标";
}
