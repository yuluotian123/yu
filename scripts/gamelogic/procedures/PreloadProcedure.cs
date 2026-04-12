using System.Threading.Tasks;
using Framework;
using Framework.UI;
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
        //是否是第一次进入游戏场景
        if (!isPreload)
        {
            //异步加载所有表格
            var configTask = _config.LoadTableAsync<TestexcelConfig>();

            //异步加载资源分包...



            //等待全部执行完毕
            await Task.WhenAll(configTask);

            //测试
            var test = _config.GetById<TestexcelConfig>(1);
            Debugger.Info("testId:" + test.Id + " testName:" + test.Name + " testPara:" + test.Para + " testPara2" + test.Para2);
        }


        //读取指定插槽的存档数据(所有需要写回的ISaveable此刻都需要完成register)
        if (_save.Load())
        {


        }


        //加载关卡
        var levelhandle = _resource.LoadSceneAsync("res://assets/scenes/spacelevel.tscn");
        var characterhandle = _resource.LoadSceneAsync("res://assets/scenes/playercontroller.tscn");

        await Task.WhenAll(levelhandle.Task, characterhandle.Task);

        Debugger.Info($"[PreloadProcedure] Level场景加载完成: {levelhandle.Scene?.ResourcePath}");
        Debugger.Info($"[PreloadProcedure] Character场景加载完成: {characterhandle.Scene?.ResourcePath}");

        if (levelhandle.IsValid && characterhandle.IsValid)
        {
            Debugger.Info("[PreloadProcedure] Level场景和关卡场景均已加载，切换关卡");
            ModuleSystem.GetModule<IUIModule>().CloseAll();

            if (Engine.GetMainLoop() is SceneTree tree)
            {
                var levelNode =levelhandle.InstantiateAndBind<Node>(tree.Root.GetNode("Root"));
                characterhandle.InstantiateAndBind<Node>(levelNode);
            }

            ChangeState<LevelProcedure>(procedureOwner);
        }
        else
        {
            Debugger.Warn("[PreloadProcedure] Level场景未加载或加载失败");
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
