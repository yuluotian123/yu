using Framework;
using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class CharacterGravityComponent2D : Component2D
    {
        public override int Priority => ComponentPriority.Physics;

        [Export] public float Gravity { get; set; } = 1600f;
        [Export] public float MaxFallSpeed { get; set; } = 900f;

        private CharacterBodyMotorComponent2D _motor;
        public override void OnInit()
        {
            _motor = Owner.GetComponent<CharacterBodyMotorComponent2D>();

            if (_motor == null)
                Debugger.Warn("[CharacterGravityComponent2D] Missing CharacterBodyMotorComponent2D.");
        }

        public override void OnPhysicsUpdate(double delta)
        {
            if (_motor == null)
                return;

            if (_motor.IsOnFloor)
            {
                if (_motor.Velocity.Y > 0f)
                    _motor.SetVerticalVelocity(0f);

                return;
            }

            float nextVelocityY = _motor.Velocity.Y + Gravity * (float)delta;
            _motor.SetVerticalVelocity(Mathf.Min(nextVelocityY, MaxFallSpeed));
        }
    }
}
