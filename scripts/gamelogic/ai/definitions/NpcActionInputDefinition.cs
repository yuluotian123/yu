using Godot;
namespace GameLogic.AI;

/// <summary>Per-action usage requirements. Plan variables only hold the bound value.</summary>
[Tool, GlobalClass]
public partial class NpcActionInputDefinition : NpcBindingDefinition
{
    [Export] public string[] ResourceRequirements { get; set; } = System.Array.Empty<string>();
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        base._ValidateProperty(property);
        if(property["name"].AsString()==nameof(ResourceRequirements)&&
            (ValueType!=NpcValueType.Symbol||NpcBindingAdapters.Find(Adapter)?.Capabilities.Count is not >0))
            property["usage"]=(long)PropertyUsageFlags.Storage;
    }
}
