namespace GameLogic
{
    public class CharacterAiApplyPatrolMoveAction : BehaviorTreeActionBase
    {
        public override string Description => "Apply Patrol Move";

        public override BehaviorTreeStatus Tick(
            BehaviorTreeRuntime runtime,
            GraphExecutionContext context,
            double delta)
        {
            SimpleAICharacterControllerComponent2D ai = CharacterBehaviorTreeContext.GetAi(context);
            if (ai == null)
                return BehaviorTreeStatus.Failure;

            if (ai.TurnPauseTimer > 0f)
            {
                ai.TurnPauseTimer -= (float)delta;
                ai.SetFrameMoveAxis(0f);
                return BehaviorTreeStatus.Success;
            }

            ai.SetFrameMoveAxis(ai.Direction);
            return BehaviorTreeStatus.Success;
        }
    }
}
