using Godot;

namespace GameLogic
{
    public readonly struct AttackIntent2D
    {
        public AttackIntent2D(bool startRequested, bool sustainRequested)
        {
            StartRequested = startRequested;
            SustainRequested = sustainRequested;
        }

        public bool StartRequested { get; }
        public bool SustainRequested { get; }

        public static AttackIntent2D None => new(false, false);
    }

    [GlobalClass]
    public partial class CharacterAttackComponent2D : Component2D, ICharacterIntentAbility2D<AttackIntent2D>, IHfsmStateHandler
    {
        public override int Priority => ComponentPriority.Combat;

        [Export] public float AttackDuration { get; set; } = 0.22f;
        [Export] public float AttackCooldown { get; set; } = 0.28f;
        [Export] public NodePath VisualRootPath { get; set; } = new("VisualRoot");
        [Export] public Vector2 SlashOffset { get; set; } = new(24f, -6f);
        [Export] public Vector2 SlashScale { get; set; } = new(1f, 1f);

        public AttackIntent2D RawIntent { get; private set; } = AttackIntent2D.None;
        public AttackIntent2D ApprovedIntent { get; private set; } = AttackIntent2D.None;
        public bool IsAttacking { get; private set; }
        public bool AttackStartedThisFrame { get; private set; }
        public bool AttackFinishedThisFrame { get; private set; }
        public float AttackTimeRemaining => _attackTimer;
        public float AttackCooldownRemaining => _cooldownTimer;
        public bool CanStartAttack => !IsAttacking && _cooldownTimer <= 0f;

        private Node2D _visualRoot;
        private Polygon2D _slashVisual;
        private HfsmComponent2D _hfsm;
        private float _attackTimer;
        private float _cooldownTimer;

        public override void OnInit()
        {
            _visualRoot = Owner.GetNodeOrNull<Node2D>(VisualRootPath) ?? Owner;
            _hfsm = Owner.GetComponent<HfsmComponent2D>();
            EnsureSlashVisual();
        }

        public override void OnPhysicsUpdate(double delta)
        {
            float dt = (float)delta;
            AttackStartedThisFrame = false;
            AttackFinishedThisFrame = false;
            _cooldownTimer = Mathf.Max(0f, _cooldownTimer - dt);

            if (ApprovedIntent.StartRequested)
                TryStartAttack();

            if (IsAttacking)
                UpdateAttack(dt);

            WriteHfsmOutputs();
            ClearFrameIntents();
        }

        public override void OnDestroy()
        {
            if (GodotObject.IsInstanceValid(_slashVisual))
                _slashVisual.QueueFree();
        }

        public void SetIntent(AttackIntent2D intent)
        {
            RawIntent = intent;
        }

        public void ApproveIntent(AttackIntent2D intent)
        {
            ApprovedIntent = intent;
        }

        public void ClearFrameIntents()
        {
            RawIntent = AttackIntent2D.None;
            ApprovedIntent = AttackIntent2D.None;
        }

        public bool TryStartAttack()
        {
            if (!CanStartAttack)
                return false;

            StartAttack();
            return true;
        }

        public void OnHfsmStateEnter(HfsmRuntime runtime, IHfsmStateNodeData state)
        {
            TryStartAttack();
        }

        public void OnHfsmStateUpdate(HfsmRuntime runtime, IHfsmStateNodeData state, double delta)
        {
        }

        public void OnHfsmStateExit(HfsmRuntime runtime, IHfsmStateNodeData state)
        {
            if (IsAttacking)
                CancelAttack();
        }

        private void StartAttack()
        {
            IsAttacking = true;
            AttackStartedThisFrame = true;
            _attackTimer = Mathf.Max(0.01f, AttackDuration);
            _cooldownTimer = AttackCooldown;
            SetSlashVisible(true);
        }

        private void UpdateAttack(float dt)
        {
            _attackTimer = Mathf.Max(0f, _attackTimer - dt);
            UpdateSlashVisual();

            if (_attackTimer <= 0f)
                FinishAttack();
        }

        private void FinishAttack()
        {
            IsAttacking = false;
            AttackFinishedThisFrame = true;
            SetSlashVisible(false);
        }

        private void CancelAttack()
        {
            IsAttacking = false;
            _attackTimer = 0f;
            SetSlashVisible(false);
        }

        private void EnsureSlashVisual()
        {
            if (_visualRoot == null)
                return;

            _slashVisual = _visualRoot.GetNodeOrNull<Polygon2D>("AttackSlash");
            if (_slashVisual == null)
            {
                _slashVisual = new Polygon2D
                {
                    Name = "AttackSlash",
                    Polygon = new[]
                    {
                        new Vector2(0f, -24f),
                        new Vector2(52f, -14f),
                        new Vector2(72f, 0f),
                        new Vector2(52f, 14f),
                        new Vector2(0f, 24f)
                    },
                    Color = new Color(1f, 0.42f, 0.18f, 0.72f),
                    ZIndex = 20,
                    Visible = false
                };

                _visualRoot.AddChild(_slashVisual);
            }

            _slashVisual.Position = SlashOffset;
            _slashVisual.Scale = SlashScale;
            _slashVisual.Visible = false;
        }

        private void SetSlashVisible(bool visible)
        {
            if (_slashVisual == null)
                return;

            _slashVisual.Visible = visible;
            if (visible)
                UpdateSlashVisual();
        }

        private void UpdateSlashVisual()
        {
            if (_slashVisual == null)
                return;

            float progress = 1f - (_attackTimer / Mathf.Max(0.01f, AttackDuration));
            float alpha = Mathf.Lerp(0.75f, 0.2f, progress);
            _slashVisual.Color = new Color(1f, 0.42f, 0.18f, alpha);
            _slashVisual.Scale = SlashScale * Mathf.Lerp(0.9f, 1.18f, progress);
        }

        private void WriteHfsmOutputs()
        {
            if (_hfsm == null)
                return;

            _hfsm.SetValue(CharacterHfsmBlackboardKeys.AttackActive, IsAttacking);
            _hfsm.SetValue(CharacterHfsmBlackboardKeys.AttackFinished, AttackFinishedThisFrame);
        }
    }
}
