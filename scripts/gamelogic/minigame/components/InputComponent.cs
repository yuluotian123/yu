using Framework;
using GameLogic;
using GameLogic.Input;
using Godot;

/// <summary>
/// 统一处理玩家输入采样的组件。
/// </summary>
public partial class InputComponent : Component
{
    private IInputModule _inputModule;
    public bool IsPointerBlockedByUI { get; private set; }

    /// <summary>
    /// 获取组件执行优先级。
    /// </summary>
    public override int Priority => ComponentPriority.Input;

    /// <summary>
    /// 初始化输入组件并缓存输入模块。
    /// </summary>
    public override void OnInit()
    {
        _inputModule = ModuleSystem.GetModule<IInputModule>();
    }

    /// <summary>
    /// 在每帧中采样相机输入与 RTS 输入。
    /// </summary>
    public override void OnUpdate(double delta)
    {
        if (_inputModule == null)
            return;

                
        IsPointerBlockedByUI = CheckPointerBlockedByUI();
        OnRtsInputUpdate(delta);
        OnCameraInputUpdate(delta);
    }

    /// <summary>
    /// 在组件销毁时重置输入状态。
    /// </summary>
    public override void OnDestroy()
    {
        OnCameraInputDestroy();
        OnRtsInputDestroy();
    }

        /// <summary>
    /// 判断当前鼠标是否被 UI 控件阻挡。
    /// </summary>
    private bool CheckPointerBlockedByUI()
    {
        var hoveredControl = Owner?.GetViewport()?.GuiGetHoveredControl();
        return hoveredControl != null && hoveredControl.MouseFilter != Control.MouseFilterEnum.Ignore;
    }
}
