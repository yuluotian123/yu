namespace GameLogic
{
    public class CharacterAiTurnAroundAction : BehaviorTreeActionBase
    {
        public override string Description => "Turn Around";

        public override void Execute(GraphExecutionContext context)
        {
            SimpleAICharacterControllerComponent2D ai = CharacterBehaviorTreeContext.GetAi(context);
            if (ai == null)
                return;

            ai.Direction = ai.Direction >= 0 ? -1 : 1;
            ai.TurnPauseTimer = ai.TurnPauseDuration;
            ai.SetFrameMoveAxis(0f);
        }
    }
}
