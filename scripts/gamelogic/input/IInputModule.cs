using Godot;

namespace GameLogic
{
    /// <summary>
    /// 统一的 action 输入访问入口。
    /// </summary>
    public interface IInputModule
    {
        bool IsPressed(string action, string handlerLayer = null, bool filterConsumed = false, bool includeSamePriority = true);

        bool IsJustPressed(string action, string handlerLayer = null, bool filterConsumed = false, bool includeSamePriority = false);

        bool IsJustReleased(string action, string handlerLayer = null, bool filterConsumed = false, bool includeSamePriority = false);

        bool TryConsumePressed(string action, string handlerLayer = null, bool includeSamePriority = true);

        bool TryConsumeJustPressed(string action, string handlerLayer = null, bool includeSamePriority = false);

        bool TryConsumeJustReleased(string action, string handlerLayer = null, bool includeSamePriority = false);

        float GetActionStrength(string action, string handlerLayer = null, bool filterConsumed = false, bool includeSamePriority = true);

        bool TryConsumeActionStrength(string action, string handlerLayer = null, bool includeSamePriority = true);

        Vector2 GetAxis(
            string negativeX,
            string positiveX,
            string negativeY,
            string positiveY,
            string handlerLayer = null,
            bool filterConsumed = false,
            bool includeSamePriority = true);

        bool TryConsumeAxis(
            string negativeX,
            string positiveX,
            string negativeY,
            string positiveY,
            string handlerLayer = null,
            bool includeSamePriority = true);

        Vector2 GetVector(
            string negativeX,
            string positiveX,
            string negativeY,
            string positiveY,
            float deadzone = -1f,
            string handlerLayer = null,
            bool filterConsumed = false,
            bool includeSamePriority = true);

        bool TryConsumeVector(
            string negativeX,
            string positiveX,
            string negativeY,
            string positiveY,
            string handlerLayer = null,
            bool includeSamePriority = true);

        Vector2 GetMouseDelta();

        bool IsBuffered(string action, float bufferTime);

        float GetHoldTime(string action);

        void EnableLayer(string layerName);

        void DisableLayer(string layerName);

        bool IsLayerEnabled(string layerName);

        void RefreshActionCache();

        bool IsActionConsumed(string action, string handlerLayer = null, bool includeSamePriority = false);

        bool IsActionHeldConsumed(string action, string handlerLayer = null, bool includeSamePriority = true);

        void ClearBuffer();

        void ConsumeBufferedAction(string action);

        void SimulateInputEvent(InputEvent @event);

        void SimulateActionPress(string action, float strength = 1.0f);

        void SimulateActionRelease(string action);
    }
}
