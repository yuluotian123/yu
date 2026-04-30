using Godot;

namespace GameLogic
{
    public readonly struct DashIntent2D
    {
        public DashIntent2D(bool startRequested, float directionX)
        {
            StartRequested = startRequested;
            DirectionX = Mathf.Clamp(directionX, -1f, 1f);
        }

        public bool StartRequested { get; }
        public float DirectionX { get; }

        public static DashIntent2D None => new(false, 0f);
    }

    [GlobalClass]
    public partial class CharacterDashComponent2D : Component2D, ICharacterIntentAbility2D<DashIntent2D>, IHfsmStateHandler
    {
        public override int Priority => ComponentPriority.Motor + 1;

        [Export] public float DashSpeed { get; set; } = 760f;
        [Export] public float DashDuration { get; set; } = 0.16f;
        [Export] public float DashCooldown { get; set; } = 0.45f;
        [Export] public bool StopVerticalVelocity { get; set; } = true;
        [Export] public NodePath VisualRootPath { get; set; } = new("VisualRoot");

        public DashIntent2D RawIntent { get; private set; } = DashIntent2D.None;
        public DashIntent2D ApprovedIntent { get; private set; } = DashIntent2D.None;
        public bool IsDashing { get; private set; }
        public bool DashStartedThisFrame { get; private set; }
        public bool DashFinishedThisFrame { get; private set; }
        public float DashTimeRemaining => _dashTimer;
        public float DashCooldownRemaining => _cooldownTimer;
        public bool CanStartDash => !IsDashing && _cooldownTimer <= 0f;
        public float DashDirection { get; private set; } = 1f;

        private CharacterBodyMotorComponent2D _motor;
        private CharacterMoveComponent2D _move;
        private HfsmComponent2D _hfsm;
        private CanvasItem _visualRoot;
        private Color _defaultModulate = Colors.White;
        private float _dashTimer;
        private float _cooldownTimer;

        public override void OnInit()
        {
            _motor = Owner.GetComponent<CharacterBodyMotorComponent2D>();
            _move = Owner.GetComponent<CharacterMoveComponent2D>();
            _hfsm = Owner.GetComponent<HfsmComponent2D>();
            _visualRoot = Owner.GetNodeOrNull<CanvasItem>(VisualRootPath);

            if (_visualRoot != null)
                _defaultModulate = _visualRoot.Modulate;
        }

        public override void OnPhysicsUpdate(double delta)
        {
            float dt = (float)delta;
            DashStartedThisFrame = false;
            DashFinishedThisFrame = false;
            _cooldownTimer = Mathf.Max(0f, _cooldownTimer - dt);

            if (ApprovedIntent.StartRequested)
                TryStartDash(ApprovedIntent.DirectionX);

            if (IsDashing)
                UpdateDash(dt);

            WriteHfsmOutputs();
            ClearFrameIntents();
        }

        public override void OnDestroy()
        {
            SetDashVisual(false);
        }

        public void SetIntent(DashIntent2D intent)
        {
            RawIntent = intent;
        }

        public void ApproveIntent(DashIntent2D intent)
        {
            ApprovedIntent = intent;
        }

        public void ClearFrameIntents()
        {
            RawIntent = DashIntent2D.None;
            ApprovedIntent = DashIntent2D.None;
        }

        public bool TryStartDash(float requestedDirection)
        {
            if (!CanStartDash)
                return false;

            StartDash(requestedDirection);
            return true;
        }

        public void OnHfsmStateEnter(HfsmRuntime runtime, IHfsmStateNodeData state)
        {
            TryStartDash(RawIntent.DirectionX);
        }

        public void OnHfsmStateUpdate(HfsmRuntime runtime, IHfsmStateNodeData state, double delta)
        {
        }

        public void OnHfsmStateExit(HfsmRuntime runtime, IHfsmStateNodeData state)
        {
            if (IsDashing)
                CancelDash();
        }

        private void StartDash(float requestedDirection)
        {
            DashDirection = ResolveDirection(requestedDirection);
            IsDashing = true;
            DashStartedThisFrame = true;
            _dashTimer = Mathf.Max(0.01f, DashDuration);
            _cooldownTimer = DashCooldown;
            SetDashVisual(true);
        }

        private void UpdateDash(float dt)
        {
            if (_motor != null)
            {
                float velocityY = StopVerticalVelocity ? 0f : _motor.Velocity.Y;
                _motor.Velocity = new Vector2(DashDirection * DashSpeed, velocityY);
            }

            _dashTimer = Mathf.Max(0f, _dashTimer - dt);
            if (_dashTimer <= 0f)
                FinishDash();
        }

        private void FinishDash()
        {
            IsDashing = false;
            DashFinishedThisFrame = true;
            SetDashVisual(false);
        }

        private void CancelDash()
        {
            IsDashing = false;
            _dashTimer = 0f;
            SetDashVisual(false);
        }

        private float ResolveDirection(float requestedDirection)
        {
            if (Mathf.Abs(requestedDirection) > 0.01f)
                return Mathf.Sign(requestedDirection);

            if (_move != null)
                return _move.Facing >= 0 ? 1f : -1f;

            return DashDirection >= 0f ? 1f : -1f;
        }

        private void SetDashVisual(bool enabled)
        {
            if (_visualRoot == null)
                return;

            _visualRoot.Modulate = enabled
                ? new Color(0.55f, 0.95f, 1f, 1f)
                : _defaultModulate;
        }

        private void WriteHfsmOutputs()
        {
            if (_hfsm == null)
                return;

            _hfsm.SetValue(CharacterHfsmBlackboardKeys.DashActive, IsDashing);
            _hfsm.SetValue(CharacterHfsmBlackboardKeys.DashFinished, DashFinishedThisFrame);
        }
    }
}
