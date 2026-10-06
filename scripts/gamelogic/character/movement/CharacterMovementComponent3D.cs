using Framework;
using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

namespace GameLogic
{
    public readonly struct CharacterMovementOverride3D
    {
        public CharacterMovementOverride3D(
            Vector3 velocity,
            bool overrideHorizontal = true,
            bool overrideVertical = true,
            int priority = 0)
        {
            Velocity = velocity;
            OverrideHorizontal = overrideHorizontal;
            OverrideVertical = overrideVertical;
            Priority = priority;
        }

        public Vector3 Velocity { get; }
        public bool OverrideHorizontal { get; }
        public bool OverrideVertical { get; }
        public int Priority { get; }
    }

    [GlobalClass]
    public partial class CharacterMovementComponent3D : Component3D, ICharacterAnimationVariableProvider
    {
        public override int Priority => ComponentPriority.Movement;

        [Export] public CharacterMovementProfile Profile { get; set; }
        [Export] public bool StartDisabled { get; set; }
        [Export] public CharacterMovementSpace MovementSpace { get; set; } = CharacterMovementSpace.SideView;
        public Vector3 FacingDirection { get; private set; } = Vector3.Right;
        public float MoveInputZ { get; private set; }
        [GraphValue("MoveAmount", Access = GraphValueAccess.ReadOnly)]
        public float MoveAmount => new Vector2(MoveInputX, MoveInputZ).LimitLength().Length();
        private CharacterMovementSpace? _appliedSpace;

        [ExportGroup("Scene")]
        [Export] public NodePath BodyPath { get; set; } = new("PhysicsBody");
        [Export] public NodePath VisualRootPath { get; set; } = new("VisualRoot");

        [ExportGroup("Ground Probe")]
        [Export] public Vector3 BodySize { get; set; } = new(0.4f, 0.72f, 0.4f);
        [Export] public float FootInset { get; set; } = 0.04f;
        [Export] public float GroundProbeDistance { get; set; } = 0.24f;
        [Export(PropertyHint.Layers3DPhysics)] public uint GroundProbeCollisionMask { get; set; } = uint.MaxValue;

        public CharacterBody3D Body { get; private set; }
        public Vector3 Velocity
        {
            get => Body?.Velocity ?? Vector3.Zero;
            set
            {
                if (Body != null)
                    Body.Velocity = value;
            }
        }

        [GraphValue("IsOnFloor", DisplayName = "是否在地面", Access = GraphValueAccess.ReadOnly)]
        public bool IsOnFloor => Body?.IsOnFloor() ?? false;
        public CharacterMovementMode MovementMode { get; private set; }
        public bool MovementLocked { get; private set; }
        public bool JumpLocked { get; private set; }
        public bool JumpSustainRequested => _jumpSustainRequested;
        public float RawMoveInputX { get; private set; }
        [GraphValue("MoveInputX", DisplayName = "水平移动输入", Access = GraphValueAccess.ReadOnly)]
        public float MoveInputX { get; private set; }
        [GraphValue("MovementModeName", DisplayName = "移动模式", Access = GraphValueAccess.ReadOnly)]
        public string MovementModeName => MovementMode.ToString();
        [GraphValue("VelocityY", DisplayName = "垂直速度", Access = GraphValueAccess.ReadOnly)]
        public float VelocityY => Velocity.Y;
        public int Facing { get; private set; } = 1;
        public float HalfWidth => BodySize.X * 0.5f;
        public float HalfHeight => BodySize.Y * 0.5f;

        public bool TryGetAnimationVariable(string key, out object value)
        {
            switch (key?.Trim())
            {
                case "Character.Movement.Mode":
                case "MovementModeName":
                    value = MovementModeName;
                    return true;
                case "Character.Movement.IsOnFloor":
                case "IsOnFloor":
                    value = IsOnFloor;
                    return true;
                case "Character.Movement.MoveAxisX":
                case "MoveInputX":
                    value = MoveInputX;
                    return true;
                case "Character.Movement.VelocityY":
                case "VelocityY":
                    value = VelocityY;
                    return true;
                default:
                    value = null;
                    return false;
            }
        }

