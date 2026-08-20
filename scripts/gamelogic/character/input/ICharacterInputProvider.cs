namespace GameLogic
{
    public interface ICharacterInputProvider
    {
        bool IsPressed(string action, string handlerLayer = null);
        bool IsJustPressed(string action, string handlerLayer = null);
        bool IsJustReleased(string action, string handlerLayer = null);
        bool IsBuffered(string action, float bufferTime);
        float GetActionStrength(string action, string handlerLayer = null);
        float GetHoldTime(string action);
        bool ConsumePressed(string action, string handlerLayer = null);
        bool ConsumeJustPressed(string action, string handlerLayer = null);
        bool ConsumeJustReleased(string action, string handlerLayer = null);
    }
}
