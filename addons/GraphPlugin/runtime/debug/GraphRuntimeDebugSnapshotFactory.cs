using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public static class GraphRuntimeDebugSnapshotFactory
{
    private static readonly string[] CommonStatusProperties =
    {
        "SkillKey",
        "ResourcePath",
        "IsRunning",
        "IsCompleted",
        "LastReturnLabel",
        "CooldownReadyTime",
        "CurrentStateName",
        "CurrentStatePath",
        "DisplayName",
        "SkillId"
    };

    public static GraphExecutionContextDebugSnapshot CreateContextSnapshot(GraphExecutionContext context)
    {
        var snapshot = new GraphExecutionContextDebugSnapshot();
        GraphAsset graph = context?.Graph;
        snapshot.GraphName = GetGraphName(graph);
        snapshot.GraphType = graph?.GraphType ?? graph?.GetType().Name ?? string.Empty;
        snapshot.GraphPath = graph?.ResourcePath ?? string.Empty;

        if (context?.Blackboard != null)
            snapshot.BlackboardEntries.AddRange(context.Blackboard.CreateDebugSnapshot());

        if (context?.UserData != null)
        {
            for (int i = 0; i < context.UserData.Count; i++)
            {
                object value = context.UserData[i];
                snapshot.UserData.Add(CreateUserDataEntry(value));
                if (value is FlowTimelineContext timelineContext)
                    snapshot.Timeline = CreateTimelineSnapshot(timelineContext);
            }
        }

        return snapshot;
    }

    public static FlowTimelineDebugSnapshot CreateTimelineSnapshot(FlowTimelineContext context)
    {
        if (context == null)
            return null;

        return new FlowTimelineDebugSnapshot
        {
            HasValue = true,
            NodeId = context.Node?.Id ?? string.Empty,
            Phase = context.Phase.ToString(),
            EventLabel = context.EventLabel ?? string.Empty,
            Time = context.Time,
            PreviousTime = context.PreviousTime,
            Delta = context.Delta,
            Duration = context.Duration,
            TrackName = context.TrackName ?? string.Empty,
            ClipName = context.ClipName ?? string.Empty,
            ClipTime = context.ClipTime,
            ClipDuration = context.ClipDuration,
            NormalizedTime = context.NormalizedTime,
            ClipNormalizedTime = context.ClipNormalizedTime
        };
    }

    public static GraphUserDataDebugEntry CreateUserDataEntry(object value)
    {
        return new GraphUserDataDebugEntry
        {
            TypeName = GetTypeName(value),
            Summary = CreateSafeSummary(value)
        };
    }

    public static string GetGraphName(GraphAsset graph)
    {
        if (graph == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(graph.ResourcePath))
            return graph.ResourcePath.GetFile();

        if (!string.IsNullOrWhiteSpace(graph.graphName))
            return graph.graphName;

        return graph.GetType().Name;
    }

    public static string GetObjectPath(Node node)
    {
        if (node == null)
            return string.Empty;

        try
        {
            return node.IsInsideTree() ? node.GetPath().ToString() : node.Name.ToString();
        }
        catch
        {
            return node.Name.ToString();
        }
    }

    public static string Truncate(string text, int maxLength = 140)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? string.Empty;

        return text.Substring(0, Math.Max(0, maxLength - 3)) + "...";
    }

    private static string GetTypeName(object value)
    {
        return value == null ? "null" : value.GetType().Name;
    }

    private static string CreateSafeSummary(object value)
    {
        if (value == null)
            return "null";

        if (value is FlowTimelineContext timelineContext)
            return CreateTimelineSummary(timelineContext);

        if (value is Node node)
            return $"Node name={node.Name}, path={GetObjectPath(node)}";

        if (value is Resource resource)
        {
            string path = string.IsNullOrWhiteSpace(resource.ResourcePath)
                ? "<memory>"
                : resource.ResourcePath;
            return $"Resource path={path}";
        }

        if (IsSimpleValue(value))
            return value.ToString();

        List<string> parts = CreateWhitelistedPropertySummary(value);
        if (parts.Count > 0)
            return string.Join(", ", parts);

        return value.GetType().FullName ?? value.GetType().Name;
    }

    private static string CreateTimelineSummary(FlowTimelineContext context)
    {
        string clip = string.IsNullOrWhiteSpace(context.ClipName) ? "-" : context.ClipName;
        string track = string.IsNullOrWhiteSpace(context.TrackName) ? "-" : context.TrackName;
        string label = string.IsNullOrWhiteSpace(context.EventLabel) ? "-" : context.EventLabel;
        return $"phase={context.Phase}, time={context.Time:0.###}/{context.Duration:0.###}, track={track}, clip={clip}, event={label}, normalized={context.ClipNormalizedTime:0.###}";
    }

    private static bool IsSimpleValue(object value)
    {
        Type type = value.GetType();
        return type.IsPrimitive ||
               type.IsEnum ||
               value is string ||
               value is decimal ||
               value is Vector2 ||
               value is Vector3 ||
               value is Color;
    }

    private static List<string> CreateWhitelistedPropertySummary(object value)
    {
        var parts = new List<string>();
        Type type = value.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

        for (int i = 0; i < CommonStatusProperties.Length; i++)
        {
            PropertyInfo property = type.GetProperty(CommonStatusProperties[i], flags);
            if (property == null || property.GetIndexParameters().Length > 0)
                continue;

            object propertyValue;
            try
            {
                propertyValue = property.GetValue(value);
            }
            catch
            {
                continue;
            }

            if (propertyValue == null)
                continue;

            string preview = propertyValue switch
            {
                Resource resource => string.IsNullOrWhiteSpace(resource.ResourcePath) ? resource.GetType().Name : resource.ResourcePath,
                Node node => GetObjectPath(node),
                double d => d.ToString("0.###"),
                float f => f.ToString("0.###"),
                _ => propertyValue.ToString()
            };

            parts.Add($"{property.Name}={Truncate(preview, 64)}");
        }

        return parts;
    }
}
