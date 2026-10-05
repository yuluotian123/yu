#if TOOLS
using System;
using Godot;

[Tool]
public sealed partial class GraphTimelineCanvas : Control
{
    public const float RulerHeight = 24f;
    public const float MarkerHeight = 28f;
    public const float TracksTop = RulerHeight + MarkerHeight;
    public const float TrackHeight = 36f;
    public const float PixelsPerSecond = 140f;
    private const float ClipHeight = 24f;
    private const float HandleWidth = 7f;
    private FlowTimelineNodeData _timeline;
    private float _zoom = 1, _playhead;
    private bool _snap = true;
    public float SnapStep { get; set; } = 0.05f;
    private int _selectedTrackIndex = -1, _selectedClipIndex = -1, _selectedMarkerIndex = -1;
    private FlowTimelineClip _dragClip;
    private FlowTimelineMarker _dragMarker;
    private float _dragStartMouseTime, _dragStartTime, _dragStartDuration;
    private DragMode _dragMode;
    private enum DragMode { None, MoveClip, LeftEdge, RightEdge, MoveMarker, Playhead, Pan }

    public event Action<int, int> ClipSelected;
    public event Action<int> MarkerSelected;
    public event Action<int> TrackSelected;
    public event Action<float> PlayheadChanged;
    public event Action Changed;
    public event Action EditStarted;
    public event Action EditFinished;
    public event Action EditCancelled;
    public event Action<float, float> ZoomRequested;
    public event Action<Vector2> PanRequested;
    public event Action DeleteSelectedClipRequested;
    public event Action DeleteSelectedMarkerRequested;

