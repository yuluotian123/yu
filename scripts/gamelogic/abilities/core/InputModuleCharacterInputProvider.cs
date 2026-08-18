using Framework;

namespace GameLogic
{
    public sealed class InputModuleCharacterInputProvider : ICharacterInputProvider
    {
        private readonly IInputModule _input;

        public InputModuleCharacterInputProvider(IInputModule input)
        {
            _input = input;
        }

        public bool IsAvailable => _input != null;
        public bool IsPressed(string action, string handlerLayer = null) => _input?.IsPressed(action, handlerLayer) == true;
        public bool IsJustPressed(string action, string handlerLayer = null) => _input?.IsJustPressed(action, handlerLayer) == true;
        public bool IsJustReleased(string action, string handlerLayer = null) => _input?.IsJustReleased(action, handlerLayer) == true;
        public bool IsBuffered(string action, float bufferTime) => _input?.IsBuffered(action, bufferTime) == true;
        public float GetActionStrength(string action, string handlerLayer = null) => _input?.GetActionStrength(action, handlerLayer) ?? 0f;
        public float GetHoldTime(string action) => _input?.GetHoldTime(action) ?? 0f;
        public bool ConsumePressed(string action, string handlerLayer = null) => _input?.TryConsumePressed(action, handlerLayer) == true;
        public bool ConsumeJustPressed(string action, string handlerLayer = null) => _input?.TryConsumeJustPressed(action, handlerLayer) == true;
        public bool ConsumeJustReleased(string action, string handlerLayer = null) => _input?.TryConsumeJustReleased(action, handlerLayer) == true;
    }
}
