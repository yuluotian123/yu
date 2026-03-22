using Framework;
using Godot;

/// <summary>
/// 预加载流程 —— 异步预加载关卡场景，完成后切换到 MainMenuProcedure。
/// MainMenuProcedure 负责通过 UIModule 打开主菜单 UI。
/// </summary>
public class PreloadProcedure : ProcedureBase
{
    private IResourceModule _resource;

    protected internal override void OnEnter(IFsm<IProcedureModule> procedureOwner)
    {
        Debugger.Info("Enter PreloadProcedure");

        base.OnEnter(procedureOwner);
        _resource = ModuleSystem.GetModule<IResourceModule>();

        // 预加载关卡场景（后台异步，供 MainMenuProcedure 使用）
        var levelHandle = _resource.LoadAssetAsync<PackedScene>("res://assets/minigame/scenes/level.tscn")
            .OnCompleted(h => Debugger.Info($"[PreloadProcedure] Level场景加载完成: {h.Asset?.ResourcePath}"));
        procedureOwner.SetData("LevelHandle", levelHandle);

        // 切换到主菜单流程（由 MainMenuProcedure 负责打开 MainMenuWindow）
        ChangeState<MainMenuProcedure>(procedureOwner);
    }

    protected internal override void OnProcess(IFsm<IProcedureModule> procedureOwner, double elapseSeconds, double realElapseSeconds)
    {
        base.OnProcess(procedureOwner, elapseSeconds, realElapseSeconds);
    }

    protected internal override void OnLeave(IFsm<IProcedureModule> procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);
    }
}