    public void Bind(FlowTimelineNodeData timeline, float zoom, float playhead, bool snap, int selectedTrackIndex, int selectedClipIndex, int selectedMarkerIndex)
    {
        if (_timeline != timeline) ResetDrag();
        _timeline = timeline; _zoom = Mathf.Clamp(zoom, 0.05f, 8f); _playhead = playhead; _snap = snap;
        _selectedTrackIndex = selectedTrackIndex; _selectedClipIndex = selectedClipIndex; _selectedMarkerIndex = selectedMarkerIndex;
        UpdateMinimum(); QueueRedraw();
    }
    private float ScaleX => PixelsPerSecond * _zoom;
    private float TimeToX(float time) => time * ScaleX;
    private float RawTime(float x) => x / ScaleX;
    private float SnapTime(float time, bool bypass = false) => _snap && !bypass ? Mathf.Round(time / SnapStep) * SnapStep : time;

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("202329"));
        if (_timeline == null) return;
        DrawRect(new Rect2(0, 0, Size.X, RulerHeight), new Color("30343e"));
        DrawRect(new Rect2(0, RulerHeight, Size.X, MarkerHeight), new Color("382e23"));
        var font = GetThemeDefaultFont();
        for (int trackIndex = 0; trackIndex < _timeline.Tracks.Count; trackIndex++)
        {
            var track = _timeline.Tracks[trackIndex];
            float y = TracksTop + trackIndex * TrackHeight;
            DrawRect(new Rect2(0, y, Size.X, TrackHeight), trackIndex % 2 == 0 ? new Color("282c34") : new Color("22262d"));
            for (int clipIndex = 0; clipIndex < track.Clips.Count; clipIndex++)
            {
                var clip = track.Clips[clipIndex];
                Rect2 rect = ClipRect(clip, trackIndex);
                bool selected = trackIndex == _selectedTrackIndex && clipIndex == _selectedClipIndex;
                bool compatible = clip.Action == null || GraphTimelineActionRules.Supports(clip.Action.GetType(), GraphTimelineActionKind.Clip);
                Color color = !compatible ? new Color("a74949") : clip.Enabled && track.Enabled ? new Color("497abe") : new Color("4e535e");
                DrawRect(rect, color);
                DrawRect(rect, selected ? new Color("ffd773") : new Color("11151c"), false, selected ? 2 : 1);
                float handle = Mathf.Min(HandleWidth, rect.Size.X / 3);
                DrawRect(new Rect2(rect.Position, new Vector2(handle, rect.Size.Y)), new Color(1, 1, 1, .2f));
                DrawRect(new Rect2(rect.End.X - handle, rect.Position.Y, handle, rect.Size.Y), new Color(1, 1, 1, .2f));
                if (rect.Size.X > 28)
                    DrawString(font, rect.Position + new Vector2(10, 17), clip.Name, HorizontalAlignment.Left, rect.Size.X - 20, 12, Colors.White);
            }
        }
        // Draw only the visible ticks. Long timelines must not draw millions of grid lines.
        float desiredStep = 80f / ScaleX;
        float power = Mathf.Pow(10, Mathf.Floor(Mathf.Log(desiredStep) / Mathf.Log(10)));
        float major = power * (desiredStep / power <= 2 ? 2 : desiredStep / power <= 5 ? 5 : 10);
        var scroll = GetParent() as ScrollContainer;
        float left = scroll?.ScrollHorizontal ?? 0;
        float right = left + (scroll?.Size.X ?? Size.X);
        float minor = major / 5;
        int first = Mathf.Max(0, Mathf.FloorToInt(left / ScaleX / minor));
        int last = Mathf.CeilToInt(Mathf.Min(_timeline.Duration * ScaleX, right) / ScaleX / minor);
        for (int i = first; i <= last; i++)
        {
            float time = i * minor, x = TimeToX(time);
            bool full = i % 5 == 0;
            DrawLine(new Vector2(x, full ? 0 : RulerHeight - 5), new Vector2(x, RulerHeight), new Color("747d8d"));
            if (full)
            {
                DrawString(font, new Vector2(x + 4, 16), $"{time:0.###}s", HorizontalAlignment.Left, -1, 12, new Color("d0d8e5"));
                DrawLine(new Vector2(x, TracksTop), new Vector2(x, Size.Y), new Color(1, 1, 1, .09f));
            }
        }
        for (int i = 0; i < _timeline.Markers.Count; i++)
        {
            var marker = _timeline.Markers[i];
            float x = TimeToX(marker.Time), y = RulerHeight + MarkerHeight / 2;
            Color color = !marker.Enabled ? new Color("71604a") : i == _selectedMarkerIndex ? new Color("ffe39b") : new Color("efa746");
            DrawColoredPolygon(new[] { new Vector2(x, y - 7), new Vector2(x + 6, y), new Vector2(x, y + 7), new Vector2(x - 6, y) }, color);
            float labelWidth = 110;
            foreach (var other in _timeline.Markers)
                if (other.Time > marker.Time) labelWidth = Mathf.Min(labelWidth, TimeToX(other.Time) - x - 18);
            if (labelWidth > 15) DrawString(font, new Vector2(x + 9, y + 4), marker.Label, HorizontalAlignment.Left, labelWidth, 11, color);
        }
        DrawLine(new Vector2(TimeToX(_playhead), 0), new Vector2(TimeToX(_playhead), Size.Y), new Color("f97070"), 2);
        DrawLine(new Vector2(TimeToX(_timeline.Duration), 0), new Vector2(TimeToX(_timeline.Duration), Size.Y), new Color("bac4d8"), 1);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_timeline == null) return;
        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            if ((key.Keycode == Key.Escape || key.CtrlPressed && key.Keycode == Key.Z) && _dragMode != DragMode.None)
            {
                bool editing = _dragClip != null || _dragMarker != null;
                ResetDrag(); if (editing) EditCancelled?.Invoke(); AcceptEvent();
            }
            else if (_dragMode == DragMode.None && key.Keycode is Key.Delete or Key.Backspace)
            {
                if (_selectedMarkerIndex >= 0) DeleteSelectedMarkerRequested?.Invoke();
                else if (_selectedClipIndex >= 0) DeleteSelectedClipRequested?.Invoke();
                AcceptEvent();
            }
            return;
        }
        if (@event is InputEventMouseButton mouse)
        {
            if (mouse.Pressed && mouse.CtrlPressed && mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                if (_dragMode == DragMode.None) ZoomRequested?.Invoke(mouse.ButtonIndex == MouseButton.WheelUp ? 1.25f : 0.8f, mouse.Position.X);
                AcceptEvent(); return;
            }
            if (mouse.ButtonIndex == MouseButton.Middle)
            {
                _dragMode = mouse.Pressed ? DragMode.Pan : DragMode.None; AcceptEvent(); return;
            }
            if (mouse.ButtonIndex != MouseButton.Left) return;
            if (!mouse.Pressed)
            {
                bool editing = _dragClip != null || _dragMarker != null;
                ResetDrag(); if (editing) EditFinished?.Invoke(); AcceptEvent(); return;
            }
            GrabFocus();
            if (HitMarker(mouse.Position, out int markerIndex))
            {
                MarkerSelected?.Invoke(markerIndex);
                _dragMarker = _timeline.Markers[markerIndex]; _dragStartTime = _dragMarker.Time; _dragMode = DragMode.MoveMarker;
            }
            else if (HitClip(mouse.Position, out int trackIndex, out int clipIndex, out DragMode mode))
            {
                ClipSelected?.Invoke(trackIndex, clipIndex);
                _dragClip = _timeline.Tracks[trackIndex].Clips[clipIndex]; _dragStartTime = _dragClip.StartTime;
                _dragStartDuration = _dragClip.Duration; _dragMode = mode;
            }
            else if (mouse.Position.Y < RulerHeight)
            {
                _dragMode = DragMode.Playhead; SetPlayhead(mouse.Position.X, mouse.AltPressed);
            }
            else
            {
                TrackSelected?.Invoke(Mathf.FloorToInt((mouse.Position.Y - TracksTop) / TrackHeight));
                SetPlayhead(mouse.Position.X, mouse.AltPressed);
            }
            _dragStartMouseTime = RawTime(mouse.Position.X);
            if (_dragClip != null || _dragMarker != null) EditStarted?.Invoke();
            AcceptEvent(); return;
        }
        if (@event is not InputEventMouseMotion motion) return;
        if (_dragMode == DragMode.None)
        {
            if (HitClip(motion.Position, out int t, out int c, out DragMode mode))
            {
                var clip = _timeline.Tracks[t].Clips[c];
                MouseDefaultCursorShape = mode == DragMode.MoveClip ? CursorShape.Move : CursorShape.Hsize;
                TooltipText = $"Clip · {clip.Name}\n{clip.StartTime:0.###}–{clip.EndTime:0.###} 秒\n{(clip.Action == null ? "请选择持续动作" : GraphCallableCatalog.Name(clip.Action.GetType()))}";
            }
            else if (HitMarker(motion.Position, out int m))
            {
                MouseDefaultCursorShape = CursorShape.Move; TooltipText = $"Marker · {_timeline.Markers[m].Label}\n{_timeline.Markers[m].Time:0.###} 秒 · 触发一次";
            }
            else { MouseDefaultCursorShape = CursorShape.Arrow; TooltipText = "Ctrl + 滚轮缩放 · 中键平移 · Alt 暂停吸附 · Esc 取消拖动"; }
            return;
        }
        if (_dragMode == DragMode.Pan) PanRequested?.Invoke(-motion.Relative);
        else if (_dragMode == DragMode.Playhead) SetPlayhead(motion.Position.X, motion.AltPressed);
        else
        {
            float delta = RawTime(motion.Position.X) - _dragStartMouseTime;
            if (_dragMarker != null) _dragMarker.Time = Mathf.Clamp(SnapTime(_dragStartTime + delta, motion.AltPressed), 0, _timeline.Duration);
            else if (_dragClip != null)
            {
                float end = _dragStartTime + _dragStartDuration;
                if (_dragMode == DragMode.MoveClip)
                    _dragClip.StartTime = Mathf.Clamp(SnapTime(_dragStartTime + delta, motion.AltPressed), 0, Mathf.Max(0, _timeline.Duration - _dragStartDuration));
                else if (_dragMode == DragMode.LeftEdge)
                {
                    _dragClip.StartTime = Mathf.Clamp(SnapTime(_dragStartTime + delta, motion.AltPressed), 0, Mathf.Max(0, end - .01f));
                    _dragClip.Duration = end - _dragClip.StartTime;
                }
                else
                    _dragClip.Duration = Mathf.Clamp(SnapTime(end + delta, motion.AltPressed) - _dragStartTime, .01f, Mathf.Max(.01f, _timeline.Duration - _dragStartTime));
            }
            Changed?.Invoke();
        }
        QueueRedraw(); AcceptEvent();
    }

    private Rect2 ClipRect(FlowTimelineClip clip, int track) => new(TimeToX(clip.StartTime), TracksTop + track * TrackHeight + 6, Mathf.Max(8, clip.Duration * ScaleX), ClipHeight);
    private bool HitClip(Vector2 point, out int trackIndex, out int clipIndex, out DragMode mode)
    {
        trackIndex = Mathf.FloorToInt((point.Y - TracksTop) / TrackHeight); clipIndex = -1; mode = DragMode.None;
        if (trackIndex < 0 || trackIndex >= _timeline.Tracks.Count) return false;
        var clips = _timeline.Tracks[trackIndex].Clips;
        for (int i = clips.Count - 1; i >= 0; i--)
        {
            Rect2 rect = ClipRect(clips[i], trackIndex);
            if (!rect.HasPoint(point)) continue;
            float handle = Mathf.Min(HandleWidth, rect.Size.X / 3);
            clipIndex = i;
            mode = point.X < rect.Position.X + handle ? DragMode.LeftEdge : point.X > rect.End.X - handle ? DragMode.RightEdge : DragMode.MoveClip;
            return true;
        }
        return false;
    }
    private bool HitMarker(Vector2 point, out int index)
    {
        index = -1;
        if (point.Y < RulerHeight || point.Y >= TracksTop) return false;
        for (int i = _timeline.Markers.Count - 1; i >= 0; i--)
            if (Mathf.Abs(point.X - TimeToX(_timeline.Markers[i].Time)) <= 8) { index = i; return true; }
        return false;
    }
    private void SetPlayhead(float x, bool bypass)
    {
        _playhead = Mathf.Clamp(SnapTime(RawTime(x), bypass), 0, _timeline.Duration);
        PlayheadChanged?.Invoke(_playhead); QueueRedraw();
    }
    internal void ResetInteraction() => ResetDrag();
    private void ResetDrag() { _dragMode = DragMode.None; _dragClip = null; _dragMarker = null; }
    private void UpdateMinimum() => CustomMinimumSize = new Vector2(_timeline == null ? 360 : Mathf.Max(360, _timeline.Duration * ScaleX + 40),
        TracksTop + Mathf.Max(1, _timeline?.Tracks.Count ?? 0) * TrackHeight + 16);
}
#endif
