using System.Collections.Generic;
using Godot;

public enum FlowTimelinePhase
{
    Start,
    Update,
    Event,
    Complete,
    Cancel
}

public sealed class FlowTimelineContext
{
    public FlowTimelineNodeData Node { get; set; }
    public FlowTimelinePhase Phase { get; set; }
    public string EventLabel { get; set; } = string.Empty;
    public float Time { get; set; }
    public float PreviousTime { get; set; }
    public float Delta { get; set; }
    public float Duration { get; set; }
    public float NormalizedTime => Duration <= 0f ? 1f : Mathf.Clamp(Time / Duration, 0f, 1f);
}

public class FlowTimelineEvent
{
    public string Label { get; set; } = string.Empty;
    public float Time { get; set; }
    public List<GraphActionBase> Actions { get; set; } = new();
}

public class FlowTimelineNodeData : GraphNodeData, IFlowNode
{
    public float Duration { get; set; } = 1f;
    public bool RunUpdateOnEnter { get; set; }
    public List<GraphActionBase> StartActions { get; set; } = new();
    public List<GraphActionBase> UpdateActions { get; set; } = new();
    public List<FlowTimelineEvent> Events { get; set; } = new();
    public List<GraphActionBase> CompleteActions { get; set; } = new();
    public List<GraphActionBase> CancelActions { get; set; } = new();

    public override List<string> GetGraphTypes() => new() { FlowGraphAsset.GraphTypeName };
    public override string GetDisplayName() => $"Timeline {Duration:0.##}s";
    public override Color GetNodeColor() => new(0.48f, 0.58f, 0.95f);
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;
    public override bool CanBePrime() => false;
    public override string GetOutputPortName(int port) => "Completed";

    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        var data = new TimelineRuntimeData();
        runtime.SetNodeData(Id, data);

        ExecuteActions(context, StartActions, FlowTimelinePhase.Start, 0f, 0f, 0f);
        FireEvents(runtime, context, data, 0f, 0f);

        if (RunUpdateOnEnter)
            ExecuteActions(context, UpdateActions, FlowTimelinePhase.Update, 0f, 0f, 0f);

