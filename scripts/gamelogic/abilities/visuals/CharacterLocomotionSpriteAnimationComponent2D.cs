using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class CharacterLocomotionSpriteAnimationComponent2D : SpriteAnimationComponent2D
    {
        [Export] public string IdleAnimation { get; set; } = "idle";
        [Export] public string RunAnimation { get; set; } = "run";
        [Export] public string JumpUpAnimation { get; set; } = "jumpup";
        [Export] public string InAirAnimation { get; set; } = "inair";
        [Export] public string FallingAnimation { get; set; } = "isfalling";
        [Export] public string LandAnimation { get; set; } = "land";
        [Export] public string LocomotionRequestKey { get; set; } = "locomotion";
        [Export] public int LocomotionPriority { get; set; } = -100;
        [Export] public float MinRunSpeed { get; set; } = 5f;
        [Export] public float LandingPoseTime { get; set; } = 0.1f;
        [Export] public float RiseVelocity { get; set; } = -180f;
        [Export] public float ApexVelocity { get; set; } = 120f;

        private CharacterMoveComponent2D _move;
        private CharacterBodyMotorComponent2D _motor;
        private bool _wasAirborne;
        private float _landingTimer;

        public override void OnInit()
        {
            base.OnInit();

            _move = Owner.GetComponent<CharacterMoveComponent2D>();
            _motor = Owner.GetComponent<CharacterBodyMotorComponent2D>();
            _wasAirborne = false;
            _landingTimer = 0f;

            RequestLocomotionAnimation(IdleAnimation);
        }

        public override void OnDestroy()
        {
            ClearAnimationRequest(LocomotionRequestKey);
            _move = null;
            _motor = null;
            _wasAirborne = false;
            _landingTimer = 0f;

            base.OnDestroy();
        }

        public override void OnPhysicsUpdate(double delta)
        {
            float dt = (float)delta;
            bool airborne = IsAirborne();
            UpdateAirborneTimers(airborne, dt);

            RequestLocomotionAnimation(ResolveLocomotionAnimation());
        }

        private void UpdateAirborneTimers(bool airborne, float delta)
        {
            if (airborne)
            {
                _landingTimer = 0f;
            }
            else if (_wasAirborne)
            {
                _landingTimer = LandingPoseTime;
            }
            else
            {
                _landingTimer = Mathf.Max(0f, _landingTimer - delta);
            }

            _wasAirborne = airborne;
        }

        private string ResolveLocomotionAnimation()
        {
            if (IsAirborne())
                return ResolveAirborneAnimation();

            if (_landingTimer > 0f)
                return LandAnimation;

            return IsRunning() ? RunAnimation : IdleAnimation;
        }

        private string ResolveAirborneAnimation()
        {
            if (_motor == null)
                return InAirAnimation;

            float velocityY = _motor.Velocity.Y;
            if (velocityY < RiseVelocity)
                return JumpUpAnimation;

            if (velocityY <= ApexVelocity)
                return InAirAnimation;

            return FallingAnimation;
        }

        private bool IsAirborne()
        {
            return _motor != null && !_motor.IsOnFloor;
        }

        private bool IsRunning()
        {
            if (_move != null && Mathf.Abs(_move.InputX) > 0.01f)
                return true;

            return _motor != null && Mathf.Abs(_motor.Velocity.X) > MinRunSpeed;
        }

        private void RequestLocomotionAnimation(string animation)
        {
            RequestAnimation(
                LocomotionRequestKey,
                animation,
                LocomotionPriority,
                restartIfPlaying: false);
        }
    }
}
