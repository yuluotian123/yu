using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class SimpleAICharacterControllerComponent2D : Component2D
    {
        public override int Priority => ComponentPriority.AI;

        [Export] public BehaviorTreeGraphAsset Graph { get; set; }
        [Export] public bool UpdateInPhysics { get; set; } = true;

        [ExportGroup("Patrol")]
        [Export] public float PatrolDistance { get; set; } = 120f;
        [Export] public int StartDirection { get; set; } = 1;
        [Export] public bool ReverseAtEdges { get; set; } = true;
        [Export] public float EdgeLookAhead { get; set; } = 18f;
        [Export] public float TurnPauseDuration { get; set; } = 0.12f;

        [ExportGroup("Jump")]
        [Export] public float JumpInterval { get; set; } = 1.8f;
        [Export] public float JumpSustainDuration { get; set; } = 0.12f;

        private AbilitySystemComponent2D _abilities;
        private CharacterMovementComponent2D _movement;
        private Vector2 _spawnPosition;
        private int _direction;
        private float _turnPauseTimer;
        private float _jumpCooldownTimer;
        private float _jumpSustainTimer;
        private float _frameMoveAxis;
        private bool _frameJumpStartRequested;
        private bool _frameJumpSustainRequested;

        public BehaviorTreeRuntime Runtime { get; private set; }
        public CharacterMovementComponent2D Movement => _movement;
        public int Direction
        {
            get => _direction;
            set => _direction = value;
        }

        public Vector2 SpawnPosition => _spawnPosition;
        public float TurnPauseTimer
        {
            get => _turnPauseTimer;
            set => _turnPauseTimer = Mathf.Max(0f, value);
        }

        public float JumpCooldownTimer
        {
            get => _jumpCooldownTimer;
            set => _jumpCooldownTimer = Mathf.Max(0f, value);
        }

        public float JumpSustainTimer
        {
            get => _jumpSustainTimer;
            set => _jumpSustainTimer = Mathf.Max(0f, value);
        }

        protected virtual string LogPrefix => nameof(SimpleAICharacterControllerComponent2D);

        public override void OnInit()
        {
            _movement = Owner.GetComponent<CharacterMovementComponent2D>();
            _abilities = Owner.GetComponent<AbilitySystemComponent2D>();
            _spawnPosition = Owner.GlobalPosition;
            _direction = StartDirection >= 0 ? 1 : -1;
            _jumpCooldownTimer = JumpInterval;
            _jumpSustainTimer = 0f;

            if (Graph == null)
            {
                GD.PushWarning($"[{LogPrefix}] Graph is not assigned.");
                return;
            }

            Runtime = new BehaviorTreeRuntime(Graph);
            Runtime.Context.UserData.Add(Owner);
            Runtime.Context.UserData.Add(this);

            if (!Runtime.Start())
                GD.PushWarning($"[{LogPrefix}] Failed to start BehaviorTree: {Graph.ResourcePath}");
        }

        public override void OnUpdate(double delta)
        {
            if (!UpdateInPhysics)
                Tick(delta);
        }

        public override void OnPhysicsUpdate(double delta)
        {
            if (UpdateInPhysics)
                Tick(delta);
        }

        public override void OnDestroy()
        {
            Runtime?.Stop();
            Runtime = null;
            _abilities = null;
            _movement = null;
        }

        public void SetFrameMoveAxis(float axis) => _frameMoveAxis = axis;
        public void RequestFrameJumpStart() => _frameJumpStartRequested = true;
        public void SetFrameJumpSustain(bool requested) => _frameJumpSustainRequested = requested;
        public void RequestAction(string actionId, int priority = 0)
        {
            _abilities?.TryActivateAbility(actionId, "BehaviorTree");
        }

        private void Tick(double delta)
        {
            ResetFrameIntent();
            Runtime?.Update(delta);
            CommitFrameIntent();
        }

        private void ResetFrameIntent()
        {
            _frameMoveAxis = 0f;
            _frameJumpStartRequested = false;
            _frameJumpSustainRequested = false;
        }

        private void CommitFrameIntent()
        {
            _movement?.SubmitCommand(new CharacterCommand2D(
                _frameMoveAxis,
                _frameJumpStartRequested,
                _frameJumpSustainRequested), ComponentPriority.AI);
        }
    }
}
