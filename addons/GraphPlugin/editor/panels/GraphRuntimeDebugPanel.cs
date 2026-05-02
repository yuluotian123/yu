#if TOOLS
using System;
using System.Collections.Generic;
using Godot;

public sealed class GraphRuntimeDebugPanel
{
    private readonly PanelContainer _root;
    private readonly VBoxContainer _content;

    public GraphRuntimeDebugPanel()
    {
        _root = new PanelContainer
        {
            Visible = false,
            CustomMinimumSize = new Vector2(0, 230),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _root.AddChild(scroll);

        _content = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _content.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_content);
    }

    public event Action<GraphAsset> OpenGraphRequested;

    public Control Root => _root;

    public void SetVisible(bool visible)
    {
        _root.Visible = visible;
        if (!visible)
            ClearChildren(_content);
    }

    public void Refresh(
        string selectedOwnerPath,
        bool hasReceivedSnapshots,
        IReadOnlyList<GraphRuntimeDebugSnapshot> snapshots,
        GraphRuntimeDebugSnapshot activeSnapshot,
        GraphAsset currentGraph)
    {
        if (!_root.Visible)
            return;

        ClearChildren(_content);
        AddSection("Target");
        AddLine($"Remote selection: {DisplayOrDash(selectedOwnerPath)}");
        AddLine($"Received snapshots: {GraphRuntimeDebugRemoteStore.SnapshotCount}");
        AddLine($"Matched runtimes: {snapshots?.Count ?? 0}");

        if (activeSnapshot == null)
        {
            AddLine(hasReceivedSnapshots
                ? "No runtime snapshot matches the selected remote node."
                : "No remote runtime snapshot has been received.");
            return;
        }

        AddLine($"Owner: {activeSnapshot.OwnerName} {activeSnapshot.OwnerPath}");
        GraphRuntimeDebugScopeSnapshot scope = GraphRuntimeDebugUtil.FindScopeForGraph(activeSnapshot.Scopes, currentGraph);
        if (scope == null && activeSnapshot.Scopes.Count > 0)
            scope = activeSnapshot.Scopes[0];

        AddRuntimeSection(activeSnapshot, scope, currentGraph);
        AddContextSection(scope?.Context ?? activeSnapshot.LastContext);
        AddTimelineSection(activeSnapshot.LastTimeline ?? scope?.Context?.Timeline);
        AddEventsSection(activeSnapshot.Events);
    }

    private void AddRuntimeSection(
        GraphRuntimeDebugSnapshot snapshot,
        GraphRuntimeDebugScopeSnapshot scope,
        GraphAsset currentGraph)
    {
        AddSection("Runtime");
        AddLine($"Type: {snapshot.RuntimeType}");
        if (!string.IsNullOrWhiteSpace(snapshot.RuntimeScope))
            AddLine($"Scope: {snapshot.RuntimeScope}");

        if (snapshot.Metadata.Count > 0)
        {
            for (int i = 0; i < snapshot.Metadata.Count; i++)
                AddLine(snapshot.Metadata[i]);
        }

        if (scope == null)
        {
            AddLine("No graph scope snapshot.");
            return;
        }

        AddLine($"Graph: {FormatGraph(scope.GraphName, scope.GraphType, scope.GraphPath)}");
        AddLine($"Running: {scope.IsRunning}");

        if (!string.IsNullOrWhiteSpace(scope.CurrentStatePath))
            AddLine($"Current State: {scope.CurrentStatePath} ({scope.CurrentStateTime:0.###}s)");

        if (scope.ActiveNodeIds.Count > 0)
            AddLine($"Active Nodes: {string.Join(", ", scope.ActiveNodeIds)}");

        if (currentGraph != null && !GraphRuntimeDebugUtil.GraphMatches(scope.Graph, currentGraph))
            AddOpenGraphRow(scope.Graph);
    }

