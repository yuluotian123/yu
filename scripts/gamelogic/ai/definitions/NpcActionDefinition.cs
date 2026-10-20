using Godot;
namespace GameLogic.AI;

[Tool, GlobalClass]
public partial class NpcActionDefinition : NpcDefinitionResource
{
    [Export] public Godot.Collections.Array<NpcActionInputDefinition> Inputs { get; set; } = new();
    [Export] public CharacterExpressionDefinition Preconditions { get; set; }
    [Export] public CharacterExpressionDefinition SuccessConditions { get; set; }
    [Export] public CharacterExpressionDefinition Cost { get; set; }
    [Export] public double DurationSeconds { get; set; } = 2;
    [Export] public double TimeoutSeconds { get; set; } = 30;
    [Export] public FlowGraphAsset ExecutionGraph { get; set; }
    [Export] public Godot.Collections.Array<NpcEffectDefinition> Predictions { get; set; } = new();
    [Export] public Godot.Collections.Array<NpcEffectDefinition> Results { get; set; } = new();
    public override string Category => "行动";
}
