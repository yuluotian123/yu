using Framework;
using Godot;

/// <summary>
/// 预加载流程示例 —— 演示资源管理系统的常见用法。
///
/// 用法一：同步加载
/// 用法二：异步加载（链式回调）
/// 用法三：同路径并发请求自动合并
/// 用法四：查询缓存状态
/// 用法五：手动卸载资源
/// </summary>
public class PreloadProcedure : ProcedureBase
{
    private IResourceModule _resource;

    protected internal override void OnEnter(IFsm<IProcedureModule> procedureOwner)
    {
        base.OnEnter(procedureOwner);
        _resource = ModuleSystem.GetModule<IResourceModule>();

        //加载关卡场景
        var levelHandle = _resource.LoadAssetAsync<PackedScene>("res://assets/minigame/scenes/level.tscn").OnCompleted(h => Debugger.Info($"[PreloadProcedure] Level场景加载完成, 进度={h.Progress}"));
        procedureOwner.SetData("LevelHandle", levelHandle);

        //加载main menu场景，并切换到mainmenu场景
        var mainMenuHandle = _resource.LoadAssetAsync<PackedScene>("res://assets/minigame/scenes/main_menu.tscn")
            .OnCompleted(handle =>
            {
                if (handle.IsValid)
                {
                    Debugger.Info($"[PreloadProcedure] 异步加载成功: {handle.Asset.ResourcePath}");
                    // 实例化场景示例（Procedure 不是 Node，需通过 SceneTree 访问）：
                    var node = handle.Asset.Instantiate();
                    if (Engine.GetMainLoop() is SceneTree tree)
                    {
                        tree.Root.GetNode("Root").AddChild(node);
                        Debugger.Info($"[PreloadProcedure] 场景实例化成功: {handle.Asset.ResourcePath}");
                    }

                    ChangeState<MainMenuProcedure>(procedureOwner);
                }
                else
                {
                    Debugger.Error($"[PreloadProcedure] 异步加载失败: {handle.Error}");
                }
            });
        procedureOwner.SetData("MainMenuHandle", mainMenuHandle);
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
