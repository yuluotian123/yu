using System;

[Flags]
public enum GraphCallableUsage { Flow = 1, BehaviorTree = 2, Timeline = 4, State = 8, Hfsm = 16, Mission = 32, All = Flow | BehaviorTree | Timeline }

// Menu metadata is independent of serialization names and component binding modes.
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GraphCallableAttribute : Attribute
{
    public GraphCallableAttribute(string name, string category, GraphCallableUsage usage = GraphCallableUsage.All)
    { Name = name; Category = category; Usage = usage; }
    public string Name { get; }
    public string Category { get; }
    public GraphCallableUsage Usage { get; }
    // Timeline actions are one-shot Markers by default. Clips must explicitly opt in
    // and handle Start/Update/Complete/Cancel via FlowTimelineContext.
    public GraphTimelineActionKind TimelineKind { get; set; } = GraphTimelineActionKind.Marker;
    public string Keywords { get; set; } = "";
    public string ChineseName { get; set; } = "";
}
