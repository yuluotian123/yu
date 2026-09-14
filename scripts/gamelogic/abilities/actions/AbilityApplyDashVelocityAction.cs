using Godot;

namespace GameLogic
{
    public class AbilityApplyDashVelocityAction : GraphActionBase
    {
        public GraphActionComponentReference Movement { get; set; } = new();
        public float Speed { get; set; } = 2000f;
        public bool StopVerticalVelocity { get; set; } = true;

        public override string Description => "Apply Dash Velocity";

        public override void Execute(GraphExecutionContext context)
        {
            Execute(new GraphActionInvocation(context));
        }

        public override void Execute(GraphActionInvocation invocation)
        {
            CharacterMovementComponent2D movement = null;
            string error = string.Empty;
            bool hasInput = invocation.Execution?.ActionDependencyMode == GraphActionDependencyMode.HostBound &&
                            invocation.TryGetInput("Component", out movement);
            if (!hasInput &&
                !GraphActionComponentResolver.TryResolve(invocation.Execution, Movement, nameof(AbilityApplyDashVelocityAction), out movement, out error))
            {
                GD.PushError($"[AbilityApplyDashVelocityAction] {error}");
                return;
            }

            float direction = ResolveDirection(movement);
            float velocityY = StopVerticalVelocity ? 0f : movement.Velocity.Y;
            movement.RequestVelocityOverride(new CharacterMovementOverride2D(
                new Vector2(Mathf.Sign(direction) * Speed, velocityY),
                overrideHorizontal: true,
                overrideVertical: StopVerticalVelocity,
                priority: 100));
        }

        private static float ResolveDirection(CharacterMovementComponent2D movement)
        {
            if (movement != null && Mathf.Abs(movement.MoveInputX) > 0.01f)
                return Mathf.Sign(movement.MoveInputX);

            if (movement != null && Mathf.Abs(movement.RawMoveInputX) > 0.01f)
                return Mathf.Sign(movement.RawMoveInputX);

            if (movement != null)
            {
                if (movement.Facing != 0)
                    return movement.Facing >= 0 ? 1f : -1f;
            }

            return 1f;
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 4);
            root.AddChild(Movement.CreateEditUI("Movement Component", context, () => { }));

            root.AddChild(GraphEditorUi.BuildSpinRow(
                "Speed",
                Speed,
                0,
                999999,
                10,
                value => Speed = (float)value));
            root.AddChild(GraphEditorUi.BuildCheckRow(
                "Stop Vertical Velocity",
                StopVerticalVelocity,
                value => StopVerticalVelocity = value));

            return root;
        }
    }
}
