using Framework;
using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class PlayerCharacterInputComponent2D : Component2D, ICharacterInputProvider
    {
        public override int Priority => ComponentPriority.Input;
        private IInputModule _input;

        public override void OnInit() => _input = ModuleSystem.GetModule<IInputModule>();
        public override void OnDestroy() => _input = null;

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
