using Godot;

namespace GameLogic
{
    public class SkillApplyDashVelocityAction : GraphActionBase
    {
        public string MoveAxisBlackboardKey { get; set; } = CharacterHfsmBlackboardKeys.MoveAxisX;
        public float Speed { get; set; } = 2000f;
        public bool StopVerticalVelocity { get; set; } = true;

        public override string Description => "Apply Dash Velocity";

        public override void Execute(GraphExecutionContext context)
        {
            GameObject2D owner = SkillActionRuntimeHelper.GetGameObject(context);
            CharacterBodyMotorComponent2D motor = owner?.GetComponent<CharacterBodyMotorComponent2D>();
            if (motor == null)
                return;

            float direction = ResolveDirection(context, owner);
            float velocityY = StopVerticalVelocity ? 0f : motor.Velocity.Y;
            motor.Velocity = new Vector2(Mathf.Sign(direction) * Speed, velocityY);
        }

        private float ResolveDirection(GraphExecutionContext context, GameObject2D owner)
        {
            CharacterMoveComponent2D move = owner?.GetComponent<CharacterMoveComponent2D>();

            if (move?.ApprovedIntent.HasInput == true)
                return Mathf.Sign(move.ApprovedIntent.AxisX);

            if (move?.RawIntent.HasInput == true)
                return Mathf.Sign(move.RawIntent.AxisX);

            float axisX = context?.Blackboard.GetValue(MoveAxisBlackboardKey, 0f) ?? 0f;
            if (Mathf.Abs(axisX) > 0.01f)
                return Mathf.Sign(axisX);

            if (move != null)
            {
                if (Mathf.Abs(move.InputX) > 0.01f)
                    return Mathf.Sign(move.InputX);

                if (move.Facing != 0)
                    return move.Facing >= 0 ? 1f : -1f;
            }

            return 1f;
        }
    }
}
