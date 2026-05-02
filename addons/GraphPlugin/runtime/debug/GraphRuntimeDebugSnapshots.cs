using System.Collections.Generic;
using Godot;

public sealed class GraphBlackboardDebugEntry
{
    public string Scope { get; set; } = string.Empty;
    public int Depth { get; set; }
    public string Key { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    public string ValuePreview { get; set; } = string.Empty;
    public bool IsGlobal { get; set; }
}

public sealed class GraphUserDataDebugEntry
{
    public string TypeName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public sealed class FlowTimelineDebugSnapshot
{
    public bool HasValue { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string EventLabel { get; set; } = string.Empty;
    public float Time { get; set; }
    public float PreviousTime { get; set; }
    public float Delta { get; set; }
    public float Duration { get; set; }
    public string TrackName { get; set; } = string.Empty;
    public string ClipName { get; set; } = string.Empty;
    public float ClipTime { get; set; }
    public float ClipDuration { get; set; }
    public float NormalizedTime { get; set; }
    public float ClipNormalizedTime { get; set; }
}

public sealed class GraphExecutionContextDebugSnapshot
{
    public string GraphName { get; set; } = string.Empty;
    public string GraphType { get; set; } = string.Empty;
    public string GraphPath { get; set; } = string.Empty;
    public List<GraphBlackboardDebugEntry> BlackboardEntries { get; } = new();
    public List<GraphUserDataDebugEntry> UserData { get; } = new();
    public FlowTimelineDebugSnapshot Timeline { get; set; }
}

public sealed class GraphRuntimeDebugEventSnapshot
{
    public double TimeSeconds { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string GraphPath { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
}

public sealed class GraphRuntimeDebugScopeSnapshot
{
    public int Depth { get; set; }
    public string RuntimeType { get; set; } = string.Empty;
    public GraphAsset Graph { get; set; }
    public string GraphName { get; set; } = string.Empty;
    public string GraphType { get; set; } = string.Empty;
    public string GraphPath { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public bool IsCompleted { get; set; }
    public List<string> ActiveNodeIds { get; } = new();
    public string CurrentStateId { get; set; } = string.Empty;
    public string CurrentStateName { get; set; } = string.Empty;
    public string CurrentStatePath { get; set; } = string.Empty;
    public double CurrentStateTime { get; set; }
    public GraphExecutionContextDebugSnapshot Context { get; set; }
}

public sealed class GraphRuntimeDebugSnapshot
{
    public int HandleId { get; set; }
    public Node Owner { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerPath { get; set; } = string.Empty;
    public object Runtime { get; set; }
    public string RuntimeType { get; set; } = string.Empty;
    public string RuntimeScope { get; set; } = string.Empty;
    public GraphAsset Graph { get; set; }
    public string GraphName { get; set; } = string.Empty;
    public string GraphType { get; set; } = string.Empty;
    public string GraphPath { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public List<string> Metadata { get; } = new();
    public List<GraphRuntimeDebugScopeSnapshot> Scopes { get; } = new();
    public List<GraphRuntimeDebugEventSnapshot> Events { get; } = new();
    public GraphExecutionContextDebugSnapshot LastContext { get; set; }
    public FlowTimelineDebugSnapshot LastTimeline { get; set; }
}
