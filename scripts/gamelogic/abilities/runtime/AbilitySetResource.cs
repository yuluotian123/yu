using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class AbilitySetResource : Resource
    {
        [Export] public Godot.Collections.Array<AbilityResource> Abilities { get; set; } = new();
    }
}
