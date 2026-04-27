using Framework;
using Godot;
using Godot.Collections;

namespace GameLogic
{
    [GlobalClass]
    public partial class CharacterBodyMotorComponent2D : Component2D
    {
        public override int Priority => ComponentPriority.Motor;

        [Export] public NodePath BodyPath { get; set; } = new("PhysicsBody");
        [Export] public float FloorSnapLength { get; set; } = 12f;
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
        public float HalfWidth => BodySize.X * 0.5f;
        public float HalfHeight => BodySize.Y * 0.5f;

        public override void OnInit()
        {
            Body = Owner.GetNodeOrNull<CharacterBody2D>(BodyPath);
            if (Body == null)
            {
                Debugger.Warn("[CharacterBodyMotorComponent2D] Missing CharacterBody2D child.");
                return;
            }

            Body.GlobalPosition = Owner.GlobalPosition;
            Body.Position = Vector2.Zero;
            Body.FloorSnapLength = FloorSnapLength;
        }

        public override void OnPhysicsUpdate(double delta)
        {
            if (Body == null)
                return;

            Body.MoveAndSlide();

            // Keep GameObject2D as the gameplay transform while CharacterBody2D handles collisions.
            Owner.GlobalPosition = Body.GlobalPosition;
            Body.Position = Vector2.Zero;
        }

        public void SetHorizontalVelocity(float velocityX)
        {
            Velocity = new Vector2(velocityX, Velocity.Y);
        }

        public void SetVerticalVelocity(float velocityY)
        {
            Velocity = new Vector2(Velocity.X, velocityY);
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
    }
}
