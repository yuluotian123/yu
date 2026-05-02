using Godot;

namespace GameLogic
{
    public class SkillApplyDashVelocityAction : GraphActionBase
    {
        public string DirectionBlackboardKey { get; set; } = "Skill.DashDirection";
        public float Speed { get; set; } = 760f;
        public bool StopVerticalVelocity { get; set; } = true;

        public override string Description => "Apply Dash Velocity";

        public override void Execute(GraphExecutionContext context)
        {
            CharacterBodyMotorComponent2D motor = SkillActionRuntimeHelper.GetGameObject(context)?.GetComponent<CharacterBodyMotorComponent2D>();
            if (motor == null)
                return;

            float direction = context.Blackboard.GetValue(DirectionBlackboardKey, 1f);
            if (Mathf.IsZeroApprox(direction))
                direction = 1f;

            float velocityY = StopVerticalVelocity ? 0f : motor.Velocity.Y;
            motor.Velocity = new Vector2(Mathf.Sign(direction) * Speed, velocityY);
        }
    }
}
