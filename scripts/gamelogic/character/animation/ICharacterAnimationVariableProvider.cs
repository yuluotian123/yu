namespace GameLogic
{
    public interface ICharacterAnimationVariableProvider
    {
        bool TryGetAnimationVariable(string key, out object value);
    }
}
