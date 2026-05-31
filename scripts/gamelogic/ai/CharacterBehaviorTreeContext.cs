namespace GameLogic
{
    internal static class CharacterBehaviorTreeContext
    {
        public static SimpleAICharacterControllerComponent2D GetAi(GraphExecutionContext context)
        {
            return context?.GetUserData<SimpleAICharacterControllerComponent2D>();
        }
    }
}
