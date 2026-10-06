namespace GameLogic
{
    public sealed class AbilityExecutionContext
    {
        public IGameObject GameObject { get; init; }
        public AbilitySystemComponent3D AbilitySystem { get; init; }
        public string Source { get; init; } = string.Empty;
    }
}
