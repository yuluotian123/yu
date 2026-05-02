using System.Collections.Generic;
using Godot;

public static class GraphRuntimeDebugUtil
{
    public static List<GraphRuntimeDebugScopeSnapshot> CreateScopeSnapshots(object runtime)
    {
        var scopes = new List<GraphRuntimeDebugScopeSnapshot>();
        if (runtime is IGraphRuntimeScope scope)
            AddScope(scope, scopes, 0, new HashSet<IGraphRuntimeScope>());

        return scopes;
    }

    public static GraphRuntimeDebugScopeSnapshot FindScopeForGraph(
        IReadOnlyList<GraphRuntimeDebugScopeSnapshot> scopes,
        GraphAsset graph)
    {
        if (scopes == null || graph == null)
            return null;

        for (int i = 0; i < scopes.Count; i++)
        {
            if (GraphMatches(scopes[i].Graph, graph))
                return scopes[i];
        }

        return null;
    }

    public static bool GraphMatches(GraphAsset a, GraphAsset b)
    {
        if (a == null || b == null)
            return false;

        if (ReferenceEquals(a, b))
            return true;

        return !string.IsNullOrWhiteSpace(a.ResourcePath) &&
               !string.IsNullOrWhiteSpace(b.ResourcePath) &&
               a.ResourcePath == b.ResourcePath;
    }

    public static bool ScopeContainsRuntime(IGraphRuntimeScope scope, object runtime)
    {
        return ScopeContainsRuntime(scope, runtime, new HashSet<IGraphRuntimeScope>());
    }

    public static bool ScopeContainsContext(IGraphRuntimeScope scope, GraphExecutionContext context)
    {
        return ScopeContainsContext(scope, context, new HashSet<IGraphRuntimeScope>());
    }

    public static bool IsRunningRuntime(object runtime)
    {
        return runtime switch
        {
            FlowGraphRuntime flow => flow.IsRunning,
            StateGraphRuntime state => state.IsRunning,
            _ => false
        };
    }

    private static void AddScope(
        IGraphRuntimeScope scope,
        List<GraphRuntimeDebugScopeSnapshot> scopes,
        int depth,
        HashSet<IGraphRuntimeScope> visited)
    {
        if (scope == null || !visited.Add(scope))
            return;

        GraphExecutionContext context = scope.Context;
        GraphAsset graph = context?.Graph;
        var snapshot = new GraphRuntimeDebugScopeSnapshot
        {
            Depth = depth,
            RuntimeType = scope.GetType().Name,
            Graph = graph,
            GraphName = GraphRuntimeDebugSnapshotFactory.GetGraphName(graph),
            GraphType = graph?.GraphType ?? graph?.GetType().Name ?? string.Empty,
            GraphPath = graph?.ResourcePath ?? string.Empty,
            Context = GraphRuntimeDebugSnapshotFactory.CreateContextSnapshot(context)
        };

        PopulateRuntimeState(scope, snapshot);
        scopes.Add(snapshot);

        IEnumerable<IGraphRuntimeScope> children = scope.ChildScopes;
        if (children == null)
            return;

        foreach (IGraphRuntimeScope child in children)
            AddScope(child, scopes, depth + 1, visited);
    }

    private static void PopulateRuntimeState(IGraphRuntimeScope scope, GraphRuntimeDebugScopeSnapshot snapshot)
    {
        if (scope is FlowGraphRuntime flow)
        {
            snapshot.IsRunning = flow.IsRunning;
            snapshot.IsCompleted = flow.IsCompleted;
            IReadOnlyList<string> activeIds = flow.ActiveNodeIds;
            for (int i = 0; i < activeIds.Count; i++)
                snapshot.ActiveNodeIds.Add(activeIds[i]);
            return;
        }

        if (scope is StateGraphRuntime state)
        {
            snapshot.IsRunning = state.IsRunning;
            snapshot.CurrentStateId = state.CurrentStateId;
            snapshot.CurrentStateName = state.CurrentStateName;
            snapshot.CurrentStatePath = state.CurrentStatePath;
            snapshot.CurrentStateTime = state.CurrentStateTime;
            if (!string.IsNullOrWhiteSpace(state.CurrentStateId))
                snapshot.ActiveNodeIds.Add(state.CurrentStateId);
        }
    }

    private static bool ScopeContainsRuntime(
        IGraphRuntimeScope scope,
        object runtime,
        HashSet<IGraphRuntimeScope> visited)
    {
        if (scope == null || runtime == null || !visited.Add(scope))
            return false;

        if (ReferenceEquals(scope, runtime))
            return true;

        IEnumerable<IGraphRuntimeScope> children = scope.ChildScopes;
        if (children == null)
            return false;

        foreach (IGraphRuntimeScope child in children)
        {
            if (ScopeContainsRuntime(child, runtime, visited))
                return true;
        }

        return false;
    }

    private static bool ScopeContainsContext(
        IGraphRuntimeScope scope,
        GraphExecutionContext context,
        HashSet<IGraphRuntimeScope> visited)
    {
        if (scope == null || context == null || !visited.Add(scope))
            return false;

        if (ReferenceEquals(scope.Context, context))
            return true;

        IEnumerable<IGraphRuntimeScope> children = scope.ChildScopes;
        if (children == null)
            return false;

        foreach (IGraphRuntimeScope child in children)
        {
            if (ScopeContainsContext(child, context, visited))
                return true;
        }

        return false;
    }
}
