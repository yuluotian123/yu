using Framework;
using Framework.UI;
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
        ModuleSystem.GetModule<IUIModule>().ShowUIAsync<MainMenuWindow>(t => { ModuleSystem.GetModule<IEventModule>().Send(GameUIEvents.GameNotice, "这是一个游戏通知事件！");}, "v1.0.0");
        ModuleSystem.GetModule<IEventModule>().Subscribe(GameUIEvents.GameStart, LoadLevelScene);
    }

    protected internal override void OnProcess(IFsm<IProcedureModule> procedureOwner, double elapseSeconds, double realElapseSeconds)
    {
        base.OnProcess(procedureOwner, elapseSeconds, realElapseSeconds);

        if (Input.IsActionPressed("ui_cancel"))
        {
            Debugger.Info("[MainMenuProcedure] 按下取消键，退出游戏");
            if (Engine.GetMainLoop() is SceneTree tree)
                tree.Quit();
        }
        // 保留 Tab 键快速进入 Level 的测试逻辑（演示用）
        else if (Input.IsActionPressed("ui_focus_next"))
        {
            LoadLevelScene();
        }
    }

    private void LoadLevelScene()
    {
        var levelHandle = _procedureOwner.GetData<ResourceHandle<PackedScene>>("LevelHandle");
        if (levelHandle != null && levelHandle.IsValid)
        {
            Debugger.Info($"[MainMenuProcedure] Level场景已加载，切换关卡");

            // 关闭主菜单 UI
            ModuleSystem.GetModule<IUIModule>().CloseUI<MainMenuWindow>();

            // 实例化关卡场景
            var node = levelHandle.Asset.Instantiate();
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

    protected internal override void OnLeave(IFsm<IProcedureModule> procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);

        ModuleSystem.GetModule<IEventModule>().Unsubscribe(GameUIEvents.GameStart, LoadLevelScene);
    }
}
