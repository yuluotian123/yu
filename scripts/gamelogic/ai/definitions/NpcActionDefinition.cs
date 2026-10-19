using Godot;
namespace GameLogic.AI;

[Tool, GlobalClass]
public partial class NpcActionDefinition : NpcDefinitionResource
{
    // Storage-only compatibility. Migrated once into the generic configuration below.
    [Export] public string[] TargetTags { get; set; } = System.Array.Empty<string>();
    [Export] public string TargetIdParameter { get; set; } = "";
    [Export] public string TargetSelectorId { get; set; } = "";
    [Export] public bool ShareTargetWithinPlan { get; set; } = true;
    [Export] public bool RequireAtTarget { get; set; }
    [Export] public bool ArrivesAtTarget { get; set; }
    [Export] public bool ReserveTarget { get; set; } = true;
    [Export] public int BindingVersion { get; set; } = 2;
    [Export] public Godot.Collections.Array<NpcBindingDefinition> Inputs { get; set; } = new();
    [Export] public Godot.Collections.Array<NpcResourceClaimDefinition> Resources { get; set; } = new();
    [Export] public NpcExpressionDefinition Preconditions { get; set; }
    [Export] public NpcExpressionDefinition SuccessConditions { get; set; }
    [Export] public NpcExpressionDefinition Cost { get; set; }
    [Export] public double DurationSeconds { get; set; } = 2;
    [Export] public double TimeoutSeconds { get; set; } = 30;
    [Export] public double DistanceCost { get; set; } = .2;
    [Export] public FlowGraphAsset ExecutionGraph { get; set; }
    [Export] public Godot.Collections.Array<NpcEffectDefinition> Predictions { get; set; } = new();
    [Export] public Godot.Collections.Array<NpcEffectDefinition> Results { get; set; } = new();
    public override string Category => "行动";
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        base._ValidateProperty(property);
        if(property["name"].AsString() is nameof(TargetTags) or nameof(TargetIdParameter) or nameof(TargetSelectorId) or nameof(ShareTargetWithinPlan) or nameof(RequireAtTarget) or nameof(ArrivesAtTarget) or nameof(ReserveTarget) or nameof(DistanceCost) or nameof(BindingVersion))
            property["usage"]=(long)PropertyUsageFlags.Storage;
    }
}
