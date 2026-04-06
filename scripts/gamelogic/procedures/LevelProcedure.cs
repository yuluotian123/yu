using System;
using Framework;
using GameLogic.Mission;
using Godot;

public class LevelProcedure : ProcedureBase
{
    private IMissionModule mission;

    protected internal override void OnEnter(IFsm<IProcedureModule> procedureOwner)
    {
        Debugger.Info("Enter LevelProcedure");
        mission = ModuleSystem.GetModule<IMissionModule>();
    }

    protected internal override void OnLeave(IFsm<IProcedureModule> procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);
    }

    protected internal override void OnProcess(IFsm<IProcedureModule> procedureOwner, double elapseSeconds, double realElapseSeconds)
    {
        //进入主菜单(临时)
        if (Input.IsActionJustPressed("ui_cancel"))
        {
            if (Engine.GetMainLoop() is SceneTree tree)
            {
                var levelNode = tree.Root.GetNode("Root/Level");
                if (levelNode != null)
                {
                    levelNode.QueueFree();

                    //卸载资源，避免内存泄漏
                    ModuleSystem.GetModule<IResourceModule>().ForceUnloadAsset("res://assets/scenes/level.tscn");
                    GC.Collect();
                }

                ChangeState<MainMenuProcedure>(procedureOwner);
            }

        }

    }
}