using System.Collections.Generic;
using Godot;

public partial class FlowTimelineNodeData
{
    public void NormalizeTimelineData()
    {
        EnsureCollections();
        Duration = Mathf.Max(0f, Duration);

        for (int trackIndex = 0; trackIndex < Tracks.Count; trackIndex++)
        {
            FlowTimelineTrack track = Tracks[trackIndex];
            if (track == null)
            {
                Tracks[trackIndex] = new FlowTimelineTrack { Name = $"Track {trackIndex + 1}" };
                continue;
            }

            if (string.IsNullOrWhiteSpace(track.Name))
                track.Name = $"Track {trackIndex + 1}";
            track.Clips ??= new List<FlowTimelineClip>();

            for (int clipIndex = 0; clipIndex < track.Clips.Count; clipIndex++)
            {
                FlowTimelineClip clip = track.Clips[clipIndex];
                if (clip == null)
                {
                    track.Clips[clipIndex] = new FlowTimelineClip();
                    continue;
                }

                if (string.IsNullOrWhiteSpace(clip.Id))
                    clip.Id = $"clip_{trackIndex}_{clipIndex}_{Time.GetTicksUsec():x}";
                if (string.IsNullOrWhiteSpace(clip.Name))
                    clip.Name = clip.Action?.Description ?? $"Clip {clipIndex + 1}";
                clip.StartTime = Mathf.Max(0f, clip.StartTime);
                clip.Duration = Mathf.Max(0f, clip.Duration);
            }
        }

        for (int markerIndex = 0; markerIndex < Markers.Count; markerIndex++)
        {
            FlowTimelineMarker marker = Markers[markerIndex];
            if (marker == null)
            {
                Markers[markerIndex] = new FlowTimelineMarker { Label = $"Marker {markerIndex + 1}" };
                continue;
            }

            if (string.IsNullOrWhiteSpace(marker.Label))
                marker.Label = $"Marker {markerIndex + 1}";
            marker.Time = Mathf.Clamp(marker.Time, 0f, Duration);
            marker.Actions ??= new List<GraphActionBase>();
        }
    }

    private void EnsureCollections()
    {
        Tracks ??= new List<FlowTimelineTrack>();
        Markers ??= new List<FlowTimelineMarker>();
        CancelActions ??= new List<GraphActionBase>();
    }
}