        private readonly System.Collections.Generic.Dictionary<string, ControlLock> _controlLocks = new(StringComparer.Ordinal);
        private CharacterMovementProfile _settings;
        private Node3D _visualRoot;
        private float _jumpBufferTimer;
        private float _coyoteTimer;
        private bool _jumpSustainRequested;
        private bool _wasJumpSustainRequested;
        private bool _hasVelocityOverride;
        private CharacterMovementOverride3D _velocityOverride;
        private CharacterCommand3D _pendingCommand = CharacterCommand3D.None;
        private int _commandSourcePriority = int.MinValue;

        public override void OnInit()
        {
            _settings = Profile ?? new CharacterMovementProfile();
            _visualRoot = Owner.GetNodeOrNull<Node3D>(VisualRootPath);
            Body = Owner.GetNodeOrNull<CharacterBody3D>(BodyPath);

            if (Body == null)
            {
                Debugger.Warn("[CharacterMovementComponent3D] Missing CharacterBody3D child.");
                return;
            }

            Body.TopLevel = true;
            Body.GlobalPosition = Owner.GlobalPosition;

            Body.FloorSnapLength = Settings.FloorSnapLength;
            MovementMode = StartDisabled ? CharacterMovementMode.Disabled : CharacterMovementMode.Falling;
            Owner.ResetPhysicsInterpolation();
        }

        public override void OnPhysicsUpdate(double delta)
        {
            if (Body == null)
            {
                ClearFrameState();
                return;
            }

            ApplyMovementSpace();
            float dt = Mathf.Max(0f, (float)delta);
            CharacterCommand3D command = ConsumeCommand();
            RawMoveInputX = command.MoveAxisX;

            ResolveControlLocks(out bool movementLocked, out bool jumpLocked);
            MovementLocked = StartDisabled || movementLocked;
            JumpLocked = StartDisabled || MovementLocked || jumpLocked;

            MovementMode = StartDisabled
                ? CharacterMovementMode.Disabled
                : (IsOnFloor ? CharacterMovementMode.Walking : CharacterMovementMode.Falling);
            MoveInputX = MovementLocked ? 0f : RawMoveInputX;
            MoveInputZ = MovementLocked || MovementSpace == CharacterMovementSpace.SideView ? 0f : command.MoveAxisZ;

            UpdateFacing();
            UpdateHorizontalVelocity(dt);
            UpdateJump(command, dt);
            UpdateGravity(dt);
            ApplyVelocityOverride();

            Body.MoveAndSlide();
            Owner.GlobalPosition = Body.GlobalPosition;


            MovementMode = StartDisabled
                ? CharacterMovementMode.Disabled
                : (IsOnFloor ? CharacterMovementMode.Walking : CharacterMovementMode.Falling);
            ClearFrameState();
        }

        public override void OnDestroy()
        {
            _controlLocks.Clear();
            _settings = null;
            _appliedSpace = null;
            _visualRoot = null;
            Body = null;
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _jumpSustainRequested = false;
            _wasJumpSustainRequested = false;
            _hasVelocityOverride = false;
            _pendingCommand = CharacterCommand3D.None;
            _commandSourcePriority = int.MinValue;
        }

        public void SubmitCommand(CharacterCommand3D command, int sourcePriority = 0)
        {
            if (sourcePriority < _commandSourcePriority)
                return;

            _pendingCommand = command;
            _jumpSustainRequested = command.JumpSustainRequested;
            _commandSourcePriority = sourcePriority;
        }

        [GraphAction("AddMovementInput", DisplayName = "添加移动输入", UseInputEventValue = true)]
        public void AddMovementInput(float axis, int sourcePriority = 0)
        {
            if (!PreparePartialCommand(sourcePriority))
                return;
            _pendingCommand = new CharacterCommand3D(
                _pendingCommand.MoveAxisX + axis,
                _pendingCommand.JumpStartRequested,
                _pendingCommand.JumpSustainRequested, _pendingCommand.MoveAxisZ);
        }

        [GraphAction("StopMovementInput", DisplayName = "停止移动输入")]
        public void StopMovementInput(int sourcePriority = 0)
        {
            if (!PreparePartialCommand(sourcePriority))
                return;
            _pendingCommand = new CharacterCommand3D(
                0f,
                _pendingCommand.JumpStartRequested,
                _pendingCommand.JumpSustainRequested);
        }

