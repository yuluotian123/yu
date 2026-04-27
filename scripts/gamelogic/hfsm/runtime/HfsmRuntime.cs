using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic
{
    public sealed class HfsmRuntime
    {
        private readonly HashSet<string> _triggers = new(StringComparer.Ordinal);
        private HfsmRuntime _childRuntime;

        public HfsmRuntime(HfsmGraphAsset graph, GraphBlackboardNode globalBlackboard = null)
            : this(graph, new GraphBlackboardRuntime(globalBlackboard), false)
        {
        }

        public HfsmRuntime(HfsmGraphAsset graph, GraphBlackboardRuntime parentBlackboard)
            : this(graph, parentBlackboard?.ForkSharedLocals() ?? new GraphBlackboardRuntime(), true)
        {
        }

        private HfsmRuntime(HfsmGraphAsset graph, GraphBlackboardRuntime blackboard, bool _)
        {
            Graph = graph;
            Blackboard = blackboard ?? new GraphBlackboardRuntime();
            Blackboard.PushLocal(graph);
            Context = new GraphExecutionContext(graph, Blackboard);
        }

        public HfsmGraphAsset Graph { get; }
        public GraphBlackboardRuntime Blackboard { get; }
        public GraphExecutionContext Context { get; }
        public IHfsmStateNodeData CurrentState { get; private set; }
        public HfsmRuntime ChildRuntime => _childRuntime;
        public string CurrentStateName => CurrentState?.StateName ?? string.Empty;
        public double CurrentStateTime { get; private set; }
        public bool IsRunning => CurrentState != null;

        public string CurrentStatePath
        {
            get
            {
                if (!IsRunning)
                    return string.Empty;

                if (_childRuntime?.IsRunning == true)
                    return $"{CurrentStateName}/{_childRuntime.CurrentStatePath}";

                return CurrentStateName;
            }
        }

        public event Action<HfsmRuntime, IHfsmStateNodeData, IHfsmStateNodeData, HfsmTransitionConnection> StateChanged;
        public event Action<HfsmRuntime, IHfsmStateNodeData> StateEntered;
        public event Action<HfsmRuntime, IHfsmStateNodeData> StateUpdated;
        public event Action<HfsmRuntime, IHfsmStateNodeData> StateExited;

        public bool Start(string initialStateName = null)
        {
            if (Graph == null)
                return false;

            IHfsmStateNodeData initialState = Graph.GetInitialState(initialStateName);
            if (initialState == null)
                return false;

            if (IsRunning)
                Stop();

            CurrentState = initialState;
            CurrentStateTime = 0d;
            EnterCurrentState();
            return true;
        }

        public void Stop()
        {
            if (!IsRunning)
                return;

            ExitCurrentState();
            CurrentState = null;
            CurrentStateTime = 0d;
            _triggers.Clear();
        }

        public void Update(double delta)
        {
            if (!IsRunning)
                return;

            CurrentStateTime += delta;

            try
            {
                CurrentState.OnUpdate(this, delta);
                StateUpdated?.Invoke(this, CurrentState);

                _childRuntime?.Update(delta);

                HfsmTransitionConnection transition = Graph
                    .GetOutgoingTransitions(CurrentState.Id)
                    .FirstOrDefault(CanUseTransition);

                if (transition != null)
                    ChangeStateById(transition.ToNode, transition);
            }
            finally
            {
                _triggers.Clear();
            }
        }

        public void Trigger(string triggerName)
        {
            if (string.IsNullOrWhiteSpace(triggerName))
                return;

            _triggers.Add(triggerName);
            _childRuntime?.Trigger(triggerName);
        }

        public bool HasTrigger(string triggerName)
        {
            return !string.IsNullOrWhiteSpace(triggerName) && _triggers.Contains(triggerName);
        }

        public bool ChangeState(string stateNameOrId)
        {
            if (Graph == null)
                return false;

            IHfsmStateNodeData state = Graph.FindState(stateNameOrId);
            return state != null && ChangeState(state, null);
        }

        public bool CurrentStateHasTag(string tag)
        {
            return CurrentState?.HasTag(tag) == true;
        }

        public IReadOnlyList<string> GetCurrentStateTags()
        {
            return CurrentState?.GetTags() ?? Array.Empty<string>();
        }

        public bool TryGetValue<T>(string key, out T value)
        {
            return Blackboard.TryGetValue(key, out value);
        }

        public T GetValue<T>(string key, T defaultValue = default)
        {
            return Blackboard.GetValue(key, defaultValue);
        }

        public bool SetValue<T>(string key, T value)
        {
            return Blackboard.SetValue(key, value);
        }

        public bool SetGlobalValue<T>(string key, T value)
        {
            return Blackboard.SetGlobalValue(key, value);
        }

        private bool ChangeStateById(string stateId, HfsmTransitionConnection transition)
        {
            IHfsmStateNodeData state = Graph.FindStateById(stateId);
            return state != null && ChangeState(state, transition);
        }

        private bool ChangeState(IHfsmStateNodeData nextState, HfsmTransitionConnection transition)
        {
            if (nextState == null || nextState == CurrentState)
                return false;

            IHfsmStateNodeData previousState = CurrentState;
            ExitCurrentState();

            CurrentState = nextState;
            CurrentStateTime = 0d;
            StateChanged?.Invoke(this, previousState, CurrentState, transition);
            EnterCurrentState();
            return true;
        }

        private void EnterCurrentState()
        {
            if (CurrentState == null)
                return;

            CurrentState.OnEnter(this);
            StateEntered?.Invoke(this, CurrentState);

            if (CurrentState is HfsmCompositeStateNodeData compositeState)
                StartChildRuntime(compositeState);
        }

        private void ExitCurrentState()
        {
            if (CurrentState == null)
                return;

            if (_childRuntime != null)
            {
                _childRuntime.Stop();
                _childRuntime = null;
            }

            CurrentState.OnExit(this);
            StateExited?.Invoke(this, CurrentState);
        }

        private void StartChildRuntime(HfsmCompositeStateNodeData compositeState)
        {
            HfsmGraphAsset subGraph = compositeState.GetSubGraph();
            if (subGraph == null)
                return;

            _childRuntime = new HfsmRuntime(subGraph, Blackboard);
            if (!_childRuntime.Start())
            {
                GD.PushWarning($"[HFSM] Failed to start child graph: {compositeState.SubGraphPath}");
                _childRuntime = null;
            }
        }

        private bool CanUseTransition(HfsmTransitionConnection transition)
        {
            return transition != null && transition.CanUse(this);
        }
    }
}
