namespace GameLogic
{
    public class CharacterAiShouldTurnCondition : BehaviorTreeConditionBase
    {
        public override string Description => "Character should turn";

        public override bool IsMet(GraphExecutionContext context)
        {
            SimpleAICharacterControllerComponent2D ai = CharacterBehaviorTreeContext.GetAi(context);
            if (ai?.Movement == null || !ai.Movement.IsOnFloor || ai.TurnPauseTimer > 0f)
                return false;

            float offsetX = ai.Owner.GlobalPosition.X - ai.SpawnPosition.X;
            bool reachedPatrolEdge = ai.Direction >= 0
                ? offsetX >= ai.PatrolDistance
                : offsetX <= -ai.PatrolDistance;
            bool noGroundAhead = ai.ReverseAtEdges &&
                !ai.Movement.HasGroundAhead(ai.Direction, ai.EdgeLookAhead);

            return reachedPatrolEdge || noGroundAhead;
        }
    }
}
