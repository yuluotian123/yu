using System;
using System.Threading;
using System.Threading.Tasks;
using Framework;
using Framework.UI;
using GameLogic;
using Godot;

/// <summary>Loads the level only while this procedure still owns the request.</summary>
public class PreloadProcedure : ProcedureBase
{
    public static bool ResumeGame { get; set; } = true;
    private CancellationTokenSource _loadCancellation;
    private int _loadVersion;

    internal Task PendingLoad { get; private set; } = Task.CompletedTask;

    protected internal override void OnEnter(IFsm<IProcedureModule> procedureOwner)
    {
        base.OnEnter(procedureOwner);
        CancelPendingLoad();
        _loadCancellation = new CancellationTokenSource();
        PendingLoad = Load(procedureOwner, _loadVersion, _loadCancellation.Token);
    }

    private async Task Load(IFsm<IProcedureModule> procedureOwner, int version, CancellationToken cancellation)
    {
        try
        {
            if (Engine.GetMainLoop() is not SceneTree tree ||
                tree.Root.GetNodeOrNull<Node>("Root") is not Node root || root.IsQueuedForDeletion())
                throw new InvalidOperationException("The level owner is unavailable.");

            var world=ModuleSystem.GetModule<IWorldModule>();
            var definition=ResourceLoader.Load<WorldDefinition>(WorldDefinition.CommonPath);
            if(!await world.OpenAsync(root,definition,ResumeGame,"default",cancellation))
                throw new InvalidOperationException(world.LastError);
            if (!IsCurrent(procedureOwner, version, cancellation))
                return;
            ModuleSystem.GetModule<IUIModule>().CloseAll();
            ChangeState<LevelProcedure>(procedureOwner);
        }
        catch (Exception exception)
        {
            if (IsCurrent(procedureOwner, version, cancellation))
            {
                Debugger.Warn($"[PreloadProcedure] {exception.Message}");
                ChangeState<MainMenuProcedure>(procedureOwner);
            }
        }
    }

    private bool IsCurrent(IFsm<IProcedureModule> owner, int version, CancellationToken cancellation) =>
        !cancellation.IsCancellationRequested && version == _loadVersion &&
        !owner.IsDestroyed && ReferenceEquals(owner.CurrentState, this);

    protected internal override void OnLeave(IFsm<IProcedureModule> procedureOwner, bool isShutdown)
    {
        CancelPendingLoad();
        base.OnLeave(procedureOwner, isShutdown);
    }

    private void CancelPendingLoad()
    {
        _loadVersion++;
        var cancellation = _loadCancellation;
        _loadCancellation = null;
        cancellation?.Cancel();
        cancellation?.Dispose();
    }
}