        [GraphAction("RequestJumpStart", DisplayName = "请求跳跃")]
        public void RequestJumpStart(int sourcePriority = 0)
        {
            if (!PreparePartialCommand(sourcePriority))
                return;
            _pendingCommand = new CharacterCommand3D(
                _pendingCommand.MoveAxisX,
                true,
                true, _pendingCommand.MoveAxisZ);
            _jumpSustainRequested = true;
        }

        [GraphAction("ClearJumpInput", DisplayName = "清除跳跃输入")]
        public void ClearJumpInput(int sourcePriority = 0)
        {
            if (!PreparePartialCommand(sourcePriority)) return;
            _pendingCommand = new CharacterCommand3D(_pendingCommand.MoveAxisX, false, false, _pendingCommand.MoveAxisZ);
            _jumpSustainRequested = false;
        }

        [GraphAction("SetJumpSustain", DisplayName = "设置跳跃持续")]
        public void SetJumpSustain(bool requested, int sourcePriority = 0)
        {
            if (!PreparePartialCommand(sourcePriority))
                return;
            _pendingCommand = new CharacterCommand3D(
                _pendingCommand.MoveAxisX,
                _pendingCommand.JumpStartRequested,
                requested, _pendingCommand.MoveAxisZ);
            _jumpSustainRequested = requested;
        }

        public void SetControlLock(
            string key,
            bool blocksMovement,
            bool blocksJump,
            int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;
            _controlLocks[key.Trim()] = new ControlLock(blocksMovement, blocksJump, priority);
        }

        public void ClearControlLock(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
                _controlLocks.Remove(key.Trim());
        }

        public void SetEnabled(bool enabled)
        {
            StartDisabled = !enabled;
            if (!enabled)
            {
                MovementMode = CharacterMovementMode.Disabled;
                Velocity = Vector3.Zero;
            }
        }

        public void RequestVelocityOverride(CharacterMovementOverride3D request)
        {
            if (_hasVelocityOverride && request.Priority < _velocityOverride.Priority)
                return;

            _velocityOverride = request;
            _hasVelocityOverride = true;
        }

        public void SetHorizontalVelocity(float velocityX)
        {
            Velocity = new Vector3(velocityX, Velocity.Y, Velocity.Z);
        }

        public void SetVerticalVelocity(float velocityY)
        {
            Velocity = new Vector3(Velocity.X, velocityY, Velocity.Z);
        }

        public void RestoreFacing(int facing)
        {
            Facing = facing < 0 ? -1 : 1;
            FacingDirection = Vector3.Right * Facing;
            ApplyFacing();
        }

        public void SyncBodyToOwner()
        {
            if (Body == null || Owner == null)
                return;

            Body.TopLevel = true;
            Body.GlobalPosition = Owner.GlobalPosition;
            // 出生、回池重用、传送/读档都不能从旧位置插值到新位置。
            Owner.ResetPhysicsInterpolation();
        }

        public bool HasGroundAhead(float direction, float lookAheadDistance)
        {
            if (Body == null)
                return false;

            float signedDirection = Mathf.IsZeroApprox(direction) ? 0f : Mathf.Sign(direction);
            Vector3 rayStart = Body.GlobalPosition + new Vector3(
                signedDirection * (HalfWidth + lookAheadDistance), -HalfHeight + FootInset, 0f);
            Vector3 rayEnd = rayStart + Vector3.Down * GroundProbeDistance;

            var query = PhysicsRayQueryParameters3D.Create(rayStart, rayEnd, GroundProbeCollisionMask);
            query.Exclude = new Array<Rid> { Body.GetRid() };
            return Body.GetWorld3D().DirectSpaceState.IntersectRay(query).Count > 0;
        }

        private CharacterMovementProfile Settings => _settings ??= Profile ?? new CharacterMovementProfile();

        private void UpdateHorizontalVelocity(float delta)
        {
            Vector2 target = new Vector2(MoveInputX, MoveInputZ).LimitLength() * Settings.MoveSpeed;
            float control = IsOnFloor ? 1f : Settings.AirControl;
            float rate = target.LengthSquared() > 0.0001f ? Settings.Acceleration * control : Settings.Deceleration;
            Vector2 planar = new Vector2(Velocity.X, Velocity.Z).MoveToward(target, rate * delta);
            Velocity = new Vector3(planar.X, Velocity.Y, planar.Y);
        }

