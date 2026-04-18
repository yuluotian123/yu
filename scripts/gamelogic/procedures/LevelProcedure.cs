using Framework;
using Framework.UI;
using GameLogic;
using GameLogic.Input;
using GameLogic.Save;
using GameLogic.UI;
using Godot;

/// <summary>
/// 鍏冲崱娴佺▼銆?/// </summary>
public class LevelProcedure : ProcedureBase
{
    private IInputModule _inputModule;

    protected internal override void OnEnter(IFsm<IProcedureModule> procedureOwner)
    {
        Debugger.Info("Enter LevelProcedure");
        RootModule.Instance?.ResetNormalGameSpeed();

        _inputModule = ModuleSystem.GetModule<IInputModule>();
    }

    protected internal override void OnLeave(IFsm<IProcedureModule> procedureOwner, bool isShutdown)
    {
        RootModule.Instance?.ResetNormalGameSpeed();

        base.OnLeave(procedureOwner, isShutdown);
    }

    protected internal override void OnProcess(IFsm<IProcedureModule> procedureOwner, double elapseSeconds, double realElapseSeconds)
    {
        if (_inputModule != null &&
            _inputModule.IsJustPressed("camera_down") &&
            _inputModule.TryConsumeJustPressed("camera_down"))
        {
            ModuleSystem.GetModule<ISaveModule>().Save();
        }

        if (_inputModule != null &&
            _inputModule.IsJustPressed("ui_cancel"))
        {
            if (Engine.GetMainLoop() is SceneTree tree)
            {
                var levelNode = tree.Root.GetNode("Root/Level");
                if (levelNode != null)
                {
                    levelNode.QueueFree();
                }

                ChangeState<MainMenuProcedure>(procedureOwner);
            }
        }
    }
}
