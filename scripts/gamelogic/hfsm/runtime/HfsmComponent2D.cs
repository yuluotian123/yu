using Godot;
using System.Collections.Generic;

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
        [Export] public bool IncludeTagsInDebugText { get; set; } = true;
        [Export] public NodePath DebugStateLabelPath { get; set; } = new("");

        public override int Priority => ComponentPriority.State;

        public HfsmRuntime Runtime { get; private set; }
        public string CurrentStateName => Runtime?.CurrentStateName ?? string.Empty;
        public string CurrentStatePath => Runtime?.CurrentStatePath ?? string.Empty;

        private Label _debugStateLabel;
        private GraphRuntimeDebugHandle _debugHandle;

        public override void OnInit()
        {
            if (Graph == null)
            {
                GD.PushWarning("[HfsmComponent2D] Graph is not assigned.");
                return;
            }

            Runtime = new HfsmRuntime(Graph, this);
            Runtime.StateChanged += OnRuntimeStateChanged;
            Runtime.StateEntered += OnRuntimeStateEntered;
            Runtime.StateExited += OnRuntimeStateExited;
            _debugHandle = GraphRuntimeDebugRegistry.Register(Owner, Runtime, Graph, "HFSM", CreateRuntimeDebugMetadata);
            ResolveDebugStateLabel();
            OnBeforeStartRuntime();

            if (!Runtime.Start(InitialStateName))
            {
                GD.PushWarning($"[HfsmComponent2D] Failed to start HFSM graph: {Graph.ResourcePath}");
                _debugHandle?.Dispose();
                _debugHandle = null;
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
            _debugHandle?.Dispose();
            _debugHandle = null;
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

        protected virtual void OnBeforeStartRuntime()
        {
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

            if (!IncludeTagsInDebugText)
                return $"HFSM: {Runtime.CurrentStatePath}";

            string tags = string.Join(",", Runtime.GetCurrentStateTags());
            return string.IsNullOrWhiteSpace(tags)
                ? $"HFSM: {Runtime.CurrentStatePath}"
                : $"HFSM: {Runtime.CurrentStatePath} [{tags}]";
        }

        private string GetDebugOwnerName()
        {
            return Owner?.Name.ToString() ?? Graph?.ResourcePath ?? "Unknown";
        }

        private IEnumerable<string> CreateRuntimeDebugMetadata()
        {
            if (Runtime == null)
                yield break;

            yield return $"CurrentState={Runtime.CurrentStatePath}";
            yield return $"StateTime={Runtime.CurrentStateTime:0.###}";

            string tags = string.Join(",", Runtime.GetCurrentStateTags());
            if (!string.IsNullOrWhiteSpace(tags))
                yield return $"Tags={tags}";
        }
    }
}
