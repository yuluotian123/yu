using Godot;

namespace GameLogic
{
    [Tool, GlobalClass]
    public partial class AbilitySetResource : Resource
    {
        [Export] public Godot.Collections.Array<Resource> Abilities { get; set; } = new();
    }
}
