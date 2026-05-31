namespace GameLogic
{
    public class CharacterAiMaintainDirectionAction : BehaviorTreeActionBase
    {
        public override string Description => "Maintain Direction";

        public override void Execute(GraphExecutionContext context)
        {
            SimpleAICharacterControllerComponent2D ai = CharacterBehaviorTreeContext.GetAi(context);
            if (ai != null && ai.Direction == 0)
                ai.Direction = ai.StartDirection >= 0 ? 1 : -1;
        }
    }
}
