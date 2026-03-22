using Godot;

namespace GameLogic.Input
{
    /// <summary>
    /// 输入模块接口。
    /// 基于 Godot 原生 Input 系统和 InputEvent 机制，提供动作游戏所需的增强功能。
    /// </summary>
    public interface IInputModule
    {
        /// <summary>
        /// 检查动作是否按下（持续）。
        /// </summary>
        bool IsPressed(string action);

        /// <summary>
        /// 检查动作是否刚按下（本帧）。
        /// </summary>
        bool IsJustPressed(string action);

        /// <summary>
        /// 检查动作是否刚释放（本帧）。
        /// </summary>
        bool IsJustReleased(string action);

        /// <summary>
        /// 获取动作的强度（0-1，用于扳机等模拟输入）。
        /// </summary>
        float GetActionStrength(string action);

        /// <summary>
        /// 获取轴向输入（如 WASD 或摇杆）。
        /// </summary>
        Vector2 GetAxis(string negativeX, string positiveX, string negativeY, string positiveY);

        /// <summary>
        /// 获取向量输入（Godot 4.x 新 API）。
        /// </summary>
        Vector2 GetVector(string negativeX, string positiveX, string negativeY, string positiveY, float deadzone = -1f);

        /// <summary>
        /// 检查动作是否在缓冲时间内被按下（输入缓冲，动作游戏核心）。
        /// </summary>
        bool IsBuffered(string action, float bufferTime);

        /// <summary>
        /// 获取动作持续按下的时间。
        /// </summary>
        float GetHoldTime(string action);

        /// <summary>
        /// 启用输入层。
        /// </summary>
        void EnableLayer(string layerName);

        /// <summary>
        /// 禁用输入层。
        /// </summary>
        void DisableLayer(string layerName);

        /// <summary>
        /// 检查输入层是否启用。
        /// </summary>
        bool IsLayerEnabled(string layerName);

        /// <summary>
        /// 清除所有输入缓冲。
        /// </summary>
        void ClearBuffer();

        /// <summary>
        /// 消费缓冲的动作（使其失效）。
        /// </summary>
        void ConsumeBufferedAction(string action);

        /// <summary>
        /// 模拟输入事件（运行时注入）。
        /// </summary>
        void SimulateInputEvent(InputEvent @event);

        /// <summary>
        /// 模拟动作按下。
        /// </summary>
        void SimulateActionPress(string action, float strength = 1.0f);

        /// <summary>
        /// 模拟动作释放。
        /// </summary>
        void SimulateActionRelease(string action);
    }
}
