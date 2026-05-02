using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public class StateGraphRuntime : IGraphRuntimeScope
{
    private readonly HashSet<string> _triggers = new(StringComparer.Ordinal);
    private StateGraphRuntime _childRuntime;
    private bool _localBlackboardPushed;

    public StateGraphRuntime(StateGraphAsset graph, GraphBlackboardNode globalBlackboard = null)
        : this(graph, new GraphExecutionContext(graph, new GraphBlackboardRuntime(globalBlackboard)), false)
    {
    }

    public StateGraphRuntime(StateGraphAsset graph, GraphExecutionContext context)
        : this(graph, context, false)
    {
    }

    public StateGraphRuntime(StateGraphAsset graph, GraphBlackboardRuntime parentBlackboard, GraphExecutionContext parentContext = null)
        : this(graph, CreateChildContext(graph, parentBlackboard, parentContext), true)
    {
    }

    private StateGraphRuntime(StateGraphAsset graph, GraphExecutionContext context, bool _)
    {
        Graph = graph;
        Context = context ?? new GraphExecutionContext(graph, new GraphBlackboardRuntime());
        Blackboard = Context.Blackboard;
    }

    public StateGraphAsset Graph { get; }
    public GraphExecutionContext Context { get; }
    public GraphBlackboardRuntime Blackboard { get; }
    public IStateNodeData CurrentState { get; private set; }
    public StateGraphRuntime ChildRuntime => _childRuntime;
    /// <summary>
    /// 当前 StateGraph 正在运行的子图作用域。没有进入 Composite State 时为空集合。
    /// </summary>
    public IEnumerable<IGraphRuntimeScope> ChildScopes
    {
        get
        {
            if (_childRuntime != null)
                yield return _childRuntime;
        }
    }

    public string CurrentStateName => CurrentState?.StateName ?? string.Empty;
    public string CurrentStateId => CurrentState?.Id ?? string.Empty;
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

    public event Action<StateGraphRuntime, IStateNodeData, IStateNodeData, StateTransitionConnection> StateChanged;
    public event Action<StateGraphRuntime, IStateNodeData> StateEntered;
    public event Action<StateGraphRuntime, IStateNodeData> StateUpdated;
    public event Action<StateGraphRuntime, IStateNodeData> StateExited;

    public bool Start(string initialStateName = null)
    {
        if (Graph == null)
            return false;

        if (!Graph.Validate(out GraphValidationResult validation))
        {
            GD.PushWarning($"[StateGraphRuntime] 图验证失败，无法启动：\n{validation.ToDisplayText()}");
            return false;
        }

        IStateNodeData initialState = Graph.GetInitialState(initialStateName);
        if (initialState == null)
            return false;

        if (IsRunning)
            Stop();

        if (!_localBlackboardPushed)
        {
            Blackboard.PushLocal(Graph);
            _localBlackboardPushed = true;
        }

        CurrentState = initialState;
        CurrentStateTime = 0d;
        GraphRuntimeDebugRegistry.RecordEvent(this, "Start", "state graph started", Graph, CurrentState as GraphNodeData);
        EnterCurrentState();
        GraphRuntimeDebugRegistry.CaptureContext(this, Context, true);
        return true;
    }

    public void Stop()
    {
        if (!IsRunning && !_localBlackboardPushed)
            return;

        if (IsRunning)
        {
            ExitCurrentState();
            CurrentState = null;
            CurrentStateTime = 0d;
            _triggers.Clear();
        }

        if (_localBlackboardPushed)
        {
            Blackboard.PopLocal();
            _localBlackboardPushed = false;
        }

        GraphRuntimeDebugRegistry.RecordEvent(this, "Stop", "state graph stopped", Graph);
        GraphRuntimeDebugRegistry.CaptureContext(this, Context, true);
    }

    public void Update(double delta)
    {
        if (!IsRunning)
            return;

        CurrentStateTime += delta;

        try
        {
            if (TryUseAnyStateTransition())
                return;

            CurrentState.OnUpdate(this, delta);
            StateUpdated?.Invoke(this, CurrentState);

            _childRuntime?.Update(delta);

            if (TryUseCompletionTransition())
                return;

            TryUseCurrentStateTransition();
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

        IStateNodeData state = Graph.FindState(stateNameOrId);
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
        return GraphRuntimeBlackboardWriter.SetValueRecursive(this, key, value);
    }

    public bool SetGlobalValue<T>(string key, T value)
    {
        return Blackboard.SetGlobalValue(key, value);
    }

    private bool ChangeState(IStateNodeData nextState, StateTransitionConnection transition)
    {
        if (nextState == null || nextState == CurrentState)
            return false;

        IStateNodeData previousState = CurrentState;
        ExitCurrentState();

        CurrentState = nextState;
        CurrentStateTime = 0d;
        StateChanged?.Invoke(this, previousState, CurrentState, transition);
        GraphRuntimeDebugRegistry.RecordEvent(
            this,
            "StateChanged",
            $"{previousState?.StateName ?? "<none>"} -> {CurrentState?.StateName ?? "<none>"}",
            Graph,
            CurrentState as GraphNodeData);
        GraphRuntimeDebugRegistry.CaptureContext(this, Context, true);
        EnterCurrentState();
        return true;
    }

    private void EnterCurrentState()
    {
        if (CurrentState == null)
            return;

        CurrentState.OnEnter(this);
        StateEntered?.Invoke(this, CurrentState);
        GraphRuntimeDebugRegistry.RecordEvent(this, "StateEntered", CurrentState.StateName, Graph, CurrentState as GraphNodeData);
        GraphRuntimeDebugRegistry.CaptureContext(this, Context, true);

        if (CurrentState is CompositeStateNodeData compositeState)
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

        IStateNodeData exitingState = CurrentState;
        CurrentState.OnExit(this);
        StateExited?.Invoke(this, exitingState);
        GraphRuntimeDebugRegistry.RecordEvent(this, "StateExited", exitingState.StateName, Graph, exitingState as GraphNodeData);
        GraphRuntimeDebugRegistry.CaptureContext(this, Context, true);
    }

    private void StartChildRuntime(CompositeStateNodeData compositeState)
    {
        StateGraphAsset subGraph = compositeState.GetSubGraph() as StateGraphAsset;
        if (subGraph == null)
            return;

        _childRuntime = CreateChildRuntime(subGraph);
        if (_childRuntime == null)
            return;

        AttachChildRuntimeEvents(_childRuntime);
        if (!_childRuntime.Start())
        {
            GD.PushWarning($"[StateGraph] Failed to start child graph: {compositeState.SubGraphPath}");
            DetachChildRuntimeEvents(_childRuntime);
            _childRuntime = null;
            return;
        }

        _childRuntime.TryUseAnyStateTransition();
    }

    protected virtual StateGraphRuntime CreateChildRuntime(StateGraphAsset subGraph)
    {
        return new StateGraphRuntime(subGraph, Blackboard, Context);
    }

    private void AttachChildRuntimeEvents(StateGraphRuntime childRuntime)
    {
        if (childRuntime == null)
            return;

        childRuntime.StateChanged += OnChildStateChanged;
        childRuntime.StateEntered += OnChildStateEntered;
        childRuntime.StateUpdated += OnChildStateUpdated;
        childRuntime.StateExited += OnChildStateExited;
    }

    private void DetachChildRuntimeEvents(StateGraphRuntime childRuntime)
    {
        if (childRuntime == null)
            return;

        childRuntime.StateChanged -= OnChildStateChanged;
        childRuntime.StateEntered -= OnChildStateEntered;
        childRuntime.StateUpdated -= OnChildStateUpdated;
        childRuntime.StateExited -= OnChildStateExited;
    }

    private void OnChildStateChanged(
        StateGraphRuntime runtime,
        IStateNodeData previousState,
        IStateNodeData nextState,
        StateTransitionConnection transition)
    {
        StateChanged?.Invoke(runtime, previousState, nextState, transition);
    }

    private void OnChildStateEntered(StateGraphRuntime runtime, IStateNodeData state)
    {
        StateEntered?.Invoke(runtime, state);
    }

    private void OnChildStateUpdated(StateGraphRuntime runtime, IStateNodeData state)
    {
        StateUpdated?.Invoke(runtime, state);
    }

    private void OnChildStateExited(StateGraphRuntime runtime, IStateNodeData state)
    {
        StateExited?.Invoke(runtime, state);
    }

    private bool CanUseTransition(StateTransitionConnection transition)
    {
        return transition != null && transition.CanUse(this);
    }

    private bool TryUseAnyStateTransition()
    {
        return TryUseTransitions(Graph.GetAnyStateTransitions(CurrentState));
    }

    private bool TryUseCompletionTransition()
    {
        if (CurrentState == null ||
            !CurrentState.TryGetCompletion(this, out NodeCompletion completion))
        {
            return false;
        }

        return TryUseTransitions(Graph.GetOutgoingTransitions(CurrentState.Id, completion.OutputPort));
    }

    private bool TryUseCurrentStateTransition()
    {
        IEnumerable<StateTransitionConnection> transitions = Graph
            .GetOutgoingTransitions(CurrentState.Id)
            .Where(transition => !transition.CompletionOnly);

        return TryUseTransitions(transitions);
    }

    private bool TryUseTransitions(IEnumerable<StateTransitionConnection> transitions)
    {
        if (transitions == null)
            return false;

        foreach (StateTransitionConnection transition in transitions.OrderByDescending(transition => transition.Priority))
        {
            if (!CanUseTransition(transition))
                continue;

            IStateNodeData targetState = ResolveTransitionTarget(transition.ToNode, 0);
            if (targetState == null ||
                targetState == CurrentState ||
                !targetState.CanEnter(this))
            {
                continue;
            }

            ChangeState(targetState, transition);
            return true;
        }

        return false;
    }

    private IStateNodeData ResolveTransitionTarget(string nodeId, int depth)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || depth > 8)
            return null;

        IStateNodeData state = Graph.FindStateById(nodeId);
        if (state != null)
            return state;

        if (Graph.FindPseudoNodeById(nodeId) is StateReturnNodeData)
        {
            StateTransitionConnection returnTransition = Graph
                .GetOutgoingTransitions(nodeId)
                .FirstOrDefault(CanUseTransition);

            return returnTransition == null
                ? null
                : ResolveTransitionTarget(returnTransition.ToNode, depth + 1);
        }

        return null;
    }

    private static GraphExecutionContext CreateChildContext(
        StateGraphAsset graph,
        GraphBlackboardRuntime parentBlackboard,
        GraphExecutionContext parentContext)
    {
        var context = new GraphExecutionContext(
            graph,
            parentBlackboard?.ForkSharedLocals() ?? new GraphBlackboardRuntime());

        if (parentContext != null)
        {
            for (int i = 0; i < parentContext.UserData.Count; i++)
                context.UserData.Add(parentContext.UserData[i]);
        }

        return context;
    }
}
