using System.Threading.Tasks;
using Framework;
using Framework.UI;
using GameLogic;
using GameLogic.Save;
using Generated.Config;
using Godot;

/// <summary>
/// Preloads early data and resources, then enters the level procedure.
/// </summary>
public class PreloadProcedure : ProcedureBase
{
    private bool isPreload = false;
    private IResourceModule _resource;
    private IConfigModule _config;
    private ISaveModule _save;

    protected internal override void OnEnter(IFsm<IProcedureModule> procedureOwner)
    {
        Debugger.Info("Enter PreloadProcedure");

        base.OnEnter(procedureOwner);
        _resource = ModuleSystem.GetModule<IResourceModule>();
        _config = ModuleSystem.GetModule<IConfigModule>();
        _save = ModuleSystem.GetModule<ISaveModule>();

        _ = Load(procedureOwner);
    }

    private async Task Load(IFsm<IProcedureModule> procedureOwner)
    {
        //第一次进入时加载配置表和资源分包，后续进入不重复加载
        if (!isPreload)
        {
            var configTask = _config.LoadTableAsync<TestexcelConfig>();
            await Task.WhenAll(configTask);

            var test = _config.GetById<TestexcelConfig>(1);
            Debugger.Info("testId:" + test.Id + " testName:" + test.Name + " testPara:" + test.Para + " testPara2" + test.Para2);
        }

        //预加载关卡和控制器场景，进入关卡后会实例化它们
        var levelHandle = _resource.LoadSceneAsync("res://assets/scenes/spacelevel.tscn");
        var controllerHandle = _resource.LoadSceneAsync("res://assets/scenes/minigamecontroller.tscn");
        var gameState = RootModule.Instance.GameState;

        //尝试加载存档数据，如果存在则可以在后续流程中使用
        if (_save.Load())
        {
            
        }

        await Task.WhenAll(levelHandle.Task, controllerHandle.Task);

        Debugger.Info($"[PreloadProcedure] Level scene loaded: {levelHandle.Scene?.ResourcePath}");
        Debugger.Info($"[PreloadProcedure] Controller scene loaded: {controllerHandle.Scene?.ResourcePath}");
        
        //只有当关卡和控制器场景都成功加载后才进入关卡流程
        if (levelHandle.IsValid && controllerHandle.IsValid)
        {
            Debugger.Info("[PreloadProcedure] Level and controller scenes are ready, entering level.");
            ModuleSystem.GetModule<IUIModule>().CloseAll();

            if (Engine.GetMainLoop() is SceneTree tree)
            {
                var levelNode = levelHandle.InstantiateAndBind<Node>(tree.Root.GetNode("Root"));

                var controllerNode = controllerHandle.Instantiate<Node>();
                gameState.SetPlayerController(controllerNode as GameObject2D);
                
                levelNode.AddChild(controllerNode);
                controllerHandle.BindTo(levelNode);
            }

            ChangeState<LevelProcedure>(procedureOwner);
        }
        else
        {
            Debugger.Warn("[PreloadProcedure] Failed to load level or controller scene.");
        }

        isPreload = true;
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
