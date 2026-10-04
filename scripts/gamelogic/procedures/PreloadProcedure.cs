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
    private CancellationTokenSource _loadCancellation;
    private SceneHandle _pendingScene;
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
        SceneHandle handle = null;
        Node level = null;
        bool transferred = false;
        try
        {
            var resource = ModuleSystem.GetModule<IResourceModule>();
            handle = resource.LoadSceneAsync("res://assets/scenes/spacelevel.tscn")
                .WithCancellation(cancellation);
            _pendingScene = handle;
            await handle.Task;
            if (!IsCurrent(procedureOwner, version, cancellation))
                return;
            if (!handle.IsValid)
                throw new InvalidOperationException($"Level loading failed: {handle.Error}");
            if (Engine.GetMainLoop() is not SceneTree tree ||
                tree.Root.GetNodeOrNull<Node>("Root") is not Node root || root.IsQueuedForDeletion())
                throw new InvalidOperationException("The level owner is unavailable.");

            ModuleSystem.GetModule<ISaveModule>().Load();
            if (!IsCurrent(procedureOwner, version, cancellation))
                return;
            level = handle.InstantiateAndBind<Node>(root);
            if (level == null)
                throw new InvalidOperationException("The level could not be instantiated.");
            _pendingScene = null;
            ModuleSystem.GetModule<IUIModule>().CloseAll();
            ChangeState<LevelProcedure>(procedureOwner);
            transferred = true;
        }
        catch (Exception exception)
        {
            if (IsCurrent(procedureOwner, version, cancellation))
            {
                Debugger.Warn($"[PreloadProcedure] {exception.Message}");
                ChangeState<MainMenuProcedure>(procedureOwner);
            }
        }
        finally
        {
            if (!transferred)
            {
                if (GodotObject.IsInstanceValid(level))
                    level.QueueFree();
                handle?.Dispose();
            }
            if (ReferenceEquals(_pendingScene, handle))
                _pendingScene = null;
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
        var handle = _pendingScene;
        _loadCancellation = null;
        _pendingScene = null;
        cancellation?.Cancel();
        handle?.Dispose();
        cancellation?.Dispose();
    }
}
