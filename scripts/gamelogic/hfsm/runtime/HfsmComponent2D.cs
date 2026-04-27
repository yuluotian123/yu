using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class HfsmComponent2D : Component2D
    {
        [Export] public HfsmGraphAsset Graph { get; set; }
        [Export] public string InitialStateName { get; set; } = string.Empty;
        [Export] public bool UpdateInPhysics { get; set; } = true;

        public override int Priority => ComponentPriority.State;

        public HfsmRuntime Runtime { get; private set; }
        public string CurrentStateName => Runtime?.CurrentStateName ?? string.Empty;
        public string CurrentStatePath => Runtime?.CurrentStatePath ?? string.Empty;

        public override void OnInit()
        {
            if (Graph == null)
            {
                GD.PushWarning("[HfsmComponent2D] Graph is not assigned.");
                return;
            }

            Runtime = new HfsmRuntime(Graph);
            if (!Runtime.Start(InitialStateName))
                GD.PushWarning($"[HfsmComponent2D] Failed to start HFSM graph: {Graph.ResourcePath}");
        }

        public override void OnUpdate(double delta)
        {
            if (!UpdateInPhysics)
                Runtime?.Update(delta);
        }

        public override void OnPhysicsUpdate(double delta)
        {
            if (UpdateInPhysics)
                Runtime?.Update(delta);
        }

        public override void OnDestroy()
        {
            Runtime?.Stop();
            Runtime = null;
        }

        public void Trigger(string triggerName)
        {
            Runtime?.Trigger(triggerName);
        }

        public bool ChangeState(string stateNameOrId)
        {
            return Runtime?.ChangeState(stateNameOrId) == true;
        }

        public bool CurrentStateHasTag(string tag)
        {
            return Runtime?.CurrentStateHasTag(tag) == true;
        }

        public bool SetValue<T>(string key, T value)
        {
            return Runtime?.SetValue(key, value) == true;
        }

        public T GetValue<T>(string key, T defaultValue = default)
        {
            return Runtime != null ? Runtime.GetValue(key, defaultValue) : defaultValue;
        }
    }
}
