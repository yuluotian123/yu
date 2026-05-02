using System.Collections.Generic;
using Godot;

public partial class FlowTimelineNodeData
{
    private void EnterClipsAt(GraphExecutionContext context, TimelineRuntimeData data, float time)
    {
        ForEachClip((track, clip, trackIndex, clipIndex) =>
        {
            if (!IsClipEnabled(track, clip) || time < clip.StartTime || time > clip.EndTime)
                return;

            string key = GetClipKey(trackIndex, clipIndex, clip);
            if (!data.ActiveClips.Add(key))
                return;

            ExecuteClipAction(context, data, track, clip, FlowTimelinePhase.Start, time, time, 0f);
            ExecuteClipAction(context, data, track, clip, FlowTimelinePhase.Update, time, time, 0f);
        });
    }

    private void EvaluateClips(
        GraphExecutionContext context,
        TimelineRuntimeData data,
        float previousTime,
        float nextTime,
        float delta)
    {
        ForEachClip((track, clip, trackIndex, clipIndex) =>
        {
            if (!IsClipEnabled(track, clip))
                return;

            string key = GetClipKey(trackIndex, clipIndex, clip);
            bool active = data.ActiveClips.Contains(key);

            if (!active && clip.StartTime > previousTime && clip.StartTime <= nextTime)
            {
                data.ActiveClips.Add(key);
                active = true;
                ExecuteClipAction(context, data, track, clip, FlowTimelinePhase.Start, clip.StartTime, previousTime, delta);
                ExecuteClipAction(context, data, track, clip, FlowTimelinePhase.Update, clip.StartTime, previousTime, delta);
            }

            bool overlapsFrame = clip.StartTime <= nextTime && clip.EndTime >= previousTime && nextTime >= clip.StartTime;
            if (active && overlapsFrame)
            {
                float sampleTime = Mathf.Clamp(nextTime, clip.StartTime, clip.EndTime);
                ExecuteClipAction(context, data, track, clip, FlowTimelinePhase.Update, sampleTime, previousTime, delta);
            }

            if (active && clip.EndTime > previousTime && clip.EndTime <= nextTime)
                data.ActiveClips.Remove(key);
        });
    }

    private static void ClearActiveClips(TimelineRuntimeData data)
    {
        data?.ActiveClips.Clear();
    }

    private void FireMarkers(
        GraphExecutionContext context,
        TimelineRuntimeData data,
        float previousTime,
        float nextTime)
    {
        if (Markers == null)
            return;

        for (int i = 0; i < Markers.Count; i++)
        {
            FlowTimelineMarker marker = Markers[i];
            if (marker == null || !marker.Enabled || data.FiredMarkers.Contains(i))
                continue;

            if (marker.Time < previousTime || marker.Time > nextTime)
                continue;

            data.FiredMarkers.Add(i);
            ExecuteActions(
                context,
                data,
                marker.Actions,
                FlowTimelinePhase.Event,
                marker.Time,
                previousTime,
                nextTime - previousTime,
                marker.Label);
        }
    }

    private void ExecuteClipAction(
        GraphExecutionContext context,
        TimelineRuntimeData data,
        FlowTimelineTrack track,
        FlowTimelineClip clip,
        FlowTimelinePhase phase,
        float time,
        float previousTime,
        float delta)
    {
        if (clip?.Action == null)
            return;

        FlowTimelineContext timelineContext = data.TimelineContext;
        PopulateContext(timelineContext, phase, time, previousTime, delta, string.Empty, track, clip);
        context.UserData.Add(timelineContext);
        try
        {
            clip.Action.Execute(context);
        }
        finally
        {
            context.UserData.Remove(timelineContext);
        }
    }

    private void ExecuteActions(
        GraphExecutionContext context,
        TimelineRuntimeData data,
        List<GraphActionBase> actions,
        FlowTimelinePhase phase,
        float time,
        float previousTime,
        float delta,
        string eventLabel = "")
    {
        if (actions == null || actions.Count == 0)
            return;

        FlowTimelineContext timelineContext = data.TimelineContext;
        PopulateContext(timelineContext, phase, time, previousTime, delta, eventLabel, null, null);
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

    private void PopulateContext(
        FlowTimelineContext timelineContext,
        FlowTimelinePhase phase,
        float time,
        float previousTime,
        float delta,
        string eventLabel,
        FlowTimelineTrack track,
        FlowTimelineClip clip)
    {
        float clipTime = 0f;
        float previousClipTime = 0f;
        float clipDuration = 0f;
        if (clip != null)
        {
            clipDuration = Mathf.Max(0f, clip.Duration);
            clipTime = Mathf.Clamp(time - clip.StartTime, 0f, clipDuration);
            previousClipTime = Mathf.Clamp(previousTime - clip.StartTime, 0f, clipDuration);
        }

        timelineContext.Node = this;
        timelineContext.Phase = phase;
        timelineContext.EventLabel = eventLabel ?? string.Empty;
        timelineContext.Time = time;
        timelineContext.PreviousTime = previousTime;
        timelineContext.Delta = delta;
        timelineContext.Duration = Duration;
        timelineContext.TrackName = track?.Name ?? string.Empty;
        timelineContext.ClipName = clip?.Name ?? string.Empty;
        timelineContext.ClipTime = clipTime;
        timelineContext.PreviousClipTime = previousClipTime;
        timelineContext.ClipDuration = clipDuration;
    }

    private static bool IsClipEnabled(FlowTimelineTrack track, FlowTimelineClip clip)
    {
        return track?.Enabled == true &&
               clip?.Enabled == true &&
               clip.Duration >= 0f;
    }

    private void ForEachClip(System.Action<FlowTimelineTrack, FlowTimelineClip, int, int> callback)
    {
        if (callback == null || Tracks == null)
            return;

        for (int trackIndex = 0; trackIndex < Tracks.Count; trackIndex++)
        {
            FlowTimelineTrack track = Tracks[trackIndex];
            if (track?.Clips == null)
                continue;

            for (int clipIndex = 0; clipIndex < track.Clips.Count; clipIndex++)
                callback(track, track.Clips[clipIndex], trackIndex, clipIndex);
        }
    }

    private static string GetClipKey(int trackIndex, int clipIndex, FlowTimelineClip clip)
    {
        return string.IsNullOrWhiteSpace(clip?.Id)
            ? $"{trackIndex}:{clipIndex}"
            : clip.Id;
    }

    private sealed class TimelineRuntimeData
    {
        public float Elapsed;
        public bool Completed;
        public HashSet<int> FiredMarkers { get; } = new();
        public HashSet<string> ActiveClips { get; } = new();
        public FlowTimelineContext TimelineContext { get; } = new();
    }
}
