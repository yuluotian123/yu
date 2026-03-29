using System;
using System.Threading.Tasks;
using Framework;
using Framework.UI;
using GameLogic;
using GameLogic.UI;
using Godot;

public class MainMenuProcedure : ProcedureBase
{
    private IFsm<IProcedureModule> _procedureOwner = null;

    protected internal override void OnEnter(IFsm<IProcedureModule> procedureOwner)
    {
        Debugger.Info("Enter MainMenuProcedure");

        base.OnInit(procedureOwner);
        _procedureOwner = procedureOwner;

        // 使用 UIModule 打开主菜单窗口，传入版本号作为 UserData
        ModuleSystem.GetModule<IUIModule>().ShowUIAsync<MainMenuWindow>(t => { ModuleSystem.GetModule<IEventModule>().Send(GameUIEvents.GameNotice, "这是一个游戏通知事件！"); }, "v1.0.0");
        ModuleSystem.GetModule<IEventModule>().Subscribe(GameUIEvents.GameStart, LoadLevelScene);

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

    private void LoadLevelScene()
    {
        var _resource = ModuleSystem.GetModule<IResourceModule>();
     _resource.LoadAssetAsync<PackedScene>("res://assets/minigame/scenes/level.tscn")
            .OnCompleted(
                h =>
                {
                    Debugger.Info($"[LevelProcedure] Level场景加载完成: {h.Asset?.ResourcePath}");

                    if (h != null && h.IsValid)
                    {
                        Debugger.Info($"[MainMenuProcedure] Level场景已加载，切换关卡");

                        // 关闭主菜单 UI，自动释放资源
                        ModuleSystem.GetModule<IUIModule>().CloseAll();

                        // 实例化关卡场景
                        var node = h.Asset.Instantiate();
                        if (Engine.GetMainLoop() is SceneTree tree)
                        {
                            tree.Root.GetNode("Root").AddChild(node);
                        }
                        ChangeState<LevelProcedure>(_procedureOwner);
                    }
                    else
                    {
                        Debugger.Warn("[MainMenuProcedure] Level场景未加载或加载失败");
                    }

                }
            );
    }

    protected internal override void OnLeave(IFsm<IProcedureModule> procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);

        ModuleSystem.GetModule<IEventModule>().Unsubscribe(GameUIEvents.GameStart, LoadLevelScene);
    }
}
