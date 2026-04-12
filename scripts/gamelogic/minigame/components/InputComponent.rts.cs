using Framework;
using GameLogic;
using GameLogic.Input;
using Godot;

/// <summary>
/// 处理 RTS 交互输入的 partial 逻辑。
/// </summary>
public partial class InputComponent
{
    public bool SelectPressedThisFrame { get; private set; }
    public bool CommandMovePressedThisFrame { get; private set; }
    public Vector2 MouseScreenPosition { get; private set; } = Vector2.Zero;
    public Vector2 MouseWorldPosition { get; private set; } = Vector2.Zero;

    /// <summary>
    /// 在每帧中采样 RTS 所需的鼠标与命令输入。
    /// </summary>
    public void OnRtsInputUpdate(double delta)
    {
        MouseScreenPosition = Owner?.GetViewport()?.GetMousePosition() ?? Vector2.Zero;
        MouseWorldPosition = ScreenToWorld(MouseScreenPosition);

        SelectPressedThisFrame =!IsPointerBlockedByUI &&_inputModule.IsJustPressed("combat_select");
        CommandMovePressedThisFrame =  _inputModule.TryHandleJustPressed("combat_command_move");
    }

    /// <summary>
    /// 在销毁时清空 RTS 输入状态。
    /// </summary>
    public void OnRtsInputDestroy()
    {
        SelectPressedThisFrame = false;
        CommandMovePressedThisFrame = false;
        MouseScreenPosition = Vector2.Zero;
        MouseWorldPosition = Vector2.Zero;
        IsPointerBlockedByUI = false;
    }

    /// <summary>
    /// 将屏幕坐标转换为当前画布世界坐标。
    /// </summary>
    private Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        var viewport = Owner?.GetViewport();
        if (viewport == null)
            return screenPosition;

        return viewport.GetCanvasTransform().AffineInverse() * screenPosition;
    }
}
