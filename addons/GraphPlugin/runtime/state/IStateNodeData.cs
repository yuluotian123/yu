public interface IStateNodeData
{
    string Id { get; set; }
    string StateName { get; set; }
    bool IsDefault { get; set; }

    bool CanEnter(StateGraphRuntime runtime);
    void OnEnter(StateGraphRuntime runtime);
    void OnUpdate(StateGraphRuntime runtime, double delta);
    bool TryGetCompletion(StateGraphRuntime runtime, out NodeCompletion completion);
    void OnExit(StateGraphRuntime runtime);
}
