using Framework;
using Godot;
using Godot.Collections;

namespace GameLogic
{
    public readonly struct CharacterMovementOverride2D
    {
        public CharacterMovementOverride2D(
            Vector2 velocity,
            bool overrideHorizontal = true,
            bool overrideVertical = true,
            int priority = 0)
        {
            Velocity = velocity;
            OverrideHorizontal = overrideHorizontal;
            OverrideVertical = overrideVertical;
            Priority = priority;
        }

        public Vector2 Velocity { get; }
        public bool OverrideHorizontal { get; }
        public bool OverrideVertical { get; }
        public int Priority { get; }
    }

    [GlobalClass]
    public partial class CharacterMovementComponent2D : Component2D
    {
        public override int Priority => ComponentPriority.Movement;

        [Export] public CharacterMovementProfile Profile { get; set; }
        [Export] public bool StartDisabled { get; set; }

        [ExportGroup("Scene")]
        [Export] public NodePath BodyPath { get; set; } = new("PhysicsBody");
        [Export] public NodePath VisualRootPath { get; set; } = new("VisualRoot");

        [ExportGroup("Ground Probe")]
        [Export] public Vector2 BodySize { get; set; } = new(40f, 72f);
        [Export] public float FootInset { get; set; } = 4f;
        [Export] public float GroundProbeDistance { get; set; } = 24f;
        [Export(PropertyHint.Layers2DPhysics)] public uint GroundProbeCollisionMask { get; set; } = uint.MaxValue;

        public CharacterBody2D Body { get; private set; }
        public Vector2 Velocity
        {
            get => Body?.Velocity ?? Vector2.Zero;
            set
            {
                if (Body != null)
                    Body.Velocity = value;
            }
        }

        public bool IsOnFloor => Body?.IsOnFloor() ?? false;
        public CharacterMovementMode MovementMode { get; private set; }
        public bool MovementLocked { get; private set; }
        public bool JumpLocked { get; private set; }
        public float RawMoveInputX { get; private set; }
        public float MoveInputX { get; private set; }
        public int Facing { get; private set; } = 1;
        public float HalfWidth => BodySize.X * 0.5f;
        public float HalfHeight => BodySize.Y * 0.5f;

        private CharacterCommandBufferComponent2D _commands;
        private HfsmComponent2D _hfsm;
        private SkillManagerComponent2D _skills;
        private CharacterMovementProfile _settings;
        private Node2D _visualRoot;
        private float _jumpBufferTimer;
        private float _coyoteTimer;
        private bool _wasJumpSustainRequested;
        private bool _hasVelocityOverride;
        private CharacterMovementOverride2D _velocityOverride;

        public override void OnInit()
        {
            _commands = Owner.GetComponent<CharacterCommandBufferComponent2D>();
            _hfsm = Owner.GetComponent<HfsmComponent2D>();
            _skills = Owner.GetComponent<SkillManagerComponent2D>();
            _settings = Profile ?? new CharacterMovementProfile();
            _visualRoot = Owner.GetNodeOrNull<Node2D>(VisualRootPath);
            Body = Owner.GetNodeOrNull<CharacterBody2D>(BodyPath);

            if (Body == null)
            {
                Debugger.Warn("[CharacterMovementComponent2D] Missing CharacterBody2D child.");
                return;
            }

            Body.GlobalPosition = Owner.GlobalPosition;
            Body.Position = Vector2.Zero;
            Body.FloorSnapLength = Settings.FloorSnapLength;
            MovementMode = StartDisabled ? CharacterMovementMode.Disabled : CharacterMovementMode.Falling;
        }

        public override void OnPhysicsUpdate(double delta)
        {
            if (Body == null)
            {
                ClearFrameState();
                return;
            }

            float dt = Mathf.Max(0f, (float)delta);
            CharacterCommand2D command = _commands?.Consume() ?? CharacterCommand2D.None;
            RawMoveInputX = command.MoveAxisX;

            MovementLocked = StartDisabled ||
                _skills?.ActiveSkillBlocksMovement == true;
            JumpLocked = StartDisabled || MovementLocked ||
                _skills?.ActiveSkillBlocksJump == true;

            MovementMode = StartDisabled
                ? CharacterMovementMode.Disabled
                : (IsOnFloor ? CharacterMovementMode.Walking : CharacterMovementMode.Falling);
            MoveInputX = MovementLocked ? 0f : RawMoveInputX;

            UpdateFacing();
            UpdateHorizontalVelocity(dt);
            UpdateJump(command, dt);
            UpdateGravity(dt);
            ApplyVelocityOverride();

            Body.MoveAndSlide();
            Owner.GlobalPosition = Body.GlobalPosition;
            Body.Position = Vector2.Zero;

            MovementMode = StartDisabled
                ? CharacterMovementMode.Disabled
                : (IsOnFloor ? CharacterMovementMode.Walking : CharacterMovementMode.Falling);
            PublishBlackboard();
            ClearFrameState();
        }

        public override void OnDestroy()
        {
            _commands = null;
            _hfsm = null;
            _skills = null;
            _settings = null;
            _visualRoot = null;
            Body = null;
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _hasVelocityOverride = false;
        }

        public void SetEnabled(bool enabled)
        {
            StartDisabled = !enabled;
            if (!enabled)
            {
                MovementMode = CharacterMovementMode.Disabled;
                Velocity = Vector2.Zero;
            }
        }

