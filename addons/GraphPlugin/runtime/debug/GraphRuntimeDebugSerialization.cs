using System.Collections.Generic;
using Godot;
using Godot.Collections;

public static class GraphRuntimeDebugSerialization
{
    public static Dictionary SerializeSnapshot(GraphRuntimeDebugSnapshot snapshot)
    {
        var dict = new Dictionary();
        if (snapshot == null)
            return dict;

        dict["handle_id"] = snapshot.HandleId;
        dict["owner_name"] = snapshot.OwnerName;
        dict["owner_path"] = snapshot.OwnerPath;
        dict["runtime_type"] = snapshot.RuntimeType;
        dict["runtime_scope"] = snapshot.RuntimeScope;
        dict["graph_name"] = snapshot.GraphName;
        dict["graph_type"] = snapshot.GraphType;
        dict["graph_path"] = snapshot.GraphPath;
        dict["is_running"] = snapshot.IsRunning;
        dict["metadata"] = SerializeStrings(snapshot.Metadata);
        dict["scopes"] = SerializeScopes(snapshot.Scopes);
        dict["events"] = SerializeEvents(snapshot.Events);
        dict["last_context"] = SerializeContext(snapshot.LastContext);
        dict["last_timeline"] = SerializeTimeline(snapshot.LastTimeline);
        return dict;
    }

    public static GraphRuntimeDebugSnapshot DeserializeSnapshot(Dictionary dict)
    {
        if (dict == null)
            return null;

        string graphPath = GetString(dict, "graph_path");
        var snapshot = new GraphRuntimeDebugSnapshot
        {
            HandleId = GetInt(dict, "handle_id"),
            OwnerName = GetString(dict, "owner_name"),
            OwnerPath = GetString(dict, "owner_path"),
            RuntimeType = GetString(dict, "runtime_type"),
            RuntimeScope = GetString(dict, "runtime_scope"),
            GraphName = GetString(dict, "graph_name"),
            GraphType = GetString(dict, "graph_type"),
            GraphPath = graphPath,
            Graph = LoadGraph(graphPath),
            IsRunning = GetBool(dict, "is_running"),
            LastContext = DeserializeContext(GetDictionary(dict, "last_context")),
            LastTimeline = DeserializeTimeline(GetDictionary(dict, "last_timeline"))
        };

        DeserializeStrings(GetArray(dict, "metadata"), snapshot.Metadata);
        DeserializeScopes(GetArray(dict, "scopes"), snapshot.Scopes);
        DeserializeEvents(GetArray(dict, "events"), snapshot.Events);
        return snapshot;
    }

    private static Array SerializeScopes(IReadOnlyList<GraphRuntimeDebugScopeSnapshot> scopes)
    {
        var array = new Array();
        if (scopes == null)
            return array;

        for (int i = 0; i < scopes.Count; i++)
        {
            GraphRuntimeDebugScopeSnapshot scope = scopes[i];
            if (scope == null)
                continue;

            var dict = new Dictionary
            {
                ["depth"] = scope.Depth,
                ["runtime_type"] = scope.RuntimeType,
                ["graph_name"] = scope.GraphName,
                ["graph_type"] = scope.GraphType,
                ["graph_path"] = scope.GraphPath,
                ["is_running"] = scope.IsRunning,
                ["is_completed"] = scope.IsCompleted,
                ["active_node_ids"] = SerializeStrings(scope.ActiveNodeIds),
                ["current_state_id"] = scope.CurrentStateId,
                ["current_state_name"] = scope.CurrentStateName,
                ["current_state_path"] = scope.CurrentStatePath,
                ["current_state_time"] = scope.CurrentStateTime,
                ["context"] = SerializeContext(scope.Context)
            };
            array.Add(dict);
        }

        return array;
    }

    private static void DeserializeScopes(Array array, List<GraphRuntimeDebugScopeSnapshot> output)
    {
        if (array == null || output == null)
            return;

        for (int i = 0; i < array.Count; i++)
        {
            Dictionary dict = array[i].AsGodotDictionary();
            if (dict == null || dict.Count == 0)
                continue;

            string graphPath = GetString(dict, "graph_path");
            var scope = new GraphRuntimeDebugScopeSnapshot
            {
                Depth = GetInt(dict, "depth"),
                RuntimeType = GetString(dict, "runtime_type"),
                GraphName = GetString(dict, "graph_name"),
                GraphType = GetString(dict, "graph_type"),
                GraphPath = graphPath,
                Graph = LoadGraph(graphPath),
                IsRunning = GetBool(dict, "is_running"),
                IsCompleted = GetBool(dict, "is_completed"),
                CurrentStateId = GetString(dict, "current_state_id"),
                CurrentStateName = GetString(dict, "current_state_name"),
                CurrentStatePath = GetString(dict, "current_state_path"),
                CurrentStateTime = GetDouble(dict, "current_state_time"),
                Context = DeserializeContext(GetDictionary(dict, "context"))
            };
            DeserializeStrings(GetArray(dict, "active_node_ids"), scope.ActiveNodeIds);
            output.Add(scope);
        }
    }

