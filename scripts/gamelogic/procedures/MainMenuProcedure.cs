using System;
using Framework;
using Framework.UI;
using GameLogic;
using GameLogic.UI;
using Godot;

/// <summary>
/// 主菜单界面，需要的功能为：设置，读档，开启游戏...
/// </summary>
public class MainMenuProcedure : ProcedureBase
{
    private IFsm<IProcedureModule> _procedureOwner;

    protected internal override void OnEnter(IFsm<IProcedureModule> procedureOwner)
    {
        Debugger.Info("Enter MainMenuProcedure");

        base.OnInit(procedureOwner);
        
        _procedureOwner = procedureOwner;

        ModuleSystem.GetModule<IUIModule>().ShowUIAsync<MainMenuWindow>(
            t => { ModuleSystem.GetModule<IEventModule>().Send(GameUIEvents.GameNotice, "这是一个游戏通知事件"); },
            "v1.0.0");
        ModuleSystem.GetModule<IEventModule>().Subscribe(GameUIEvents.GameStart, LoadIntoPreload);
    }

    protected internal override void OnProcess(IFsm<IProcedureModule> procedureOwner, double elapseSeconds, double realElapseSeconds)
    {
        base.OnProcess(procedureOwner, elapseSeconds, realElapseSeconds);

        if (Input.IsActionJustPressed("ui_cancel"))
        {
            Debugger.Info("[MainMenuProcedure] 按下取消键，退出游戏");
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
