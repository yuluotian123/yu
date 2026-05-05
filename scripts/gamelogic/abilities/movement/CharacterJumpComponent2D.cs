using Godot;

namespace GameLogic
{
    public readonly struct JumpIntent2D
    {
        public JumpIntent2D(bool startRequested, bool sustainRequested)
        {
            StartRequested = startRequested;
            SustainRequested = sustainRequested;
        }

        public bool StartRequested { get; }
        public bool SustainRequested { get; }

        public static JumpIntent2D None => new(false, false);
    }

    [GlobalClass]
    public partial class CharacterJumpComponent2D : Component2D, ICharacterIntentAbility2D<JumpIntent2D>
    {
        public override int Priority => ComponentPriority.Combat;

        [Export] public float JumpVelocity { get; set; } = -720f;
        [Export] public float JumpBufferTime { get; set; } = 0.12f;
        [Export] public float CoyoteTime { get; set; } = 0.1f;
        [Export] public bool CutJumpOnRelease { get; set; } = true;
        [Export(PropertyHint.Range, "0.1,1.0,0.05")] public float JumpCutMultiplier { get; set; } = 0.45f;

        public JumpIntent2D RawIntent { get; private set; } = JumpIntent2D.None;
        public JumpIntent2D ApprovedIntent { get; private set; } = JumpIntent2D.None;

        private CharacterBodyMotorComponent2D _motor;
        private float _jumpBufferTimer;
        private float _coyoteTimer;
        private bool _wasJumpSustainRequested;

        public override void OnInit()
        {
            _motor = Owner.GetComponent<CharacterBodyMotorComponent2D>();
        }

        public override void OnPhysicsUpdate(double delta)
        {
            if (_motor == null)
            {
                ClearFrameIntents();
                return;
            }

            float dt = (float)delta;
            bool jumpStartRequested = ApprovedIntent.StartRequested;
            bool jumpSustainRequested = ApprovedIntent.SustainRequested;

            if (_motor.IsOnFloor)
                _coyoteTimer = CoyoteTime;
            else
                _coyoteTimer = Mathf.Max(0f, _coyoteTimer - dt);

            if (jumpStartRequested)
                _jumpBufferTimer = JumpBufferTime;
            else
                _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - dt);

            if (_jumpBufferTimer > 0f && _coyoteTimer > 0f)
            {
                _motor.SetVerticalVelocity(JumpVelocity);
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;
            }

            if (CutJumpOnRelease && _wasJumpSustainRequested && !jumpSustainRequested && _motor.Velocity.Y < 0f)
                _motor.SetVerticalVelocity(_motor.Velocity.Y * JumpCutMultiplier);

            _wasJumpSustainRequested = jumpSustainRequested;
            ClearFrameIntents();
        }

        public void SetIntent(JumpIntent2D intent)
        {
            RawIntent = intent;
        }

        public void ApproveIntent(JumpIntent2D intent)
        {
            ApprovedIntent = intent;
        }

        public void ClearFrameIntents()
        {
            RawIntent = JumpIntent2D.None;
            ApprovedIntent = JumpIntent2D.None;
        }
    }
}
