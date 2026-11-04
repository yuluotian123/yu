using Godot;

namespace GameLogic.AI;

/// <summary>A named memory entry, recorded independently for each subject.</summary>
[Tool, GlobalClass]
public partial class NpcMemoryEntryDefinition : Resource
{
    [Export] public string Key { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public bool InitialValue { get; set; }
    /// <summary>Zero means permanent; positive values are measured in game hours.</summary>
    [Export(PropertyHint.Range, "0,87600,1")] public double LifetimeHours { get; set; }
    [Export] public string LegacyDialogueFlag { get; set; } = "";
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (property["name"].AsString() == nameof(LegacyDialogueFlag)) property["usage"] = (long)PropertyUsageFlags.Storage;
    }
}
