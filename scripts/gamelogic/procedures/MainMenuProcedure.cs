using System;
using Framework;
using Framework.UI;
using GameLogic;
using GameLogic.Input;
using GameLogic.UI;
using Godot;

/// <summary>
/// 主菜单流程。
/// </summary>
public class MainMenuProcedure : ProcedureBase
{
    private IFsm<IProcedureModule> _procedureOwner;
    private IInputModule _inputModule;

    protected internal override void OnEnter(IFsm<IProcedureModule> procedureOwner)
    {
        Debugger.Info("Enter MainMenuProcedure");

        base.OnInit(procedureOwner);

        _procedureOwner = procedureOwner;
        _inputModule = ModuleSystem.GetModule<IInputModule>();

        ModuleSystem.GetModule<IUIModule>().ShowUIAsync<MainMenuWindow>(userData: "v1.0.0");
        ModuleSystem.GetModule<IEventModule>().Subscribe(GameUIEvents.GameStart, LoadIntoPreload);
    }

    protected internal override void OnProcess(IFsm<IProcedureModule> procedureOwner, double elapseSeconds, double realElapseSeconds)
    {
        base.OnProcess(procedureOwner, elapseSeconds, realElapseSeconds);

        if (_inputModule != null &&
            _inputModule.IsJustPressed("ui_cancel"))
        {
            Debugger.Info("[MainMenuProcedure] 按下取消键，退出游戏。");
            if (Engine.GetMainLoop() is SceneTree tree)
                tree.Quit();
        }
    }

    private void LoadIntoPreload()
    {
        ModuleSystem.GetModule<IUIModule>().CloseAll();
        ChangeState<PreloadProcedure>(_procedureOwner);
    }

    protected internal override void OnLeave(IFsm<IProcedureModule> procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);
        ModuleSystem.GetModule<IEventModule>().Unsubscribe(GameUIEvents.GameStart, LoadIntoPreload);
    }
}