        public void RequestVelocityOverride(CharacterMovementOverride2D request)
        {
            if (_hasVelocityOverride && request.Priority < _velocityOverride.Priority)
                return;

            _velocityOverride = request;
            _hasVelocityOverride = true;
        }

        public void SetHorizontalVelocity(float velocityX)
        {
            Velocity = new Vector2(velocityX, Velocity.Y);
        }

        public void SetVerticalVelocity(float velocityY)
        {
            Velocity = new Vector2(Velocity.X, velocityY);
        }

        public void RestoreFacing(int facing)
        {
            Facing = facing < 0 ? -1 : 1;
            ApplyFacing();
        }

        public void SyncBodyToOwner()
        {
            if (Body == null || Owner == null)
                return;

            Body.GlobalPosition = Owner.GlobalPosition;
            Body.Position = Vector2.Zero;
        }

        public bool HasGroundAhead(float direction, float lookAheadDistance)
        {
            if (Body == null)
                return false;

            float signedDirection = Mathf.IsZeroApprox(direction) ? 0f : Mathf.Sign(direction);
            Vector2 rayStart = new(
                Body.GlobalPosition.X + signedDirection * (HalfWidth + lookAheadDistance),
                Body.GlobalPosition.Y + HalfHeight - FootInset);
            Vector2 rayEnd = rayStart + Vector2.Down * GroundProbeDistance;

            var query = PhysicsRayQueryParameters2D.Create(rayStart, rayEnd, GroundProbeCollisionMask);
            query.Exclude = new Array<Rid> { Body.GetRid() };
            return Body.GetWorld2D().DirectSpaceState.IntersectRay(query).Count > 0;
        }

        private CharacterMovementProfile Settings => _settings ??= Profile ?? new CharacterMovementProfile();

        private void UpdateHorizontalVelocity(float delta)
        {
            float targetVelocity = MoveInputX * Settings.MoveSpeed;
            float control = IsOnFloor ? 1f : Settings.AirControl;
            float rate = Mathf.Abs(targetVelocity) > 0.01f
                ? Settings.Acceleration * control
                : Settings.Deceleration;
            SetHorizontalVelocity(Mathf.MoveToward(Velocity.X, targetVelocity, rate * delta));
        }

        private void UpdateJump(CharacterCommand2D command, float delta)
        {
            if (IsOnFloor)
                _coyoteTimer = Settings.CoyoteTime;
            else
                _coyoteTimer = Mathf.Max(0f, _coyoteTimer - delta);

            if (!JumpLocked && command.JumpStartRequested)
                _jumpBufferTimer = Settings.JumpBufferTime;
            else
                _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - delta);

            if (!JumpLocked && _jumpBufferTimer > 0f && _coyoteTimer > 0f)
            {
                SetVerticalVelocity(Settings.JumpVelocity);
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;
            }

            bool sustainRequested = !JumpLocked && command.JumpSustainRequested;
            if (Settings.CutJumpOnRelease &&
                _wasJumpSustainRequested &&
                !sustainRequested &&
                Velocity.Y < 0f)
            {
                SetVerticalVelocity(Velocity.Y * Settings.JumpCutMultiplier);
            }

            _wasJumpSustainRequested = sustainRequested;
        }

        private void UpdateGravity(float delta)
        {
            if (MovementMode == CharacterMovementMode.Disabled)
            {
                SetVerticalVelocity(0f);
                return;
            }

            if (IsOnFloor)
            {
                if (Velocity.Y > 0f)
                    SetVerticalVelocity(0f);
                return;
            }

            SetVerticalVelocity(Mathf.Min(Velocity.Y + Settings.Gravity * delta, Settings.MaxFallSpeed));
        }

        private void ApplyVelocityOverride()
        {
            if (!_hasVelocityOverride)
                return;

            Vector2 velocity = Velocity;
            if (_velocityOverride.OverrideHorizontal)
                velocity.X = _velocityOverride.Velocity.X;
            if (_velocityOverride.OverrideVertical)
                velocity.Y = _velocityOverride.Velocity.Y;
            Velocity = velocity;
        }

        private void UpdateFacing()
        {
            if (Mathf.Abs(MoveInputX) <= 0.01f)
                return;

            Facing = MoveInputX < 0f ? -1 : 1;
            ApplyFacing();
        }

        private void ApplyFacing()
        {
            if (_visualRoot != null)
                _visualRoot.Scale = new Vector2(Facing, 1f);
            else if (Owner != null)
                Owner.Scale = new Vector2(Facing, 1f);
        }

        private void PublishBlackboard()
        {
            _hfsm?.SetValue(CharacterGraphBlackboardKeys.MovementMode, MovementMode.ToString());
            _hfsm?.SetValue(CharacterGraphBlackboardKeys.MovementIsOnFloor, IsOnFloor);
            _hfsm?.SetValue(CharacterGraphBlackboardKeys.MovementMoveAxisX, MoveInputX);
            _hfsm?.SetValue(CharacterGraphBlackboardKeys.MovementVelocityY, Velocity.Y);
        }

        private void ClearFrameState()
        {
            _hasVelocityOverride = false;
            _velocityOverride = default;
        }
    }
}
