namespace GameLogic;

public enum AbilityActivationState { Running, Completed, Cancelled }

// Tracks one activation even when the same AbilityRuntime instance is restarted.
public sealed class AbilityActivationHandle
{
    private readonly AbilityRuntime _runtime;
    private readonly ulong _version;
    public AbilityActivationHandle(AbilityRuntime runtime)
    { _runtime = runtime; _version = runtime?.ActivationVersion ?? 0; }
    private bool IsCurrent => _runtime != null && Godot.GodotObject.IsInstanceValid(_runtime.System)
        && _runtime.System.GetRuntime(_runtime.AbilityId) == _runtime && _runtime.ActivationVersion == _version;
    public AbilityActivationState State => !IsCurrent ? AbilityActivationState.Cancelled
        : _runtime.IsRunning ? AbilityActivationState.Running
        : _runtime.IsCompleted ? AbilityActivationState.Completed : AbilityActivationState.Cancelled;
    public string ReturnLabel => IsCurrent ? _runtime.LastReturnLabel : "Replaced";
    public void Cancel(string reason)
    {
        if (State == AbilityActivationState.Running) _runtime.System.CancelAbility(_runtime.AbilityId, reason);
    }
}
