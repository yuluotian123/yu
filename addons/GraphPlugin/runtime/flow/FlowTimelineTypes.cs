using System.Collections.Generic;
using System.Text.Json.Serialization;
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
    public string TrackName { get; set; } = string.Empty;
    public string ClipId { get; internal set; } = string.Empty;
    public string ClipName { get; set; } = string.Empty;
    public float ClipTime { get; set; }
    public float PreviousClipTime { get; set; }
    public float ClipDuration { get; set; }
    public float NormalizedTime => Duration <= 0f ? 1f : Mathf.Clamp(Time / Duration, 0f, 1f);
    public float ClipNormalizedTime => ClipDuration <= 0f ? NormalizedTime : Mathf.Clamp(ClipTime / ClipDuration, 0f, 1f);
}

public class FlowTimelineMarker
{
    public string Label { get; set; } = string.Empty;
    public float Time { get; set; }
    public bool Enabled { get; set; } = true;
    public List<GraphActionBase> Actions { get; set; } = new();
}

public class FlowTimelineClip
{
    public string Id { get; set; } = GenerateUniqueId();
    public string Name { get; set; } = "Clip";
    public float StartTime { get; set; }
    public float Duration { get; set; } = 0.2f;
    public bool Enabled { get; set; } = true;
    public GraphActionBase Action { get; set; }

    [JsonIgnore]
    public float EndTime => StartTime + Mathf.Max(0f, Duration);

    internal static string GenerateUniqueId()
    {
        ulong time = Time.GetTicksUsec();
        uint rand1 = (uint)GD.Randi();
        uint rand2 = (uint)GD.Randi();
        return $"clip_{time:x}_{rand1:x}_{rand2:x}";
    }
}

public class FlowTimelineTrack
{
    public string Name { get; set; } = "Track";
    public bool Enabled { get; set; } = true;
    public List<FlowTimelineClip> Clips { get; set; } = new();
}
