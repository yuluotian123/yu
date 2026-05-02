using System;
using System.Collections.Generic;
using Godot;

public sealed class GraphRuntimeDebugHandle : IDisposable
{
    private const int MaxEvents = 20;
    private readonly Func<IEnumerable<string>> _metadataProvider;
    private readonly List<GraphRuntimeDebugEventSnapshot> _events = new();
    private bool _disposed;
    private double _lastContextCaptureTime;

    internal GraphRuntimeDebugHandle(
        int id,
        Node owner,
        object runtime,
        GraphAsset graph,
        string runtimeScope,
        Func<IEnumerable<string>> metadataProvider)
    {
        Id = id;
        Owner = owner;
        Runtime = runtime;
        Graph = graph;
        RuntimeScope = runtimeScope ?? string.Empty;
        _metadataProvider = metadataProvider;
    }

    public int Id { get; }
    public Node Owner { get; }
    public object Runtime { get; }
    public GraphAsset Graph { get; }
    public string RuntimeScope { get; }

    internal bool IsDisposed => _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        GraphRuntimeDebugRegistry.Unregister(this);
    }

    internal void AddEvent(GraphRuntimeDebugEventSnapshot eventSnapshot)
    {
        if (eventSnapshot == null)
            return;

        _events.Add(eventSnapshot);
        while (_events.Count > MaxEvents)
            _events.RemoveAt(0);
    }

    internal void CaptureContext(GraphExecutionContext context, bool force)
    {
        double now = NowSeconds();
        if (!force && now - _lastContextCaptureTime < 0.1d)
            return;

        LastContext = GraphRuntimeDebugSnapshotFactory.CreateContextSnapshot(context);
        _lastContextCaptureTime = now;
    }

    internal void CaptureTimeline(FlowTimelineContext timelineContext)
    {
        LastTimeline = GraphRuntimeDebugSnapshotFactory.CreateTimelineSnapshot(timelineContext);
    }

    internal GraphRuntimeDebugSnapshot CreateSnapshot()
    {
        var snapshot = new GraphRuntimeDebugSnapshot
        {
            HandleId = Id,
            Owner = Owner,
            OwnerName = Owner?.Name.ToString() ?? string.Empty,
            OwnerPath = GraphRuntimeDebugSnapshotFactory.GetObjectPath(Owner),
            Runtime = Runtime,
            RuntimeType = Runtime?.GetType().Name ?? string.Empty,
            RuntimeScope = RuntimeScope,
            Graph = Graph,
            GraphName = GraphRuntimeDebugSnapshotFactory.GetGraphName(Graph),
            GraphType = Graph?.GraphType ?? Graph?.GetType().Name ?? string.Empty,
            GraphPath = Graph?.ResourcePath ?? string.Empty,
            IsRunning = GraphRuntimeDebugUtil.IsRunningRuntime(Runtime),
            LastContext = LastContext,
            LastTimeline = LastTimeline
        };

        snapshot.Scopes.AddRange(GraphRuntimeDebugUtil.CreateScopeSnapshots(Runtime));
        AddMetadata(snapshot.Metadata);
        snapshot.Events.AddRange(_events);
        return snapshot;
    }

    internal GraphExecutionContextDebugSnapshot LastContext { get; private set; }
    internal FlowTimelineDebugSnapshot LastTimeline { get; private set; }

    private void AddMetadata(List<string> output)
    {
        if (_metadataProvider == null || output == null)
            return;

        IEnumerable<string> lines;
        try
        {
            lines = _metadataProvider();
        }
        catch (Exception ex)
        {
            output.Add($"metadata error: {ex.Message}");
            return;
        }

        if (lines == null)
            return;

        foreach (string line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
                output.Add(line);
        }
    }

    internal static double NowSeconds()
    {
        return Time.GetTicksMsec() * 0.001d;
    }
}

public static class GraphRuntimeDebugRegistry
{
    private static readonly List<GraphRuntimeDebugHandle> Handles = new();
    private static readonly Dictionary<GraphExecutionContext, List<GraphRuntimeDebugHandle>> HandlesByContext = new();
    private static int _nextId = 1;

    public static GraphRuntimeDebugHandle Register(
        Node owner,
        object runtime,
        GraphAsset graph = null,
        string runtimeScope = "",
        Func<IEnumerable<string>> metadataProvider = null)
    {
        if (runtime == null)
            return null;

        var handle = new GraphRuntimeDebugHandle(
            _nextId++,
            owner,
            runtime,
            graph ?? ResolveGraph(runtime),
            runtimeScope,
            metadataProvider);

        Handles.Add(handle);
        RegisterContext(handle, runtime as IGraphRuntimeScope);
        RecordEvent(runtime, "Register", "runtime registered");
        GraphRuntimeDebugBridge.NotifyChanged(true);
        return handle;
    }

    public static void Unregister(GraphRuntimeDebugHandle handle)
    {
        if (handle == null)
            return;

        Handles.Remove(handle);
        UnregisterContexts(handle);
        GraphRuntimeDebugBridge.NotifyChanged(true);
    }

