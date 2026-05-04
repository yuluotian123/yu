#if TOOLS
using System;
using Godot;

public sealed partial class GraphTimelineCanvas : Control
{
    private const float RulerHeight = 24f;
    private const float TrackHeight = 36f;
    private const float PixelsPerSecond = 140f;
    private const float ClipHeight = 24f;
    private const float ResizeHandleWidth = 8f;

    private FlowTimelineNodeData _timeline;
    private float _zoom = 1f;
    private float _playhead;
    private bool _snap = true;
    private int _selectedTrackIndex = -1;
    private int _selectedClipIndex = -1;
    private int _selectedMarkerIndex = -1;
    private int _dragTrackIndex = -1;
    private int _dragClipIndex = -1;
    private int _dragMarkerIndex = -1;
    private float _dragStartMouseTime;
    private float _dragStartClipTime;
    private float _dragStartClipDuration;
    private float _dragStartMarkerTime;
    private DragMode _dragMode = DragMode.None;

    public event Action<int, int> ClipSelected;
    public event Action<int> MarkerSelected;
    public event Action<float> PlayheadChanged;
    public event Action Changed;
    public event Action DeleteSelectedClipRequested;
    public event Action DeleteSelectedMarkerRequested;

    private enum DragMode
    {
        None,
        MoveClip,
        ResizeClip,
        MoveMarker,
        Playhead
    }

