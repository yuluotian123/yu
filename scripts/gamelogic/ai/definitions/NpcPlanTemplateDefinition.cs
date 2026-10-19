using Godot;
namespace GameLogic.AI;

[Tool, GlobalClass]
public partial class NpcPlanTemplateDefinition : NpcDefinitionResource
{
    [Export] public CharacterExpressionDefinition Eligibility { get; set; }
    [Export] public int Priority { get; set; }
    [Export] public string[] ActionIds { get; set; } = System.Array.Empty<string>();
    [Export] public Godot.Collections.Array<NpcBindingDefinition> Variables { get; set; } = new();
    public override string Category => "计划";
}