    public static void RecordEvent(
        object runtime,
        string kind,
        string message,
        GraphAsset graph = null,
        GraphNodeData node = null)
    {
        if (runtime == null)
            return;

        List<GraphRuntimeDebugHandle> handles = FindHandlesForRuntime(runtime);
        if (handles.Count == 0)
            return;

        var eventSnapshot = new GraphRuntimeDebugEventSnapshot
        {
            TimeSeconds = GraphRuntimeDebugHandle.NowSeconds(),
            Kind = kind ?? string.Empty,
            Message = message ?? string.Empty,
            GraphPath = graph?.ResourcePath ?? ResolveGraph(runtime)?.ResourcePath ?? string.Empty,
            NodeId = node?.Id ?? string.Empty,
            NodeName = node?.GetDisplayName() ?? string.Empty
        };

        for (int i = 0; i < handles.Count; i++)
            handles[i].AddEvent(eventSnapshot);

        GraphRuntimeDebugBridge.NotifyChanged();
    }

    public static void CaptureContext(object runtime, GraphExecutionContext context, bool force = false)
    {
        if (runtime == null || context == null)
            return;

        List<GraphRuntimeDebugHandle> handles = FindHandlesForRuntime(runtime);
        for (int i = 0; i < handles.Count; i++)
            handles[i].CaptureContext(context, force);

        GraphRuntimeDebugBridge.NotifyChanged(force);
    }

    public static void CaptureContext(GraphExecutionContext context, bool force = false)
    {
        if (context == null)
            return;

        List<GraphRuntimeDebugHandle> handles = FindHandlesForContext(context);
        for (int i = 0; i < handles.Count; i++)
            handles[i].CaptureContext(context, force);

        GraphRuntimeDebugBridge.NotifyChanged(force);
    }

    public static void CaptureTimelineContext(
        GraphExecutionContext context,
        FlowTimelineContext timelineContext,
        bool captureContextSnapshot = false)
    {
        if (context == null || timelineContext == null)
            return;

        List<GraphRuntimeDebugHandle> handles = FindHandlesForContext(context);
        for (int i = 0; i < handles.Count; i++)
        {
            handles[i].CaptureTimeline(timelineContext);
            if (captureContextSnapshot)
                handles[i].CaptureContext(context, true);
        }

        GraphRuntimeDebugBridge.NotifyChanged(captureContextSnapshot);
    }

    public static List<GraphRuntimeDebugSnapshot> CreateSnapshots()
    {
        var result = new List<GraphRuntimeDebugSnapshot>();
        CleanupInvalidHandles();

        for (int i = 0; i < Handles.Count; i++)
        {
            GraphRuntimeDebugHandle handle = Handles[i];
            if (handle == null || handle.IsDisposed)
                continue;

            result.Add(handle.CreateSnapshot());
        }

        return result;
    }

    private static void RegisterContext(GraphRuntimeDebugHandle handle, IGraphRuntimeScope scope)
    {
        if (handle == null || scope?.Context == null)
            return;

        if (!HandlesByContext.TryGetValue(scope.Context, out List<GraphRuntimeDebugHandle> handles))
        {
            handles = new List<GraphRuntimeDebugHandle>();
            HandlesByContext[scope.Context] = handles;
        }

        if (!handles.Contains(handle))
            handles.Add(handle);
    }

    private static void UnregisterContexts(GraphRuntimeDebugHandle handle)
    {
        if (handle == null)
            return;

        var emptyKeys = new List<GraphExecutionContext>();
        foreach (var pair in HandlesByContext)
        {
            pair.Value.Remove(handle);
            if (pair.Value.Count == 0)
                emptyKeys.Add(pair.Key);
        }

        for (int i = 0; i < emptyKeys.Count; i++)
            HandlesByContext.Remove(emptyKeys[i]);
    }

    private static List<GraphRuntimeDebugHandle> FindHandlesForRuntime(object runtime)
    {
        var result = new List<GraphRuntimeDebugHandle>();
        CleanupInvalidHandles();

        for (int i = 0; i < Handles.Count; i++)
        {
            GraphRuntimeDebugHandle handle = Handles[i];
            if (handle.IsDisposed)
                continue;

            if (ReferenceEquals(handle.Runtime, runtime))
            {
                result.Add(handle);
                continue;
            }

            if (handle.Runtime is IGraphRuntimeScope scope &&
                GraphRuntimeDebugUtil.ScopeContainsRuntime(scope, runtime))
            {
                result.Add(handle);
            }
        }

        return result;
    }

    private static List<GraphRuntimeDebugHandle> FindHandlesForContext(GraphExecutionContext context)
    {
        var result = new List<GraphRuntimeDebugHandle>();
        CleanupInvalidHandles();

        if (HandlesByContext.TryGetValue(context, out List<GraphRuntimeDebugHandle> mapped))
            result.AddRange(mapped);

        for (int i = 0; i < Handles.Count; i++)
        {
            GraphRuntimeDebugHandle handle = Handles[i];
            if (handle.IsDisposed || result.Contains(handle))
                continue;

            if (handle.Runtime is IGraphRuntimeScope scope &&
                GraphRuntimeDebugUtil.ScopeContainsContext(scope, context))
            {
                result.Add(handle);
            }
        }

        return result;
    }

    private static GraphAsset ResolveGraph(object runtime)
    {
        if (runtime is IGraphRuntimeScope scope)
            return scope.Context?.Graph;

        return null;
    }

    private static void CleanupInvalidHandles()
    {
        for (int i = Handles.Count - 1; i >= 0; i--)
        {
            GraphRuntimeDebugHandle handle = Handles[i];
            if (handle == null ||
                handle.IsDisposed ||
                handle.Owner != null && !GodotObject.IsInstanceValid(handle.Owner))
            {
                Handles.RemoveAt(i);
                if (handle != null)
                    UnregisterContexts(handle);
            }
        }
    }
}