    public void Bind(
        FlowTimelineNodeData timeline,
        float zoom,
        float playhead,
        bool snap,
        int selectedTrackIndex,
        int selectedClipIndex,
        int selectedMarkerIndex)
    {
        _timeline = timeline;
        _zoom = Mathf.Max(0.25f, zoom);
        _playhead = playhead;
        _snap = snap;
        _selectedTrackIndex = selectedTrackIndex;
        _selectedClipIndex = selectedClipIndex;
        _selectedMarkerIndex = selectedMarkerIndex;
        UpdateMinimum();
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.12f, 0.13f, 0.16f));
        if (_timeline == null)
            return;

        float width = Mathf.Max(Size.X, _timeline.Duration * ScaleX + 40f);
        DrawRect(new Rect2(0, 0, width, RulerHeight), new Color(0.18f, 0.19f, 0.23f));
        DrawGrid(width);
        DrawTracks(width);
        DrawMarkers();
        DrawPlayhead();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_timeline == null)
            return;

        if (@event is InputEventMouseButton mouseButton)
            HandleMouseButton(mouseButton);
        else if (@event is InputEventMouseMotion mouseMotion)
            HandleMouseMotion(mouseMotion);
        else if (@event is InputEventKey keyEvent)
            HandleKey(keyEvent);
    }

    private float ScaleX => PixelsPerSecond * _zoom;

    private void DrawGrid(float width)
    {
        float majorStep = 0.5f;
        float minorStep = 0.1f;
        for (float time = 0f; time <= _timeline.Duration + 0.001f; time += minorStep)
        {
            bool major = Mathf.IsEqualApprox(Mathf.PosMod(time, majorStep), 0f);
            Color color = major ? new Color(0.36f, 0.38f, 0.44f) : new Color(0.22f, 0.23f, 0.27f);
            float x = TimeToX(time);
            DrawLine(new Vector2(x, 0f), new Vector2(x, Size.Y), color, major ? 1.4f : 1f);
        }

        DrawLine(new Vector2(0f, RulerHeight), new Vector2(width, RulerHeight), new Color(0.42f, 0.43f, 0.48f), 1f);
    }

    private void DrawTracks(float width)
    {
        for (int trackIndex = 0; trackIndex < _timeline.Tracks.Count; trackIndex++)
        {
            FlowTimelineTrack track = _timeline.Tracks[trackIndex];
            float y = RulerHeight + trackIndex * TrackHeight;
            Color rowColor = trackIndex % 2 == 0
                ? new Color(0.145f, 0.15f, 0.18f)
                : new Color(0.12f, 0.125f, 0.15f);
            DrawRect(new Rect2(0, y, width, TrackHeight), rowColor);
            DrawLine(new Vector2(0, y + TrackHeight), new Vector2(width, y + TrackHeight), new Color(0.22f, 0.23f, 0.27f), 1f);

            if (track?.Clips == null)
                continue;

            for (int clipIndex = 0; clipIndex < track.Clips.Count; clipIndex++)
                DrawClip(track.Clips[clipIndex], trackIndex, clipIndex);
        }
    }

    private void DrawClip(FlowTimelineClip clip, int trackIndex, int clipIndex)
    {
        if (clip == null)
            return;

        float x = TimeToX(clip.StartTime);
        float y = RulerHeight + trackIndex * TrackHeight + (TrackHeight - ClipHeight) * 0.5f;
        float width = Mathf.Max(6f, clip.Duration * ScaleX);
        bool selected = trackIndex == _selectedTrackIndex && clipIndex == _selectedClipIndex;
        Color color = clip.Enabled
            ? new Color(0.36f, 0.52f, 0.95f)
            : new Color(0.28f, 0.30f, 0.36f);
        DrawRect(new Rect2(x, y, width, ClipHeight), color);
        DrawRect(new Rect2(x, y, width, ClipHeight), selected ? new Color(1f, 0.86f, 0.36f) : new Color(0.08f, 0.09f, 0.11f), false, selected ? 2f : 1f);
        DrawRect(new Rect2(x + width - ResizeHandleWidth, y, ResizeHandleWidth, ClipHeight), new Color(1f, 1f, 1f, 0.18f));
    }

    private void DrawMarkers()
    {
        if (_timeline.Markers == null)
            return;

        for (int markerIndex = 0; markerIndex < _timeline.Markers.Count; markerIndex++)
        {
            FlowTimelineMarker marker = _timeline.Markers[markerIndex];
            if (marker == null)
                continue;

            float x = TimeToX(marker.Time);
            Color color = marker.Enabled
                ? new Color(0.96f, 0.62f, 0.18f)
                : new Color(0.45f, 0.38f, 0.3f);
            if (markerIndex == _selectedMarkerIndex)
                color = new Color(1f, 0.9f, 0.35f);
            DrawLine(new Vector2(x, RulerHeight), new Vector2(x, Size.Y), color, markerIndex == _selectedMarkerIndex ? 2.5f : 1.5f);
            DrawCircle(new Vector2(x, RulerHeight * 0.5f), markerIndex == _selectedMarkerIndex ? 5f : 4f, color);
        }
    }

    private void DrawPlayhead()
    {
        float x = TimeToX(_playhead);
        DrawLine(new Vector2(x, 0f), new Vector2(x, Size.Y), new Color(1f, 0.22f, 0.22f), 2f);
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.ButtonIndex != MouseButton.Left)
            return;

        if (mouseButton.Pressed)
        {
            GrabFocus();

            if (TryHitMarker(mouseButton.Position, out int markerIndex))
            {
                MarkerSelected?.Invoke(markerIndex);
                _dragMarkerIndex = markerIndex;
                _dragStartMouseTime = XToTime(mouseButton.Position.X);
                _dragStartMarkerTime = _timeline.Markers[markerIndex].Time;
                _dragMode = DragMode.MoveMarker;
                SetPlayheadToTime(_timeline.Markers[markerIndex].Time);
                return;
            }

            if (TryHitClip(mouseButton.Position, out int trackIndex, out int clipIndex, out bool resize))
            {
                ClipSelected?.Invoke(trackIndex, clipIndex);
                _dragTrackIndex = trackIndex;
                _dragClipIndex = clipIndex;
                FlowTimelineClip clip = _timeline.Tracks[trackIndex].Clips[clipIndex];
                _dragStartMouseTime = XToTime(mouseButton.Position.X);
                _dragStartClipTime = clip.StartTime;
                _dragStartClipDuration = clip.Duration;
                _dragMode = resize ? DragMode.ResizeClip : DragMode.MoveClip;
                return;
            }

            if (mouseButton.Position.Y <= RulerHeight)
            {
                _dragMode = DragMode.Playhead;
                SetPlayheadFromX(mouseButton.Position.X);
            }
        }
        else
        {
            _dragMode = DragMode.None;
            _dragTrackIndex = -1;
            _dragClipIndex = -1;
            _dragMarkerIndex = -1;
        }
    }

    private void HandleKey(InputEventKey keyEvent)
    {
        if (!keyEvent.Pressed || keyEvent.Echo)
            return;

        if (keyEvent.Keycode is not (Key.Delete or Key.Backspace))
            return;

        if (_selectedMarkerIndex >= 0)
            DeleteSelectedMarkerRequested?.Invoke();
        else if (_selectedTrackIndex >= 0 && _selectedClipIndex >= 0)
            DeleteSelectedClipRequested?.Invoke();
        else
            return;

        AcceptEvent();
    }

    private void HandleMouseMotion(InputEventMouseMotion mouseMotion)
    {
        switch (_dragMode)
        {
            case DragMode.Playhead:
                SetPlayheadFromX(mouseMotion.Position.X);
                break;
            case DragMode.MoveClip:
                DragClip(mouseMotion.Position.X, false);
                break;
            case DragMode.ResizeClip:
                DragClip(mouseMotion.Position.X, true);
                break;
            case DragMode.MoveMarker:
                DragMarker(mouseMotion.Position.X);
                break;
        }
    }

    private void DragClip(float mouseX, bool resize)
    {
        if (_dragTrackIndex < 0 ||
            _dragTrackIndex >= _timeline.Tracks.Count ||
            _dragClipIndex < 0 ||
            _dragClipIndex >= _timeline.Tracks[_dragTrackIndex].Clips.Count)
        {
            return;
        }

        FlowTimelineClip clip = _timeline.Tracks[_dragTrackIndex].Clips[_dragClipIndex];
        float delta = XToTime(mouseX) - _dragStartMouseTime;
        if (resize)
            clip.Duration = Mathf.Max(0f, SnapTime(_dragStartClipDuration + delta));
        else
            clip.StartTime = SnapTime(Mathf.Max(0f, _dragStartClipTime + delta));

        Changed?.Invoke();
        QueueRedraw();
    }

    private void DragMarker(float mouseX)
    {
        if (_dragMarkerIndex < 0 ||
            _timeline.Markers == null ||
            _dragMarkerIndex >= _timeline.Markers.Count)
        {
            return;
        }

        FlowTimelineMarker marker = _timeline.Markers[_dragMarkerIndex];
        if (marker == null)
            return;

        float delta = XToTime(mouseX) - _dragStartMouseTime;
        marker.Time = SnapTime(_dragStartMarkerTime + delta);
        SetPlayheadToTime(marker.Time);
        Changed?.Invoke();
        QueueRedraw();
    }

    private bool TryHitClip(Vector2 position, out int trackIndex, out int clipIndex, out bool resize)
    {
        trackIndex = -1;
        clipIndex = -1;
        resize = false;

        int row = (int)((position.Y - RulerHeight) / TrackHeight);
        if (row < 0 || row >= _timeline.Tracks.Count)
            return false;

        FlowTimelineTrack track = _timeline.Tracks[row];
        if (track?.Clips == null)
            return false;

        for (int i = track.Clips.Count - 1; i >= 0; i--)
        {
            FlowTimelineClip clip = track.Clips[i];
            float x = TimeToX(clip.StartTime);
            float y = RulerHeight + row * TrackHeight + (TrackHeight - ClipHeight) * 0.5f;
            float width = Mathf.Max(6f, clip.Duration * ScaleX);
            var rect = new Rect2(x, y, width, ClipHeight);
            if (!rect.HasPoint(position))
                continue;

            trackIndex = row;
            clipIndex = i;
            resize = position.X >= x + width - ResizeHandleWidth;
            return true;
        }

        return false;
    }

    private bool TryHitMarker(Vector2 position, out int markerIndex)
    {
        markerIndex = -1;
        if (_timeline.Markers == null || position.Y > RulerHeight + 8f)
            return false;

        for (int i = 0; i < _timeline.Markers.Count; i++)
        {
            FlowTimelineMarker marker = _timeline.Markers[i];
            if (marker == null)
                continue;

            if (Mathf.Abs(position.X - TimeToX(marker.Time)) <= 6f)
            {
                markerIndex = i;
                return true;
            }
        }

        return false;
    }

    private void SetPlayheadFromX(float x)
    {
        SetPlayheadToTime(SnapTime(XToTime(x)));
    }

    private void SetPlayheadToTime(float time)
    {
        _playhead = Mathf.Clamp(time, 0f, _timeline?.Duration ?? 0f);
        PlayheadChanged?.Invoke(_playhead);
        QueueRedraw();
    }

    private float TimeToX(float time) => time * ScaleX;

    private float XToTime(float x) => Mathf.Clamp(x / ScaleX, 0f, _timeline?.Duration ?? 0f);

    private float SnapTime(float time)
    {
        if (!_snap)
            return Mathf.Clamp(time, 0f, _timeline?.Duration ?? 0f);

        return Mathf.Clamp(Mathf.Round(time / 0.05f) * 0.05f, 0f, _timeline?.Duration ?? 0f);
    }

    private void UpdateMinimum()
    {
        if (_timeline == null)
        {
            CustomMinimumSize = new Vector2(500, 180);
            return;
        }

        float width = Mathf.Max(600f, _timeline.Duration * ScaleX + 80f);
        float height = RulerHeight + Mathf.Max(1, _timeline.Tracks.Count) * TrackHeight + 24f;
        CustomMinimumSize = new Vector2(width, height);
    }
}
#endif
