using Framework;
using Godot;

namespace GameLogic
{
    public readonly struct MoveIntent2D
    {
        public MoveIntent2D(float axisX)
        {
            AxisX = Mathf.Clamp(axisX, -1f, 1f);
            HasInput = !Mathf.IsZeroApprox(AxisX);
        }

        public float AxisX { get; }
        public bool HasInput { get; }

        public static MoveIntent2D None => new(0f);
    }

    [GlobalClass]
    public partial class CharacterMoveComponent2D : Component2D, ICharacterIntentAbility2D<MoveIntent2D>
    {
        public override int Priority => ComponentPriority.Movement;

        [Export] public float MoveSpeed { get; set; } = 280f;
        [Export] public NodePath VisualRootPath { get; set; } = new("VisualRoot");

        public MoveIntent2D RawIntent { get; private set; } = MoveIntent2D.None;
        public MoveIntent2D ApprovedIntent { get; private set; } = MoveIntent2D.None;
        public Vector2 Velocity => _motor?.Velocity ?? Vector2.Zero;
        public float InputX { get; private set; }
        public bool IsOnGround => _motor?.IsOnFloor ?? false;
        public int Facing { get; private set; } = 1;

        private Node2D _visualRoot;
        private CharacterBodyMotorComponent2D _motor;

        public override void OnInit()
        {
            _visualRoot = Owner.GetNodeOrNull<Node2D>(VisualRootPath);
            _motor = Owner.GetComponent<CharacterBodyMotorComponent2D>();

            if (_motor == null)
                Debugger.Warn("[CharacterMoveComponent2D] Missing CharacterBodyMotorComponent2D.");
        }

        public override void OnPhysicsUpdate(double delta)
        {
            if (_motor == null)
            {
                ClearFrameIntents();
                return;
            }

            InputX = ApprovedIntent.AxisX;
            UpdateFacing();
            _motor.SetHorizontalVelocity(InputX * MoveSpeed);
            ClearFrameIntents();
        }

        public void SetIntent(MoveIntent2D intent)
        {
            RawIntent = intent;
        }

        public void ApproveIntent(MoveIntent2D intent)
        {
            ApprovedIntent = intent;
        }

        public void ClearFrameIntents()
        {
            RawIntent = MoveIntent2D.None;
            ApprovedIntent = MoveIntent2D.None;
        }

        private void UpdateFacing()
        {
            if (Mathf.Abs(InputX) <= 0.01f)
                return;

            Facing = InputX < 0f ? -1 : 1;

            if (_visualRoot != null)
            {
                _visualRoot.Scale = new Vector2(Facing, 1f);
                return;
            }

            Owner.Scale = new Vector2(Facing, 1f);
        }
    }
}
