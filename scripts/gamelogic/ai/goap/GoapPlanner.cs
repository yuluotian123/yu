using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace GameLogic.Goap;

/// <summary>A grounded action: target identity and costs are bound before search.</summary>
public sealed class GoapAction
{
    public string Id { get; }
    public string Operation { get; }
    public string TargetId { get; }
    public double Cost { get; }
    public IReadOnlyList<GoapCondition> Preconditions { get; }
    public IReadOnlyList<GoapEffect> Effects { get; }
    private readonly Func<GoapWorldState, GoapWorldState> _transition;
    private readonly Func<GoapWorldState,double> _cost;
    public GoapAction(string id, string targetId, double cost, Func<GoapWorldState, GoapWorldState> transition,Func<GoapWorldState,double> dynamicCost=null)
        : this(id, id, targetId, cost, Array.Empty<GoapCondition>(), Array.Empty<GoapEffect>())
        { _transition = transition ?? throw new ArgumentNullException(nameof(transition));_cost=dynamicCost; }
    public double GetCost(GoapWorldState state)=>_cost?.Invoke(state)??Cost;
    public bool TryApply(GoapWorldState state, out GoapWorldState next)
    {
        if (_transition != null) { next = _transition(state); return next != null; }
        next = null;
        return state.Satisfies(Preconditions) && state.TryApply(Effects, out next);
    }
    public GoapAction(string id, string operation, string targetId, double cost,
        IEnumerable<GoapCondition> preconditions, IEnumerable<GoapEffect> effects)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(operation)) throw new ArgumentException("Action identity is required.");
        if (!double.IsFinite(cost) || cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
        (Id, Operation, TargetId, Cost) = (id, operation, targetId ?? "", cost);
        Preconditions = Array.AsReadOnly(preconditions.ToArray());
        Effects = Array.AsReadOnly(effects.ToArray());
    }
}

public sealed class GoapGoal
{
    private readonly Func<GoapWorldState, bool> _predicate;
    public GoapGoal(string id, Func<GoapWorldState, bool> predicate)
    { Id = id; _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate)); Conditions = Array.Empty<GoapCondition>(); }
    public bool IsMet(GoapWorldState state) => _predicate?.Invoke(state) ?? state.Satisfies(Conditions);
    public string Id { get; }
    public IReadOnlyList<GoapCondition> Conditions { get; }
    public GoapGoal(string id, params GoapCondition[] conditions)
    {
        if (string.IsNullOrWhiteSpace(id) || conditions.Length == 0) throw new ArgumentException("A goal needs an identity and conditions.");
        Id = id;
        Conditions = Array.AsReadOnly((GoapCondition[])conditions.Clone());
    }
}

public enum GoapSearchStatus { Pending, Found, AlreadySatisfied, NoPlan, LimitReached, Invalidated }

public sealed record GoapSearchLimits(int MaxExpanded = 2048, int MaxDepth = 16)
{
    public void Validate()
    {
        if (MaxExpanded < 1 || MaxDepth < 1) throw new ArgumentOutOfRangeException(nameof(MaxExpanded));
    }
}

public sealed class GoapPlan
{
    public string GoalId { get; }
    public IReadOnlyList<GoapAction> Steps { get; }
    public double Cost { get; }
    public long SourceVersion { get; }
    internal GoapPlan(string goalId, IEnumerable<GoapAction> steps, double cost, long sourceVersion)
        => (GoalId, Steps, Cost, SourceVersion) = (goalId, new ReadOnlyCollection<GoapAction>(steps.ToArray()), cost, sourceVersion);
}

/// <summary>Resumable uniform-cost A* (h=0). No Godot calls or mutations of live state.</summary>
public sealed class GoapPlanner
{
    private sealed record Node(GoapWorldState State, Node Parent, GoapAction Action, double Cost, int Depth);
    private readonly PriorityQueue<Node, (double Cost, long Sequence)> _open = new();
    private readonly Dictionary<string, List<(double Cost, int Depth)>> _visited = new(StringComparer.Ordinal);
    private readonly GoapAction[] _actions;
    private readonly GoapGoal _goal;
    private readonly GoapSearchLimits _limits;
    private readonly long _sourceVersion;
    private long _sequence;
    private bool _depthLimited;
    public GoapSearchStatus Status { get; private set; } = GoapSearchStatus.Pending;
    public GoapPlan Plan { get; private set; }
    public int Expanded { get; private set; }
    public int Queued => _open.Count;
    public double SearchMilliseconds { get; private set; }

