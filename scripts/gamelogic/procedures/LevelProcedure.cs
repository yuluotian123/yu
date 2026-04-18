using Framework;
using Framework.UI;
using GameLogic;
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
        if (_inputModule != null &&
            _inputModule.IsJustPressed("camera_down") &&
            _inputModule.TryConsumeJustPressed("camera_down"))
        {
            ModuleSystem.GetModule<ISaveModule>().Save();
        }

        // 杩涘叆涓昏彍鍗曪紙涓存椂锛?
        if (_inputModule != null &&
            _inputModule.IsJustPressed("ui_cancel") &&
            _inputModule.TryConsumeJustPressed("ui_cancel"))
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