        if (Duration <= 0f)
        {
            data.Completed = true;
            ExecuteActions(context, CompleteActions, FlowTimelinePhase.Complete, 0f, 0f, 0f);
            return;
        }
    }

    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta)
    {
        var data = runtime.GetNodeData<TimelineRuntimeData>(Id);
        float previousTime = data.Elapsed;
        float nextTime = Mathf.Min(Duration, data.Elapsed + (float)delta);

        data.Elapsed = nextTime;
        ExecuteActions(context, UpdateActions, FlowTimelinePhase.Update, nextTime, previousTime, (float)delta);
        FireEvents(runtime, context, data, previousTime, nextTime);

        if (nextTime >= Duration)
        {
            data.Completed = true;
            ExecuteActions(context, CompleteActions, FlowTimelinePhase.Complete, nextTime, previousTime, (float)delta);
        }
    }

    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        if (Duration <= 0f ||
            runtime.TryGetNodeData<TimelineRuntimeData>(Id, out var data) &&
            data.Completed)
        {
            completion = NodeCompletion.Completed();
            return true;
        }

        completion = default;
        return false;
    }

    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        var data = runtime.GetNodeData<TimelineRuntimeData>(Id);
        if (!data.Completed)
            ExecuteActions(context, CancelActions, FlowTimelinePhase.Cancel, data.Elapsed, data.Elapsed, 0f);

        runtime.ClearNodeData(Id);
    }

    public override void CreateUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(260f, 0f) };

        var durationRow = new HBoxContainer();
        durationRow.AddChild(new Label
        {
            Text = "Duration",
            VerticalAlignment = VerticalAlignment.Center
        });

        var durationSpin = new SpinBox
        {
            MinValue = 0,
            MaxValue = 999999,
            Step = 0.01,
            Value = Duration,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        durationSpin.ValueChanged += value => Duration = (float)value;
        durationRow.AddChild(durationSpin);
        root.AddChild(durationRow);

        var updateOnEnter = new CheckBox
        {
            Text = "Update on enter",
            ButtonPressed = RunUpdateOnEnter
        };
        updateOnEnter.Toggled += value => RunUpdateOnEnter = value;
        root.AddChild(updateOnEnter);

        AddActionList(root, context, "Start", StartActions);
        AddActionList(root, context, "Update", UpdateActions);
        AddEventList(root, context);
        AddActionList(root, context, "Complete", CompleteActions);
        AddActionList(root, context, "Cancel", CancelActions);

        context.GraphNode.AddChild(root);
    }

    private void FireEvents(
        FlowGraphRuntime runtime,
        GraphExecutionContext context,
        TimelineRuntimeData data,
        float previousTime,
        float nextTime)
    {
        if (Events == null)
            return;

        for (int i = 0; i < Events.Count; i++)
        {
            FlowTimelineEvent timelineEvent = Events[i];
            if (timelineEvent == null || data.FiredEvents.Contains(i))
                continue;

            if (timelineEvent.Time < previousTime || timelineEvent.Time > nextTime)
                continue;

            data.FiredEvents.Add(i);
            ExecuteActions(
                context,
                timelineEvent.Actions,
                FlowTimelinePhase.Event,
                timelineEvent.Time,
                previousTime,
                nextTime - previousTime,
                timelineEvent.Label);
        }
    }

    private void ExecuteActions(
        GraphExecutionContext context,
        List<GraphActionBase> actions,
        FlowTimelinePhase phase,
        float time,
        float previousTime,
        float delta,
        string eventLabel = "")
    {
        if (actions == null || actions.Count == 0)
            return;

        var timelineContext = new FlowTimelineContext
        {
            Node = this,
            Phase = phase,
            EventLabel = eventLabel ?? string.Empty,
            Time = time,
            PreviousTime = previousTime,
            Delta = delta,
            Duration = Duration
        };

        context.UserData.Add(timelineContext);
        try
        {
            for (int i = 0; i < actions.Count; i++)
                actions[i]?.Execute(context);
        }
        finally
        {
            context.UserData.Remove(timelineContext);
        }
    }

    private static void AddActionList(
        VBoxContainer root,
        GraphEditorContext context,
        string title,
        List<GraphActionBase> actions)
    {
        root.AddChild(new HSeparator());
        root.AddChild(new Label { Text = title });

        var listControl = new ReorderableListControl<GraphActionBase>(
            items: actions,
            buildItemUi: action => action.CreateEditUI(context),
            getItemLabel: action => action.Description,
            availableTypes: SubTypeCache.GetSubTypes<GraphActionBase>(),
            factory: type => (GraphActionBase)System.Activator.CreateInstance(type),
            defaultItemExpanded: false,
            defaultListExpanded: false
        );

        root.AddChild(listControl.Build());
    }

    private void AddEventList(VBoxContainer root, GraphEditorContext context)
    {
        root.AddChild(new HSeparator());
        root.AddChild(new Label { Text = "Events" });

        var listControl = new ReorderableListControl<FlowTimelineEvent>(
            items: Events,
            buildItemUi: timelineEvent => BuildEventUi(timelineEvent, context),
            getItemLabel: timelineEvent =>
            {
                string label = string.IsNullOrWhiteSpace(timelineEvent.Label)
                    ? "Event"
                    : timelineEvent.Label;
                return $"{label} @ {timelineEvent.Time:0.##}s";
            },
            availableTypes: new[] { typeof(FlowTimelineEvent) },
            factory: _ => new FlowTimelineEvent(),
            defaultItemExpanded: false,
            defaultListExpanded: false
        );

        root.AddChild(listControl.Build());
    }

    private static Control BuildEventUi(FlowTimelineEvent timelineEvent, GraphEditorContext context)
    {
        var root = new VBoxContainer();

        var labelEdit = new LineEdit
        {
            PlaceholderText = "Event label",
            Text = timelineEvent.Label
        };
        labelEdit.TextChanged += value => timelineEvent.Label = value;
        root.AddChild(labelEdit);

        var timeSpin = new SpinBox
        {
            MinValue = 0,
            MaxValue = 999999,
            Step = 0.01,
            Value = timelineEvent.Time,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        timeSpin.ValueChanged += value => timelineEvent.Time = (float)value;
        root.AddChild(timeSpin);

        var actionList = new ReorderableListControl<GraphActionBase>(
            items: timelineEvent.Actions,
            buildItemUi: action => action.CreateEditUI(context),
            getItemLabel: action => action.Description,
            availableTypes: SubTypeCache.GetSubTypes<GraphActionBase>(),
            factory: type => (GraphActionBase)System.Activator.CreateInstance(type),
            defaultItemExpanded: false
        );
        root.AddChild(actionList.Build());

        return root;
    }

    private sealed class TimelineRuntimeData
    {
        public float Elapsed;
        public bool Completed;
        public HashSet<int> FiredEvents { get; } = new();
    }
}