    private void AddContextSection(GraphExecutionContextDebugSnapshot context)
    {
        AddSection("Context");
        if (context == null)
        {
            AddLine("No context snapshot.");
            return;
        }

        AddLine($"Graph: {FormatGraph(context.GraphName, context.GraphType, context.GraphPath)}");

        AddSubsection("Blackboard");
        if (context.BlackboardEntries.Count == 0)
        {
            AddLine("(empty)");
        }
        else
        {
            for (int i = 0; i < context.BlackboardEntries.Count; i++)
            {
                GraphBlackboardDebugEntry entry = context.BlackboardEntries[i];
                AddLine($"{entry.Scope} {entry.Key} = {entry.ValuePreview} ({entry.ValueType})");
            }
        }

        AddSubsection("UserData");
        if (context.UserData.Count == 0)
        {
            AddLine("(empty)");
            return;
        }

        for (int i = 0; i < context.UserData.Count; i++)
        {
            GraphUserDataDebugEntry entry = context.UserData[i];
            AddLine($"{entry.TypeName}: {entry.Summary}");
        }
    }

    private void AddTimelineSection(FlowTimelineDebugSnapshot timeline)
    {
        AddSection("Timeline");
        if (timeline == null || !timeline.HasValue)
        {
            AddLine("No recent timeline context.");
            return;
        }

        AddLine($"Phase: {timeline.Phase}");
        AddLine($"Time: {timeline.Time:0.###}/{timeline.Duration:0.###}  dt={timeline.Delta:0.###}");
        AddLine($"Track: {DisplayOrDash(timeline.TrackName)}");
        AddLine($"Clip: {DisplayOrDash(timeline.ClipName)}  {timeline.ClipTime:0.###}/{timeline.ClipDuration:0.###}");
        AddLine($"Normalized: graph={timeline.NormalizedTime:0.###}, clip={timeline.ClipNormalizedTime:0.###}");
        if (!string.IsNullOrWhiteSpace(timeline.EventLabel))
            AddLine($"Event: {timeline.EventLabel}");
    }

    private void AddEventsSection(IReadOnlyList<GraphRuntimeDebugEventSnapshot> events)
    {
        AddSection("Events");
        if (events == null || events.Count == 0)
        {
            AddLine("(empty)");
            return;
        }

        for (int i = events.Count - 1; i >= 0; i--)
        {
            GraphRuntimeDebugEventSnapshot e = events[i];
            string node = string.IsNullOrWhiteSpace(e.NodeName) ? string.Empty : $" [{e.NodeName}]";
            AddLine($"{e.TimeSeconds:0.000} {e.Kind}{node}: {e.Message}");
        }
    }

    private void AddOpenGraphRow(GraphAsset graph)
    {
        if (graph == null)
            return;

        var row = new HBoxContainer();
        row.AddChild(new Label
        {
            Text = "Opened graph does not match this runtime.",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        });
        var button = new Button { Text = "Open Runtime Graph" };
        button.Pressed += () => OpenGraphRequested?.Invoke(graph);
        row.AddChild(button);
        _content.AddChild(row);
    }

    private void AddSection(string text)
    {
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 24)
        };
        label.AddThemeColorOverride("font_color", new Color(0.95f, 0.84f, 0.36f));
        _content.AddChild(label);
    }

    private void AddSubsection(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", new Color(0.72f, 0.78f, 0.9f));
        _content.AddChild(label);
    }

    private void AddLine(string text)
    {
        var label = new Label
        {
            Text = text ?? string.Empty,
            ClipText = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _content.AddChild(label);
    }

    private static string FormatGraph(string name, string type, string path)
    {
        string title = string.IsNullOrWhiteSpace(name) ? "<memory>" : name;
        string graphType = string.IsNullOrWhiteSpace(type) ? "Graph" : type;
        string graphPath = string.IsNullOrWhiteSpace(path) ? "<memory>" : path;
        return $"{title} ({graphType}) {graphPath}";
    }

    private static string DisplayOrDash(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static void ClearChildren(Control control)
    {
        foreach (Node child in control.GetChildren())
        {
            control.RemoveChild(child);
            child.QueueFree();
        }
    }
}
#endif
