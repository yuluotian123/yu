using System;
using Framework;
using Framework.UI;
using GameLogic;
using GameLogic.UI;
using Godot;

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
        ModuleSystem.GetModule<IEventModule>().Subscribe(GameUIEvents.GameStart, LoadLevelScene);
    }

    protected internal override void OnProcess(IFsm<IProcedureModule> procedureOwner, double elapseSeconds, double realElapseSeconds)
    {
        base.OnProcess(procedureOwner, elapseSeconds, realElapseSeconds);

        //GC.Collect();

        if (Input.IsActionJustPressed("ui_cancel"))
        {
            Debugger.Info("[MainMenuProcedure] 按下取消键，退出游戏");
            if (Engine.GetMainLoop() is SceneTree tree)
                tree.Quit();
        }
    }

    private void LoadLevelScene()
    {
        var resource = ModuleSystem.GetModule<IResourceModule>();
        resource.LoadSceneAsync("res://assets/scenes/level.tscn")
            .OnCompleted(handle =>
            {
                Debugger.Info($"[LevelProcedure] Level场景加载完成: {handle.Scene?.ResourcePath}");

                if (handle.IsValid)
                {
                    Debugger.Info("[MainMenuProcedure] Level场景已加载，切换关卡");
                    ModuleSystem.GetModule<IUIModule>().CloseAll();

                    if (Engine.GetMainLoop() is SceneTree tree)
                        handle.InstantiateAndBind<Node>(tree.Root.GetNode("Root"));

                    ChangeState<LevelProcedure>(_procedureOwner);
                }
                else
                {
                    Debugger.Warn("[MainMenuProcedure] Level场景未加载或加载失败");
                }
            });
    }

    protected internal override void OnLeave(IFsm<IProcedureModule> procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);
        ModuleSystem.GetModule<IEventModule>().Unsubscribe(GameUIEvents.GameStart, LoadLevelScene);
    }
}
