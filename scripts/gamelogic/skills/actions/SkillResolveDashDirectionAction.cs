using Godot;

namespace GameLogic
{
    public class SkillResolveDashDirectionAction : GraphActionBase
    {
        public string DirectionBlackboardKey { get; set; } = "Skill.DashDirection";
        public string MoveAxisBlackboardKey { get; set; } = CharacterHfsmBlackboardKeys.MoveAxisX;

        public override string Description => "Resolve Dash Direction";

        public override void Execute(GraphExecutionContext context)
        {
            if (context == null)
                return;

            float axisX = context.Blackboard.GetValue(MoveAxisBlackboardKey, 0f);
            float direction = Mathf.Abs(axisX) > 0.01f ? Mathf.Sign(axisX) : 0f;

            if (Mathf.IsZeroApprox(direction))
            {
                CharacterMoveComponent2D move = SkillActionRuntimeHelper.GetGameObject(context)?.GetComponent<CharacterMoveComponent2D>();
                if (move != null)
                    direction = move.Facing >= 0 ? 1f : -1f;
            }

            if (Mathf.IsZeroApprox(direction))
                direction = 1f;

            context.Blackboard.SetValue(DirectionBlackboardKey, direction);
        }
    }
}
