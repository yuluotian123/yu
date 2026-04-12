using Framework;
using Framework.UI;
using GameLogic;
using GameLogic.Input;
using GameLogic.Save;
using GameLogic.UI;
using Godot;

/// <summary>
/// 关卡流程。
/// </summary>
public class LevelProcedure : ProcedureBase
{
    private IInputModule _inputModule;

    protected internal override void OnEnter(IFsm<IProcedureModule> procedureOwner)
    {
        Debugger.Info("Enter LevelProcedure");
        RootModule.Instance?.ResetNormalGameSpeed();
        ModuleSystem.GetModule<IUIModule>().ShowUI<LevelWindow>();
        _inputModule = ModuleSystem.GetModule<IInputModule>();
    }

    protected internal override void OnLeave(IFsm<IProcedureModule> procedureOwner, bool isShutdown)
    {
        RootModule.Instance?.ResetNormalGameSpeed();
        ModuleSystem.GetModule<IUIModule>().CloseUI<LevelWindow>();

        base.OnLeave(procedureOwner, isShutdown);
    }

    protected internal override void OnProcess(IFsm<IProcedureModule> procedureOwner, double elapseSeconds, double realElapseSeconds)
    {
        //if(_inputModule != null && _inputModule.TryHandleJustPressed("camera_down"))
        //{
        //    ModuleSystem.GetModule<ISaveModule>().Save();
        //}

        // 进入主菜单（临时）
        if (_inputModule != null && _inputModule.TryHandleJustPressed("ui_cancel"))
        {
            if (Engine.GetMainLoop() is SceneTree tree)
            {
                var levelNode = tree.Root.GetNode("Root/Spacelevel");
                if (levelNode != null)
                {
                    levelNode.QueueFree();
                }

                ChangeState<MainMenuProcedure>(procedureOwner);
            }
        }
    }
}
