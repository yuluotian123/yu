using Godot;

namespace GameLogic
{
    public class SkillApplyDashVelocityAction : GraphActionBase
    {
        public string MoveAxisBlackboardKey { get; set; } = CharacterGraphBlackboardKeys.CommandMoveAxisX;
        public float Speed { get; set; } = 2000f;
        public bool StopVerticalVelocity { get; set; } = true;

        public override string Description => "Apply Dash Velocity";

        public override void Execute(GraphExecutionContext context)
        {
            GameObject2D owner = SkillActionRuntimeHelper.GetGameObject(context);
            CharacterMovementComponent2D movement = owner?.GetComponent<CharacterMovementComponent2D>();
            if (movement == null)
                return;

            float direction = ResolveDirection(context, owner);
            float velocityY = StopVerticalVelocity ? 0f : movement.Velocity.Y;
            movement.RequestVelocityOverride(new CharacterMovementOverride2D(
                new Vector2(Mathf.Sign(direction) * Speed, velocityY),
                overrideHorizontal: true,
                overrideVertical: StopVerticalVelocity,
                priority: 100));
        }

        private float ResolveDirection(GraphExecutionContext context, GameObject2D owner)
        {
            CharacterMovementComponent2D movement = owner?.GetComponent<CharacterMovementComponent2D>();

            if (movement != null && Mathf.Abs(movement.MoveInputX) > 0.01f)
                return Mathf.Sign(movement.MoveInputX);

            if (movement != null && Mathf.Abs(movement.RawMoveInputX) > 0.01f)
                return Mathf.Sign(movement.RawMoveInputX);

            float axisX = context?.Blackboard.GetValue(MoveAxisBlackboardKey, 0f) ?? 0f;
            if (Mathf.Abs(axisX) > 0.01f)
                return Mathf.Sign(axisX);

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

            root.AddChild(SkillActionEditorHelper.BuildLineEditRow(
                "Move Axis Key",
                MoveAxisBlackboardKey,
                CharacterGraphBlackboardKeys.CommandMoveAxisX,
                value => MoveAxisBlackboardKey = value));
            root.AddChild(SkillActionEditorHelper.BuildSpinRow(
                "Speed",
                Speed,
                0,
                999999,
                10,
                value => Speed = (float)value));
            root.AddChild(SkillActionEditorHelper.BuildCheckRow(
                "Stop Vertical Velocity",
                StopVerticalVelocity,
                value => StopVerticalVelocity = value));

            return root;
        }
    }
}
