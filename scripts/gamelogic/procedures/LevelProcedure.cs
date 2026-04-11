using System;
using Framework;
using Framework.UI;
using GameLogic.Mission;
using GameLogic.UI;
using Godot;

/// <summary>
/// 进入正式的关卡界面，理论上还需要有个读条procedure，用于加载存档数据和世界初始化数据并处理，目前不晓得有什么数据结构，所以无所吊慰
/// </summary>
public class LevelProcedure : ProcedureBase
{

    protected internal override void OnEnter(IFsm<IProcedureModule> procedureOwner)
    {
        Debugger.Info("Enter LevelProcedure");
        RootModule.Instance?.ResetNormalGameSpeed();
        ModuleSystem.GetModule<IUIModule>().ShowUI<LevelWindow>();
    }

    protected internal override void OnLeave(IFsm<IProcedureModule> procedureOwner, bool isShutdown)
    {
        RootModule.Instance?.ResetNormalGameSpeed();
        ModuleSystem.GetModule<IUIModule>().CloseUI<LevelWindow>();

        base.OnLeave(procedureOwner, isShutdown);
    }

    protected internal override void OnProcess(IFsm<IProcedureModule> procedureOwner, double elapseSeconds, double realElapseSeconds)
    {
        //进入主菜单(临时)
        if (Input.IsActionJustPressed("ui_cancel"))
        {
            if (Engine.GetMainLoop() is SceneTree tree)
            {
                var levelNode = tree.Root.GetNode("Root/SpaceLevel");
                if (levelNode != null)
                {
                    levelNode.QueueFree();

                }

                ChangeState<MainMenuProcedure>(procedureOwner);
            }

        }

    }
}
