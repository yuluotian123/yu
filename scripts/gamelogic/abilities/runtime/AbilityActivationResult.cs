namespace GameLogic
{
    public enum AbilityActivationResult
    {
        Activated,
        InvalidAbilityId,
        NotGranted,
        OnCooldown,
        AlreadyActive,
        BlockedByCurrentAbility,
        InvalidContext
    }
}
