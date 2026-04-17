using Godot;

namespace GameLogic.Input
{
    /// <summary>
    /// 统一的 action 输入访问入口。
    /// </summary>
    public interface IInputModule
    {
        /// <summary>
        /// 查询 action 是否处于按住状态。
        /// 这是纯查询接口，不会建立 consume 或 held lock。
        /// </summary>
        bool IsPressed(string action);

        /// <summary>
        /// 查询 action 是否在当前帧刚刚按下。
        /// 这是纯查询接口，不会建立 consume 或 held lock。
        /// </summary>
        bool IsJustPressed(string action);

        /// <summary>
        /// 查询 action 是否在当前帧刚刚松开。
        /// 这是纯查询接口，不会建立 consume 或 held lock。
        /// </summary>
        bool IsJustReleased(string action);

        /// <summary>
        /// 尝试接管一个持续按住中的 action。
        /// 接管成功后，会在当前层建立 held consume，直到输入失效。
        /// </summary>
        bool TryHandlePressed(string action, string handlerLayer = null);

        /// <summary>
        /// 显式接管一个持续按住中的 action。
        /// 适用于先用 IsPressed 或 IsJustPressed 做判断，再手动建立 held consume 的场景。
        /// </summary>
        bool TryConsumePressed(string action, string handlerLayer = null);

        /// <summary>
        /// 尝试接管当前帧刚按下的 action。
        /// 这是逐帧 consume，只影响当前帧。
        /// </summary>
        bool TryHandleJustPressed(string action, string handlerLayer = null, bool includeSamePriority = false);

        /// <summary>
        /// 尝试接管当前帧刚松开的 action。
        /// 这是逐帧 consume，只影响当前帧。
        /// </summary>
        bool TryHandleJustReleased(string action, string handlerLayer = null, bool includeSamePriority = false);

        /// <summary>
        /// 查询 action 当前强度。
        /// 这是纯查询接口，不会建立 consume 或 held lock。
        /// </summary>
        float GetActionStrength(string action);

        /// <summary>
        /// 尝试读取并接管 action 当前强度。
        /// 只有强度大于 0 时才会接管成功。
        /// </summary>
        bool TryHandleActionStrength(string action, out float strength, string handlerLayer = null);

        /// <summary>
        /// 查询四个方向 action 组成的轴向输入。
        /// 这是纯查询接口，不会建立 consume 或 held lock。
        /// </summary>
        Vector2 GetAxis(string negativeX, string positiveX, string negativeY, string positiveY);

        /// <summary>
        /// 尝试读取并接管四个方向 action 组成的轴向输入。
        /// 只有结果非零时才会接管成功。
        /// </summary>
        bool TryHandleAxis(
            string negativeX,
            string positiveX,
            string negativeY,
            string positiveY,
            out Vector2 axis,
            string handlerLayer = null);

        /// <summary>
        /// 查询四个方向 action 组成的向量输入。
        /// 这是纯查询接口，不会建立 consume 或 held lock。
        /// </summary>
        Vector2 GetVector(string negativeX, string positiveX, string negativeY, string positiveY, float deadzone = -1f);

        /// <summary>
        /// 尝试读取并接管四个方向 action 组成的向量输入。
        /// 只有结果非零时才会接管成功。
        /// </summary>
        bool TryHandleVector(
            string negativeX,
            string positiveX,
            string negativeY,
            string positiveY,
            out Vector2 vector,
            string handlerLayer = null,
            float deadzone = -1f);

        /// <summary>
        /// 显式接管四个方向 action 组成的向量输入。
        /// 适用于先用 GetVector 做判断，再手动建立 held consume 的场景。
        /// </summary>
        bool TryConsumeVector(
            string negativeX,
            string positiveX,
            string negativeY,
            string positiveY,
            string handlerLayer = null,
            float deadzone = -1f);

        /// <summary>
        /// 获取当前帧鼠标位移。
        /// </summary>
        Vector2 GetMouseDelta();

        /// <summary>
        /// 查询 action 是否仍在输入缓冲窗口内。
        /// </summary>
        bool IsBuffered(string action, float bufferTime);

        /// <summary>
        /// 获取 action 按住持续时间。
        /// </summary>
        float GetHoldTime(string action);

        /// <summary>
        /// 启用指定输入层。
        /// </summary>
        void EnableLayer(string layerName);

        /// <summary>
        /// 禁用指定输入层。
        /// </summary>
        void DisableLayer(string layerName);

        /// <summary>
        /// 查询指定输入层是否启用。
        /// </summary>
        bool IsLayerEnabled(string layerName);

        /// <summary>
        /// 按当前 InputMap 配置重新构建 action 缓存和 action group。
        /// 当运行时修改了 InputMap 后，可手动调用这个接口同步缓存。
        /// </summary>
        void RefreshActionCache();

        /// <summary>
        /// 手动消费一个 action。
        /// 会按 action group 规则一起扩展消费。
        /// </summary>
        void ConsumeAction(string action, string handlerLayer = null);

        /// <summary>
        /// 查询一个 action 是否已被更高层消费。
        /// 会按 action group 规则一起判断。
        /// </summary>
        bool IsActionConsumed(string action, string handlerLayer = null, bool includeSamePriority = false);

        /// <summary>
        /// 清空输入缓冲。
        /// </summary>
        void ClearBuffer();

        /// <summary>
        /// 手动消费一个缓冲中的 action。
        /// </summary>
        void ConsumeBufferedAction(string action);

        /// <summary>
        /// 注入一个原始输入事件。
        /// </summary>
        void SimulateInputEvent(InputEvent @event);

        /// <summary>
        /// 模拟按下一个 action。
        /// </summary>
        void SimulateActionPress(string action, float strength = 1.0f);

        /// <summary>
        /// 模拟松开一个 action。
        /// </summary>
        void SimulateActionRelease(string action);
    }
}
