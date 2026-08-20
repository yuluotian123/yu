using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class AbilityActivationPolicy : Resource
    {
        [Export] public int Priority { get; set; }
        [Export] public bool CanInterrupt { get; set; } = true;
        [Export] public bool AllowConcurrent { get; set; }
        [Export] public bool BlocksMovement { get; set; }
        [Export] public bool BlocksJump { get; set; }

        public static AbilityActivationPolicy Default => new();
    }
}
