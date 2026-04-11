using Framework;
using Framework.UI;
using Generated.Config;
using Godot;

/// <summary>
/// 预加载一些早期数据，在完成数据加载后进入主菜单界面（也可以后置到通过mainmenu进入正式游戏时）
/// MainMenuProcedure 负责通过 UIModule 打开主菜单 UI。
/// </summary>
public class PreloadProcedure : ProcedureBase
{
    private IResourceModule _resource;
    private IConfigModule _config;

    protected internal override void OnEnter(IFsm<IProcedureModule> procedureOwner)
    {
        Debugger.Info("Enter PreloadProcedure");

        base.OnEnter(procedureOwner);
        _resource = ModuleSystem.GetModule<IResourceModule>();
        _config = ModuleSystem.GetModule<IConfigModule>();

       //加载表格并读取数据
        _config.LoadTable<TestexcelConfig>(); 
        var test = _config.GetById<TestexcelConfig>(1);
        Debugger.Info("testId:" + test.Id + " testName:" + test.Name + " testPara:" + test.Para + " testPara2" + test.Para2);

        //加载前期资源分包（异步）...
        LoadLevelScene(procedureOwner);
    }

        private void LoadLevelScene(IFsm<IProcedureModule> _procedureOwner)
    {
        var resource = ModuleSystem.GetModule<IResourceModule>();
        resource.LoadSceneAsync("res://assets/scenes/spacelevel.tscn")
            .OnCompleted(handle =>
            {
                Debugger.Info($"[LevelProcedure] Level场景加载完成: {handle.Scene?.ResourcePath}");

                if (handle.IsValid)
                {
                    Debugger.Info("[PreloadProcedure] Level场景已加载，切换关卡");
                    ModuleSystem.GetModule<IUIModule>().CloseAll();

                    if (Engine.GetMainLoop() is SceneTree tree)
                        handle.InstantiateAndBind<Node>(tree.Root.GetNode("Root"));

                    ChangeState<LevelProcedure>(_procedureOwner);
                }
                else
                {
                    Debugger.Warn("[PreloadProcedure]  Level场景未加载或加载失败");
                }
            });
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