    private static Dictionary SerializeContext(GraphExecutionContextDebugSnapshot context)
    {
        var dict = new Dictionary();
        if (context == null)
            return dict;

        dict["graph_name"] = context.GraphName;
        dict["graph_type"] = context.GraphType;
        dict["graph_path"] = context.GraphPath;
        dict["blackboard"] = SerializeBlackboard(context.BlackboardEntries);
        dict["user_data"] = SerializeUserData(context.UserData);
        dict["timeline"] = SerializeTimeline(context.Timeline);
        return dict;
    }

    private static GraphExecutionContextDebugSnapshot DeserializeContext(Dictionary dict)
    {
        if (dict == null || dict.Count == 0)
            return null;

        var context = new GraphExecutionContextDebugSnapshot
        {
            GraphName = GetString(dict, "graph_name"),
            GraphType = GetString(dict, "graph_type"),
            GraphPath = GetString(dict, "graph_path"),
            Timeline = DeserializeTimeline(GetDictionary(dict, "timeline"))
        };
        DeserializeBlackboard(GetArray(dict, "blackboard"), context.BlackboardEntries);
        DeserializeUserData(GetArray(dict, "user_data"), context.UserData);
        return context;
    }

    private static Dictionary SerializeTimeline(FlowTimelineDebugSnapshot timeline)
    {
        var dict = new Dictionary();
        if (timeline == null)
            return dict;

        dict["has_value"] = timeline.HasValue;
        dict["node_id"] = timeline.NodeId;
        dict["phase"] = timeline.Phase;
        dict["event_label"] = timeline.EventLabel;
        dict["time"] = timeline.Time;
        dict["previous_time"] = timeline.PreviousTime;
        dict["delta"] = timeline.Delta;
        dict["duration"] = timeline.Duration;
        dict["track_name"] = timeline.TrackName;
        dict["clip_name"] = timeline.ClipName;
        dict["clip_time"] = timeline.ClipTime;
        dict["clip_duration"] = timeline.ClipDuration;
        dict["normalized_time"] = timeline.NormalizedTime;
        dict["clip_normalized_time"] = timeline.ClipNormalizedTime;
        return dict;
    }

    private static FlowTimelineDebugSnapshot DeserializeTimeline(Dictionary dict)
    {
        if (dict == null || dict.Count == 0 || !GetBool(dict, "has_value"))
            return null;

        return new FlowTimelineDebugSnapshot
        {
            HasValue = true,
            NodeId = GetString(dict, "node_id"),
            Phase = GetString(dict, "phase"),
            EventLabel = GetString(dict, "event_label"),
            Time = GetFloat(dict, "time"),
            PreviousTime = GetFloat(dict, "previous_time"),
            Delta = GetFloat(dict, "delta"),
            Duration = GetFloat(dict, "duration"),
            TrackName = GetString(dict, "track_name"),
            ClipName = GetString(dict, "clip_name"),
            ClipTime = GetFloat(dict, "clip_time"),
            ClipDuration = GetFloat(dict, "clip_duration"),
            NormalizedTime = GetFloat(dict, "normalized_time"),
            ClipNormalizedTime = GetFloat(dict, "clip_normalized_time")
        };
    }

    private static Array SerializeBlackboard(IReadOnlyList<GraphBlackboardDebugEntry> entries)
    {
        var array = new Array();
        if (entries == null)
            return array;

        for (int i = 0; i < entries.Count; i++)
        {
            GraphBlackboardDebugEntry entry = entries[i];
            if (entry == null)
                continue;

            array.Add(new Dictionary
            {
                ["scope"] = entry.Scope,
                ["depth"] = entry.Depth,
                ["key"] = entry.Key,
                ["value_type"] = entry.ValueType,
                ["value_preview"] = entry.ValuePreview,
                ["is_global"] = entry.IsGlobal
            });
        }

        return array;
    }