    public GoapPlanner(GoapWorldState initial, GoapGoal goal, IEnumerable<GoapAction> actions,
        long sourceVersion = 0, GoapSearchLimits limits = null)
    {
        ArgumentNullException.ThrowIfNull(initial);
        _goal = goal ?? throw new ArgumentNullException(nameof(goal));
        _limits = limits ?? new();
        _limits.Validate();
        _sourceVersion = sourceVersion;
        _actions = actions.OrderBy(action => action.Id, StringComparer.Ordinal).ToArray();
        if (_actions.Select(action => action.Id).Distinct(StringComparer.Ordinal).Count() != _actions.Length)
            throw new ArgumentException("Grounded action IDs must be unique.");
        if (goal.IsMet(initial))
        {
            Status = GoapSearchStatus.AlreadySatisfied;
            Plan = new(goal.Id, Array.Empty<GoapAction>(), 0, sourceVersion);
            return;
        }
        Record(initial.StateKey, 0, 0);
        _open.Enqueue(new(initial, null, null, 0, 0), (0, _sequence++));
    }

    public GoapSearchStatus Advance(int nodeBudget = 32, double millisecondBudget = double.PositiveInfinity)
    {
        if (nodeBudget < 1 || double.IsNaN(millisecondBudget) || millisecondBudget <= 0) throw new ArgumentOutOfRangeException(nameof(nodeBudget));
        if (Status != GoapSearchStatus.Pending) return Status;
        var timer = Stopwatch.StartNew();
        int expandedThisCall = 0;
        while (_open.Count > 0 && expandedThisCall < nodeBudget && timer.Elapsed.TotalMilliseconds < millisecondBudget)
        {
            Node node = _open.Dequeue();
            if (Dominated(node.State.StateKey, node.Cost, node.Depth, strict: true)) continue;
            if (_goal.IsMet(node.State))
            {
                var path = new List<GoapAction>();
                for (Node step = node; step.Action != null; step = step.Parent) path.Add(step.Action);
                path.Reverse();
                Plan = new(_goal.Id, path, node.Cost, _sourceVersion);
                Status = GoapSearchStatus.Found;
                break;
            }
            if (Expanded >= _limits.MaxExpanded) { Status = GoapSearchStatus.LimitReached; break; }
            Expanded++;
            expandedThisCall++;
            foreach (var action in _actions)
            {
                if (!action.TryApply(node.State, out var next)) continue;
                double stepCost=action.GetCost(node.State);
                if(!double.IsFinite(stepCost)||stepCost<0)continue;
                double cost = node.Cost + stepCost;
                if (!double.IsFinite(cost) || Dominated(next.StateKey, cost, node.Depth + 1)) continue;
                if (node.Depth >= _limits.MaxDepth) { _depthLimited = true; continue; }
                Record(next.StateKey, cost, node.Depth + 1);
                _open.Enqueue(new(next, node, action, cost, node.Depth + 1), (cost, _sequence++));
            }
        }
        if (Status == GoapSearchStatus.Pending && _open.Count == 0)
            Status = _depthLimited ? GoapSearchStatus.LimitReached : GoapSearchStatus.NoPlan;
        SearchMilliseconds += timer.Elapsed.TotalMilliseconds;
        return Status;
    }

    public void Invalidate()
    {
        Status = GoapSearchStatus.Invalidated;
        Plan = null;
        _open.Clear();
    }
    private bool Dominated(string key, double cost, int depth, bool strict = false)
        => _visited.TryGetValue(key, out var paths) && paths.Any(path => path.Cost <= cost && path.Depth <= depth
            && (!strict || path.Cost < cost || path.Depth < depth));
    private void Record(string key, double cost, int depth)
    {
        if (!_visited.TryGetValue(key, out var paths)) _visited[key] = paths = new();
        paths.RemoveAll(path => cost <= path.Cost && depth <= path.Depth);
        paths.Add((cost, depth));
    }
}
