using System.Collections.Generic;
using Godot;

public partial class FlowTimelineNodeData : GraphNodeData, IFlowNode
{
    public float Duration { get; set; } = 1f;
    public List<FlowTimelineTrack> Tracks { get; set; } = new();
    public List<FlowTimelineMarker> Markers { get; set; } = new();
    public List<GraphActionBase> CancelActions { get; set; } = new();

    public override List<string> GetGraphTypes() => new() { FlowGraphAsset.GraphTypeName };
    public override string GetCategory() => "Flow";
    public override string GetDisplayName() => $"FlowTimeline";
    public override string GetMenuName() => "Timeline";
    public override List<string> GetSearchKeywords() => new() { "ability", "timeline", "clip", "track" };
    public override Color GetNodeColor() => new(0.48f, 0.58f, 0.95f);
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;
    public override bool CanBePrime() => false;
    public override string GetOutputPortName(int port) => "Completed";

    public override void Validate(GraphAsset graph, GraphValidationResult result)
    {
        NormalizeTimelineData();

        var clipIds = new HashSet<string>(System.StringComparer.Ordinal);
        for (int trackIndex = 0; trackIndex < Tracks.Count; trackIndex++)
        {
            FlowTimelineTrack track = Tracks[trackIndex];
            if (track?.Clips == null)
                continue;

            for (int clipIndex = 0; clipIndex < track.Clips.Count; clipIndex++)
            {
                FlowTimelineClip clip = track.Clips[clipIndex];
                if (clip == null || clipIds.Add(clip.Id))
                    continue;

                result.AddError(
                    $"Timeline clip Id is duplicated: '{clip.Id}'.",
                    Id);
            }
        }
    }

    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        NormalizeTimelineData();

        var data = new TimelineRuntimeData();
        runtime.SetNodeData(Id, data);

        FireMarkers(context, data, 0f, 0f);
        EnterClipsAt(context, data, 0f);

        if (Duration <= 0f)
        {
            data.Completed = true;
            CompleteActiveClips(context, data, 0f);
        }
    }

    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta)
    {
        NormalizeTimelineData();

        var data = runtime.GetNodeData<TimelineRuntimeData>(Id);
        float previousTime = data.Elapsed;
        float nextTime = Mathf.Min(Duration, data.Elapsed + (float)delta);

        data.Elapsed = nextTime;
        EvaluateClips(context, data, previousTime, nextTime, (float)delta);
        FireMarkers(context, data, previousTime, nextTime);

        if (nextTime >= Duration)
        {
            data.Completed = true;
            CompleteActiveClips(context, data, nextTime);
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
        {
            CancelActiveClips(context, data, data.Elapsed);
            ExecuteActions(context, data, CancelActions, FlowTimelinePhase.Cancel, data.Elapsed, data.Elapsed, 0f);
        }

        runtime.ClearNodeData(Id);
    }
}
