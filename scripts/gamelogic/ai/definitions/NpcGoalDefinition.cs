using Godot;
namespace GameLogic.AI;

[Tool, GlobalClass]
public partial class NpcGoalDefinition : NpcDefinitionResource
{
    [Export] public NpcExpressionDefinition Activation { get; set; }
    [Export] public NpcExpressionDefinition Utility { get; set; }
    [Export] public NpcExpressionDefinition Completion { get; set; }
    public override string Category => "目标";
}
