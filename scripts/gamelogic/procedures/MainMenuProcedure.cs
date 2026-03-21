using Framework;
using Godot;

public class MainMenuProcedure : ProcedureBase
{
    IFsm<IProcedureModule> procedureOwner;

    protected internal override void OnEnter(IFsm<IProcedureModule> procedureOwner)
    {
        Debugger.Info("Enter MainMenuProcedure");
        this.procedureOwner = procedureOwner;
        base.OnInit(procedureOwner);
    }

    protected internal override void OnProcess(IFsm<IProcedureModule> procedureOwner, double elapseSeconds, double realElapseSeconds)
    {
        base.OnProcess(procedureOwner, elapseSeconds, realElapseSeconds);

        if (Input.IsActionPressed("ui_focus_next"))
        {
            var levelHandle = procedureOwner.GetData<ResourceHandle<PackedScene>>("LevelHandle");
            if (levelHandle != null && levelHandle.IsValid)
            {
                
                Debugger.Info($"[MainMenuProcedure] Level场景已加载: {levelHandle.Asset.ResourcePath}");
                var node = levelHandle.Asset.Instantiate();
                if (Engine.GetMainLoop() is SceneTree tree)
                {
                    tree.Root.GetNode("Root").RemoveChild(tree.Root.GetNode("Root/MainMenu"));
                    tree.Root.GetNode("Root").AddChild(node);
                }
            }
            else
            {
                Debugger.Warn($"[MainMenuProcedure] Level场景未加载或加载失败");
                return;
            }


            ChangeState<LevelProcedure>(procedureOwner);
        }
    }

    protected internal override void OnLeave(IFsm<IProcedureModule> procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);
    }
}