    private static void DeserializeBlackboard(Array array, List<GraphBlackboardDebugEntry> output)
    {
        if (array == null || output == null)
            return;

        for (int i = 0; i < array.Count; i++)
        {
            Dictionary dict = array[i].AsGodotDictionary();
            if (dict == null || dict.Count == 0)
                continue;

            output.Add(new GraphBlackboardDebugEntry
            {
                Scope = GetString(dict, "scope"),
                Depth = GetInt(dict, "depth"),
                Key = GetString(dict, "key"),
                ValueType = GetString(dict, "value_type"),
                ValuePreview = GetString(dict, "value_preview"),
                IsGlobal = GetBool(dict, "is_global")
            });
        }
    }

    private static Array SerializeUserData(IReadOnlyList<GraphUserDataDebugEntry> entries)
    {
        var array = new Array();
        if (entries == null)
            return array;

        for (int i = 0; i < entries.Count; i++)
        {
            GraphUserDataDebugEntry entry = entries[i];
            if (entry == null)
                continue;

            array.Add(new Dictionary
            {
                ["type_name"] = entry.TypeName,
                ["summary"] = entry.Summary
            });
        }

        return array;
    }

    private static void DeserializeUserData(Array array, List<GraphUserDataDebugEntry> output)
    {
        if (array == null || output == null)
            return;

        for (int i = 0; i < array.Count; i++)
        {
            Dictionary dict = array[i].AsGodotDictionary();
            if (dict == null || dict.Count == 0)
                continue;

            output.Add(new GraphUserDataDebugEntry
            {
                TypeName = GetString(dict, "type_name"),
                Summary = GetString(dict, "summary")
            });
        }
    }

    private static Array SerializeEvents(IReadOnlyList<GraphRuntimeDebugEventSnapshot> events)
    {
        var array = new Array();
        if (events == null)
            return array;

        for (int i = 0; i < events.Count; i++)
        {
            GraphRuntimeDebugEventSnapshot e = events[i];
            if (e == null)
                continue;

            array.Add(new Dictionary
            {
                ["time_seconds"] = e.TimeSeconds,
                ["kind"] = e.Kind,
                ["message"] = e.Message,
                ["graph_path"] = e.GraphPath,
                ["node_id"] = e.NodeId,
                ["node_name"] = e.NodeName
            });
        }

        return array;
    }

    private static void DeserializeEvents(Array array, List<GraphRuntimeDebugEventSnapshot> output)
    {
        if (array == null || output == null)
            return;

        for (int i = 0; i < array.Count; i++)
        {
            Dictionary dict = array[i].AsGodotDictionary();
            if (dict == null || dict.Count == 0)
                continue;

            output.Add(new GraphRuntimeDebugEventSnapshot
            {
                TimeSeconds = GetDouble(dict, "time_seconds"),
                Kind = GetString(dict, "kind"),
                Message = GetString(dict, "message"),
                GraphPath = GetString(dict, "graph_path"),
                NodeId = GetString(dict, "node_id"),
                NodeName = GetString(dict, "node_name")
            });
        }
    }

    private static Array SerializeStrings(IReadOnlyList<string> values)
    {
        var array = new Array();
        if (values == null)
            return array;

        for (int i = 0; i < values.Count; i++)
            array.Add(values[i] ?? string.Empty);

        return array;
    }

    private static void DeserializeStrings(Array array, List<string> output)
    {
        if (array == null || output == null)
            return;

        for (int i = 0; i < array.Count; i++)
            output.Add(array[i].AsString());
    }

    private static GraphAsset LoadGraph(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
            return null;

        return ResourceLoader.Load<GraphAsset>(path);
    }

    private static Dictionary GetDictionary(Dictionary dict, string key)
    {
        return dict != null && dict.ContainsKey(key) ? dict[key].AsGodotDictionary() : null;
    }

    private static Array GetArray(Dictionary dict, string key)
    {
        return dict != null && dict.ContainsKey(key) ? dict[key].AsGodotArray() : null;
    }

    private static string GetString(Dictionary dict, string key)
    {
        return dict != null && dict.ContainsKey(key) ? dict[key].AsString() : string.Empty;
    }

    private static bool GetBool(Dictionary dict, string key)
    {
        if (dict == null || !dict.ContainsKey(key))
            return false;

        return dict[key].AsBool();
    }

    private static int GetInt(Dictionary dict, string key)
    {
        if (dict == null || !dict.ContainsKey(key))
            return 0;

        return (int)dict[key].AsInt64();
    }

    private static float GetFloat(Dictionary dict, string key)
    {
        if (dict == null || !dict.ContainsKey(key))
            return 0f;

        return (float)dict[key].AsDouble();
    }

    private static double GetDouble(Dictionary dict, string key)
    {
        if (dict == null || !dict.ContainsKey(key))
            return 0d;

        return dict[key].AsDouble();
    }
}
