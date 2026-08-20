namespace GameLogic
{
    public sealed class AbilityExecutionContext
    {
        public GameObject2D GameObject { get; init; }
        public AbilitySystemComponent2D AbilitySystem { get; init; }
        public string Source { get; init; } = string.Empty;
    }
}
