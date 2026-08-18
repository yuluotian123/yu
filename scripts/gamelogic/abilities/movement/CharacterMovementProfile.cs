using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class CharacterMovementProfile : Resource
    {
        [Export] public float MoveSpeed { get; set; } = 280f;
        [Export] public float JumpVelocity { get; set; } = -720f;
        [Export] public float JumpBufferTime { get; set; } = 0.12f;
        [Export] public float CoyoteTime { get; set; } = 0.1f;
        [Export] public float Gravity { get; set; } = 1600f;
        [Export] public float MaxFallSpeed { get; set; } = 900f;
        [Export] public float FloorSnapLength { get; set; } = 12f;
        [Export] public float Acceleration { get; set; } = 1800f;
        [Export] public float Deceleration { get; set; } = 2200f;
        [Export(PropertyHint.Range, "0,1,0.05")] public float AirControl { get; set; } = 0.65f;
        [Export] public bool CutJumpOnRelease { get; set; } = true;
        [Export(PropertyHint.Range, "0.1,1.0,0.05")] public float JumpCutMultiplier { get; set; } = 0.45f;
    }
}