        private void UpdateJump(CharacterCommand3D command, float delta)
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
                Velocity.Y > 0f)
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
                if (Velocity.Y < 0f)
                    SetVerticalVelocity(0f);
                return;
            }

            SetVerticalVelocity(Mathf.Max(Velocity.Y - Settings.Gravity * delta, -Settings.MaxFallSpeed));
        }

        private void ApplyVelocityOverride()
        {
            if (!_hasVelocityOverride)
                return;

            Vector3 velocity = Velocity;
            if (_velocityOverride.OverrideHorizontal)
            {
                velocity.X = _velocityOverride.Velocity.X;
                velocity.Z = MovementSpace == CharacterMovementSpace.SideView ? 0f : _velocityOverride.Velocity.Z;
            }
            if (_velocityOverride.OverrideVertical)
                velocity.Y = _velocityOverride.Velocity.Y;
            Velocity = velocity;
        }

        private void UpdateFacing()
        {
            Vector3 direction = new(MoveInputX, 0f, MoveInputZ);
            if (direction.LengthSquared() > 0.0001f) FacingDirection = direction.Normalized();
            if (Mathf.Abs(MoveInputX) > 0.01f) Facing = MoveInputX < 0f ? -1 : 1;
            ApplyFacing();
        }

        private void ApplyFacing()
        {
            // Mirror only the artwork. The host and physics body keep a positive scale.
            if (_visualRoot != null)
            {
                _visualRoot.Scale = new Vector3(Facing, 1f, 1f);
                // Billboard shaders discard the sign of the model scale. Flip the UVs explicitly.
                var sprite = AbilityActionRuntimeHelper.FindFirst<SpriteBase3D>(_visualRoot);
                if (sprite != null) sprite.FlipH = Facing < 0;
            }
        }

        public void RestoreFacingDirection(Vector3 direction)
        {
            direction.Y = 0f;
            if (direction.IsFinite() && direction.LengthSquared() > 0.0001f)
                FacingDirection = direction.Normalized();
        }

        [GraphAction("AddDepthMovementInput", DisplayName = "添加前后移动输入", UseInputEventValue = true)]
        public void AddDepthMovementInput(float axis, int sourcePriority = 0)
        {
            if (!PreparePartialCommand(sourcePriority)) return;
            _pendingCommand = new CharacterCommand3D(_pendingCommand.MoveAxisX,
                _pendingCommand.JumpStartRequested, _pendingCommand.JumpSustainRequested,
                _pendingCommand.MoveAxisZ + axis);
        }

        private void ApplyMovementSpace()
        {
            if (Body == null || _appliedSpace == MovementSpace) return;
            Body.AxisLockLinearZ = MovementSpace == CharacterMovementSpace.SideView;
            if (Body.AxisLockLinearZ)
            {
                Velocity = new Vector3(Velocity.X, Velocity.Y, 0f);
                FacingDirection = Vector3.Right * Facing;
            }
            _appliedSpace = MovementSpace;
        }

        private CharacterCommand3D ConsumeCommand()
        {
            CharacterCommand3D command = new(
                _pendingCommand.MoveAxisX,
                _pendingCommand.JumpStartRequested,
                _jumpSustainRequested, _pendingCommand.MoveAxisZ);
            _pendingCommand = CharacterCommand3D.None;
            _commandSourcePriority = int.MinValue;
            return command;
        }

        private bool PreparePartialCommand(int sourcePriority)
        {
            if (sourcePriority < _commandSourcePriority)
                return false;
            if (sourcePriority > _commandSourcePriority)
                _pendingCommand = CharacterCommand3D.None;
            _commandSourcePriority = sourcePriority;
            return true;
        }

        private void ResolveControlLocks(out bool blocksMovement, out bool blocksJump)
        {
            blocksMovement = false;
            blocksJump = false;
            foreach (ControlLock controlLock in _controlLocks.Values)
            {
                blocksMovement |= controlLock.BlocksMovement;
                blocksJump |= controlLock.BlocksJump;
            }
        }

        private void ClearFrameState()
        {
            _hasVelocityOverride = false;
            _velocityOverride = default;
        }

        private readonly struct ControlLock
        {
            public ControlLock(bool blocksMovement, bool blocksJump, int priority)
            {
                BlocksMovement = blocksMovement;
                BlocksJump = blocksJump;
                Priority = priority;
            }

            public bool BlocksMovement { get; }
            public bool BlocksJump { get; }
            public int Priority { get; }
        }
    }
}

