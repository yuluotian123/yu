using System;
using System.Reflection;

[Flags]
public enum GraphTimelineActionKind { None = 0, Marker = 1, Clip = 2 }

/// <summary>Timeline placement is independent of functional category and host binding.</summary>
public static class GraphTimelineActionRules
{
    public static GraphTimelineActionKind Kind(Type type)
    {
        if (type == null || type.IsAbstract || !typeof(GraphActionBase).IsAssignableFrom(type)) return GraphTimelineActionKind.None;
        var metadata = type.GetCustomAttribute<GraphCallableAttribute>();
        if (metadata != null)
            return (metadata.Usage & GraphCallableUsage.Timeline) != 0 ? metadata.TimelineKind : GraphTimelineActionKind.None;
        return typeof(GraphInstantAction).IsAssignableFrom(type) ? GraphTimelineActionKind.Marker : GraphTimelineActionKind.None;
    }

    public static bool Supports(Type type, GraphTimelineActionKind kind) => kind != GraphTimelineActionKind.None && (Kind(type) & kind) == kind;
}
