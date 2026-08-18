using Godot;
namespace GameLogic
{
    [GlobalClass]
    public partial class HfsmComponent2D : Component2D
    {
        [Export] public HfsmGraphAsset Graph { get; set; }
        [Export] public string InitialStateName { get; set; } = string.Empty;
        [Export] public bool UpdateInPhysics { get; set; } = true;

        [ExportGroup("Debug")]
        [Export] public bool LogStateChanges { get; set; }
        [Export] public NodePath DebugStateLabelPath { get; set; } = new("");

        public override int Priority => ComponentPriority.State;

        public HfsmRuntime Runtime { get; private set; }
        public string CurrentStateName => Runtime?.CurrentStateName ?? string.Empty;
        public string CurrentStatePath => Runtime?.CurrentStatePath ?? string.Empty;

        private Label _debugStateLabel;

        public override void OnInit()
        {
            HfsmGraphAsset graph = ResolveGraph();
            if (graph == null)
            {
                GD.PushWarning("[HfsmComponent2D] Graph is not assigned.");
                return;
            }

            Runtime = new HfsmRuntime(graph, this);
            Runtime.StateChanged += OnRuntimeStateChanged;
            Runtime.StateEntered += OnRuntimeStateEntered;
            Runtime.StateExited += OnRuntimeStateExited;
            ResolveDebugStateLabel();
            OnBeforeStartRuntime();

            if (!Runtime.Start(InitialStateName))
            {
                GD.PushWarning($"[HfsmComponent2D] Failed to start HFSM graph: {Graph.ResourcePath}");
            }
            else
            {
                LogCurrentState("Start");
                UpdateDebugStateLabel();
            }

            OnAfterStartRuntime();
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
            if (Runtime != null)
            {
                Runtime.StateChanged -= OnRuntimeStateChanged;
                Runtime.StateEntered -= OnRuntimeStateEntered;
                Runtime.StateExited -= OnRuntimeStateExited;
            }

            Runtime?.Stop();
            Runtime = null;
            _debugStateLabel = null;
        }

        public void Trigger(string triggerName)
        {
            Runtime?.Trigger(triggerName);
        }

        public bool ChangeState(string stateNameOrId)
        {
            return Runtime?.ChangeState(stateNameOrId) == true;
        }

        public bool SetValue<T>(string key, T value)
        {
            return Runtime?.SetValue(key, value) == true;
        }

        public T GetValue<T>(string key, T defaultValue = default)
        {
            return Runtime != null ? Runtime.GetValue(key, defaultValue) : defaultValue;
        }

        protected virtual void OnBeforeStartRuntime()
        {
        }

        protected virtual HfsmGraphAsset ResolveGraph()
        {
            return Graph;
        }

        protected virtual void OnAfterStartRuntime()
        {
        }

        private void OnRuntimeStateChanged(
            HfsmRuntime runtime,
            IHfsmStateNodeData previousState,
            IHfsmStateNodeData nextState,
            HfsmTransitionConnection transition)
        {
            string transitionName = string.IsNullOrWhiteSpace(transition?.TransitionName)
                ? "transition"
                : transition.TransitionName;

            if (LogStateChanges)
            {
                string currentPath = Runtime?.CurrentStatePath ?? nextState?.StateName ?? "<none>";
                GD.Print(
                    $"[HFSM:{GetDebugOwnerName()}] {previousState?.StateName ?? "<none>"} -> {nextState?.StateName ?? "<none>"} ({transitionName}) => {currentPath}");
            }

            UpdateDebugStateLabel();
        }

        private void OnRuntimeStateEntered(HfsmRuntime runtime, IHfsmStateNodeData state)
        {
            UpdateDebugStateLabel();
        }

        private void OnRuntimeStateExited(HfsmRuntime runtime, IHfsmStateNodeData state)
        {
            UpdateDebugStateLabel();
        }

        private void LogCurrentState(string reason)
        {
            if (!LogStateChanges || Runtime == null)
                return;

            GD.Print($"[HFSM:{GetDebugOwnerName()}] {reason}: {GetDebugStateText()}");
        }

        private void ResolveDebugStateLabel()
        {
            _debugStateLabel = null;
            string path = DebugStateLabelPath?.ToString();
            if (string.IsNullOrWhiteSpace(path))
                return;

            _debugStateLabel = Owner?.GetNodeOrNull<Label>(DebugStateLabelPath);
            if (_debugStateLabel == null)
                GD.PushWarning($"[HfsmComponent2D] DebugStateLabelPath not found: {path}");
        }

        private void UpdateDebugStateLabel()
        {
            if (_debugStateLabel != null)
                _debugStateLabel.Text = GetDebugStateText();
        }

        private string GetDebugStateText()
        {
            if (Runtime == null || !Runtime.IsRunning)
                return "HFSM: <stopped>";

            return $"HFSM: {Runtime.CurrentStatePath}";
        }

        private string GetDebugOwnerName()
        {
            return Owner?.Name.ToString() ?? Graph?.ResourcePath ?? "Unknown";
        }

    }
}
