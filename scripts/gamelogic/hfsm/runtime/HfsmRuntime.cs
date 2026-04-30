using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace GameLogic
{
    public sealed class HfsmRuntime
    {
        private readonly HashSet<string> _triggers = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Type> ComponentTypeCache = new(StringComparer.Ordinal);
        private HfsmRuntime _childRuntime;

        public HfsmRuntime(HfsmGraphAsset graph, GraphBlackboardNode globalBlackboard = null)
            : this(graph, null, new GraphBlackboardRuntime(globalBlackboard), false)
        {
        }

        public HfsmRuntime(HfsmGraphAsset graph, HfsmComponent2D owner, GraphBlackboardNode globalBlackboard = null)
            : this(graph, owner, new GraphBlackboardRuntime(globalBlackboard), false)
        {
        }

        public HfsmRuntime(HfsmGraphAsset graph, GraphBlackboardRuntime parentBlackboard)
            : this(graph, null, parentBlackboard?.ForkSharedLocals() ?? new GraphBlackboardRuntime(), true)
        {
        }

        public HfsmRuntime(HfsmGraphAsset graph, HfsmComponent2D owner, GraphBlackboardRuntime parentBlackboard)
            : this(graph, owner, parentBlackboard?.ForkSharedLocals() ?? new GraphBlackboardRuntime(), true)
        {
        }

        private HfsmRuntime(HfsmGraphAsset graph, HfsmComponent2D owner, GraphBlackboardRuntime blackboard, bool _)
        {
            Graph = graph;
            Owner = owner;
            Blackboard = blackboard ?? new GraphBlackboardRuntime();
            Blackboard.PushLocal(graph);
            Context = new GraphExecutionContext(graph, Blackboard);
        }

        public HfsmComponent2D Owner { get; }
        public GameObject2D GameObject => Owner?.Owner;
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

                TryUseFirstAvailableTransition();
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
            return CurrentState?.HasTag(tag) == true ||
                   _childRuntime?.CurrentStateHasTag(tag) == true;
        }

        public IReadOnlyList<string> GetCurrentStateTags()
        {
            var tags = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            AddTags(CurrentState?.GetTags());
            AddTags(_childRuntime?.GetCurrentStateTags());
            return tags;

            void AddTags(IReadOnlyList<string> source)
            {
                if (source == null)
                    return;

                for (int i = 0; i < source.Count; i++)
                {
                    string tag = source[i];
                    if (string.IsNullOrWhiteSpace(tag) || !seen.Add(tag))
                        continue;

                    tags.Add(tag);
                }
            }
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

        public T GetComponent<T>() where T : Component2D
        {
            return GameObject?.GetComponent<T>();
        }

        public Component2D GetComponent(Type componentType)
        {
            return GameObject?.GetComponent(componentType) as Component2D;
        }

        public Component2D GetComponent(string componentTypeName)
        {
            Type componentType = ResolveComponentType(componentTypeName);
            return componentType == null ? null : GetComponent(componentType);
        }

        private static Type ResolveComponentType(string componentTypeName)
        {
            if (string.IsNullOrWhiteSpace(componentTypeName))
                return null;

            string normalizedName = componentTypeName.Trim();
            if (ComponentTypeCache.TryGetValue(normalizedName, out Type cachedType))
                return cachedType;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(type => type != null).ToArray();
                }

                foreach (Type type in types)
                {
                    if (type == null ||
                        type.IsAbstract ||
                        !typeof(Component2D).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    if (!string.Equals(type.Name, normalizedName, StringComparison.Ordinal) &&
                        !string.Equals(type.FullName, normalizedName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    ComponentTypeCache[normalizedName] = type;
                    return type;
                }
            }

            return null;
        }

        private bool ChangeStateById(string stateId, HfsmTransitionConnection transition)
        {
            IHfsmStateNodeData state = ResolveTransitionTarget(stateId, 0);
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
                DetachChildRuntimeEvents(_childRuntime);
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

            _childRuntime = new HfsmRuntime(subGraph, Owner, Blackboard);
            AttachChildRuntimeEvents(_childRuntime);
            if (!_childRuntime.Start())
            {
                GD.PushWarning($"[HFSM] Failed to start child graph: {compositeState.SubGraphPath}");
                DetachChildRuntimeEvents(_childRuntime);
                _childRuntime = null;
                return;
            }

            _childRuntime.TryUseFirstAvailableTransition();
        }

        private void AttachChildRuntimeEvents(HfsmRuntime childRuntime)
        {
            if (childRuntime == null)
                return;

            childRuntime.StateChanged += OnChildStateChanged;
            childRuntime.StateEntered += OnChildStateEntered;
            childRuntime.StateUpdated += OnChildStateUpdated;
            childRuntime.StateExited += OnChildStateExited;
        }

        private void DetachChildRuntimeEvents(HfsmRuntime childRuntime)
        {
            if (childRuntime == null)
                return;

            childRuntime.StateChanged -= OnChildStateChanged;
            childRuntime.StateEntered -= OnChildStateEntered;
            childRuntime.StateUpdated -= OnChildStateUpdated;
            childRuntime.StateExited -= OnChildStateExited;
        }

        private void OnChildStateChanged(
            HfsmRuntime runtime,
            IHfsmStateNodeData previousState,
            IHfsmStateNodeData nextState,
            HfsmTransitionConnection transition)
        {
            StateChanged?.Invoke(runtime, previousState, nextState, transition);
        }

        private void OnChildStateEntered(HfsmRuntime runtime, IHfsmStateNodeData state)
        {
            StateEntered?.Invoke(runtime, state);
        }

        private void OnChildStateUpdated(HfsmRuntime runtime, IHfsmStateNodeData state)
        {
            StateUpdated?.Invoke(runtime, state);
        }

        private void OnChildStateExited(HfsmRuntime runtime, IHfsmStateNodeData state)
        {
            StateExited?.Invoke(runtime, state);
        }

        private bool CanUseTransition(HfsmTransitionConnection transition)
        {
            return transition != null && transition.CanUse(this);
        }

        private void TryUseFirstAvailableTransition()
        {
            var transitions = Graph
                .GetOutgoingTransitions(CurrentState.Id)
                .Concat(Graph.GetAnyStateTransitions(CurrentState))
                .OrderByDescending(transition => transition.Priority);

            foreach (HfsmTransitionConnection transition in transitions)
            {
                if (!CanUseTransition(transition))
                    continue;

                IHfsmStateNodeData targetState = ResolveTransitionTarget(transition.ToNode, 0);
                if (targetState == null || targetState == CurrentState)
                    continue;

                ChangeState(targetState, transition);
                return;
            }
        }

        private IHfsmStateNodeData ResolveTransitionTarget(string nodeId, int depth)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || depth > 8)
                return null;

            IHfsmStateNodeData state = Graph.FindStateById(nodeId);
            if (state != null)
                return state;

            if (Graph.FindPseudoNodeById(nodeId) is HfsmReturnStateNodeData)
            {
                HfsmTransitionConnection returnTransition = Graph
                    .GetOutgoingTransitions(nodeId)
                    .FirstOrDefault(CanUseTransition);

                return returnTransition == null
                    ? null
                    : ResolveTransitionTarget(returnTransition.ToNode, depth + 1);
            }

            return null;
        }
    }
}
