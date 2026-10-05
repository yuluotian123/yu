#if TOOLS
using System;
using System.Linq;
using Godot;

[Tool]
public sealed partial class GraphTimelinePanel : RefCounted
{
    private readonly Func<GraphAsset> _getCurrentGraph;
    private readonly Func<GraphEditorContext> _createContext;
    private readonly VBoxContainer _root;
    private readonly HBoxContainer _header;
    private readonly VBoxContainer _trackList;
    private readonly VBoxContainer _inspector;
    private readonly GraphTimelineCanvas _canvas;

    private readonly Func<EditorUndoRedoManager> _getUndoRedo;
    private readonly ScrollContainer _scroll;
    private readonly ScrollContainer _trackScroll;
    private SpinBox _playheadSpin, _zoomSpin, _durationSpin;
    private Button _addClipButton, _addMarkerButton;
    private string _lastSnapshot, _dragSnapshot;
    private float _snapStep = 0.05f;
    private FlowTimelineNodeData _timeline;
    private GraphAsset _boundGraph;
    private int _selectedTrackIndex = -1;
    private int _selectedClipIndex = -1;
    private int _selectedMarkerIndex = -1;
    private float _zoom = 1f;
    private float _playhead;
    private bool _snap = true;

    public GraphTimelinePanel() { }

    public GraphTimelinePanel(Func<GraphAsset> getCurrentGraph, Func<GraphEditorContext> createContext, Func<EditorUndoRedoManager> getUndoRedo = null)
    {
        _getCurrentGraph = getCurrentGraph;
        _getUndoRedo = getUndoRedo;
        _createContext = createContext;

        _root = new VBoxContainer
        {
            Visible = false,
            CustomMinimumSize = new Vector2(0, 260),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _root.AddThemeConstantOverride("separation", 4);

        _header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 34) };
        var headerScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        headerScroll.AddChild(_header);
        _root.AddChild(headerScroll);

        var body = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            DraggingEnabled = true,
            DraggerVisibility = SplitContainer.DraggerVisibilityEnum.Visible
        };
        body.AddThemeConstantOverride("separation", 6);
        _root.AddChild(body);

        _trackList = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(130, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _trackList.AddThemeConstantOverride("separation", 0);
        _trackScroll = new ScrollContainer { CustomMinimumSize = new Vector2(130, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = .35f, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever, MouseFilter = Control.MouseFilterEnum.Ignore };
        _trackScroll.AddChild(_trackList);
        body.AddChild(_trackScroll);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(220, 180)
        };
        _scroll = scroll;
        body.AddChild(scroll);

        _canvas = new GraphTimelineCanvas
        {
            FocusMode = Control.FocusModeEnum.Click,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _canvas.ClipSelected += SelectClip;
        _canvas.MarkerSelected += SelectMarker;
        _canvas.DeleteSelectedClipRequested += DeleteSelectedClip;
        _canvas.DeleteSelectedMarkerRequested += DeleteSelectedMarker;
        _canvas.PlayheadChanged += value =>
        {
            _playhead = value;
            _playheadSpin?.SetValueNoSignal(value);
        };
        _canvas.Changed += () =>
        {
            MarkChanged();
        };
        _canvas.TrackSelected += track => { ClearSelection(); _selectedTrackIndex = track >= 0 && track < _timeline.Tracks.Count ? track : -1; RefreshInspector(); RefreshCanvas(); };
        _canvas.EditStarted += () => { Checkpoint(); _dragSnapshot = _lastSnapshot; };
        _canvas.EditFinished += () => { _dragSnapshot = null; MarkChanged(); RefreshInspector(); };
        _canvas.EditCancelled += () =>
        {
            string snapshot = _dragSnapshot; _dragSnapshot = null;
            if (snapshot != null) ApplyTimelineSnapshot(_boundGraph, _timeline.Id, snapshot);
        };
        _canvas.ZoomRequested += ZoomAround;
        _canvas.PanRequested += delta => { scroll.ScrollHorizontal += (int)delta.X; scroll.ScrollVertical += (int)delta.Y; };
        scroll.GetVScrollBar().ValueChanged += value => _trackScroll.ScrollVertical = (int)value;
        scroll.GetHScrollBar().ValueChanged += _ => _canvas.QueueRedraw();
        scroll.AddChild(_canvas);

        var inspectorScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            CustomMinimumSize = new Vector2(200, 180),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = .65f,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        body.AddChild(inspectorScroll);

        _inspector = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _inspector.AddThemeConstantOverride("separation", 4);
        inspectorScroll.AddChild(_inspector);
    }

    public Control Root => _root;

    public void Bind(FlowTimelineNodeData timeline)
    {
        if (timeline == null)
        {
            Clear();
            return;
        }

        CancelPendingDrag();
        ClearSelection();
        _timeline = timeline;
        _boundGraph = _getCurrentGraph();
        _timeline.NormalizeTimelineData();
        Checkpoint();
        _playhead = Mathf.Clamp(_playhead, 0f, _timeline.Duration);
        _root.Visible = true;
        RefreshAll();
    }

    public void Clear()
    {
        CancelPendingDrag();
        _boundGraph = null;
        _timeline = null;
        _lastSnapshot = _dragSnapshot = null;
        _selectedTrackIndex = -1;
        _selectedClipIndex = -1;
        _selectedMarkerIndex = -1;
        _root.Visible = false;
        _canvas.Bind(null, 1f, 0f, true, -1, -1, -1);
    }

    private void RefreshAll()
    {
        RefreshHeader();
        RefreshTrackList();
        RefreshInspector();
        RefreshCanvas();
    }

    private void RefreshHeader()
    {
        ClearChildren(_header);
        if (_timeline == null)
            return;

        _header.AddChild(new Label
        {
            Text = "时间轴",
            CustomMinimumSize = new Vector2(90, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        _header.AddChild(new Label { Text = "总时长", VerticalAlignment = VerticalAlignment.Center });
        var duration = new SpinBox
        {
            MinValue = 0,
            MaxValue = 999999,
            Step = 0.01,
            Value = _timeline.Duration,
            CustomMinimumSize = new Vector2(100, 0)
        };
        duration.ValueChanged += value =>
        {
            if (_timeline == null)
                return;

            float contentEnd = Mathf.Max(_timeline.Tracks.SelectMany(track => track.Clips).Select(clip => clip.EndTime).DefaultIfEmpty(0).Max(),
                _timeline.Markers.Select(marker => marker.Time).DefaultIfEmpty(0).Max());
            _timeline.Duration = Mathf.Max((float)value, contentEnd);
            duration.SetValueNoSignal(_timeline.Duration);
            _timeline.NormalizeTimelineData();
            _playhead = Mathf.Clamp(_playhead, 0f, _timeline.Duration);
            MarkChanged();
            RefreshTrackList();
            RefreshInspector();
            RefreshCanvas();
        };
        _durationSpin = duration;
        duration.TooltipText = "总时长不能短于最后一个片段或标记；先移动或缩短它们再缩短时间轴。";
        _header.AddChild(duration);

        _header.AddChild(new Label { Text = "缩放", VerticalAlignment = VerticalAlignment.Center });
        var zoom = new SpinBox
        {
            MinValue = 0.05,
            MaxValue = 8,
            Step = 0.01,
            Value = _zoom,
            CustomMinimumSize = new Vector2(76, 0)
        };
        zoom.ValueChanged += value =>
        {
            _zoom = (float)value;
            RefreshCanvas();
        };
        _zoomSpin = zoom;
        _header.AddChild(zoom);

        _header.AddChild(new Label { Text = "时间", VerticalAlignment = VerticalAlignment.Center });
        var playhead = new SpinBox
        {
            MinValue = 0,
            MaxValue = _timeline.Duration,
            Step = 0.01,
            Value = _playhead,
            CustomMinimumSize = new Vector2(96, 0)
        };
        playhead.ValueChanged += value =>
        {
            _playhead = (float)value;
            RefreshCanvas();
        };
        _playheadSpin = playhead;
        _header.AddChild(playhead);

        var snap = new CheckBox
        {
            Text = "吸附",
            ButtonPressed = _snap
        };
        snap.Toggled += value =>
        {
            _snap = value;
            RefreshCanvas();
        };
        _header.AddChild(snap);

        var step = new OptionButton();
        float[] steps = { 0.01f, 0.05f, 0.1f };
        foreach (float value in steps) step.AddItem($"{value:0.##}s");
        step.Select(Array.IndexOf(steps, _snapStep));
        step.ItemSelected += index => { _snapStep = steps[index]; RefreshCanvas(); };
        _header.AddChild(step);
        AddHeaderButton("适应", () => ZoomAround(Mathf.Clamp((_scroll.Size.X - 40) / (Mathf.Max(.01f, _timeline.Duration) * GraphTimelineCanvas.PixelsPerSecond), .05f, 8) / _zoom, 0));
        AddHeaderButton("+ 轨道", AddTrack);
        _addClipButton = AddHeaderButton("+ Clip 片段", AddClip);
        _addClipButton.TooltipText = "选择持续动作，在当前时间插入片段";
        _addMarkerButton = AddHeaderButton("+ Marker 标记", AddMarker);
        _addMarkerButton.TooltipText = "选择瞬时动作，在当前时间触发一次";
    }

    private void RefreshTrackList()
    {
        ClearChildren(_trackList);
        if (_timeline == null)
            return;

        _trackList.AddChild(new Label { Text = "Marker（瞬时）\nClip 轨道", CustomMinimumSize = new Vector2(0, GraphTimelineCanvas.TracksTop) });
        for (int i = 0; i < _timeline.Tracks.Count; i++)
            _trackList.AddChild(BuildTrackRow(i));

        if (_timeline.Tracks.Count == 0)
        {
            var empty = new Label { Text = "(empty)" };
            empty.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
            _trackList.AddChild(empty);
        }
    }

    private Control BuildTrackRow(int trackIndex)
    {
        FlowTimelineTrack track = _timeline.Tracks[trackIndex];
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, GraphTimelineCanvas.TrackHeight) };
        var enabled = new CheckBox { ButtonPressed = track.Enabled };
        enabled.Toggled += value =>
        {
            track.Enabled = value;
            MarkChanged();
            RefreshCanvas();
        };
        row.AddChild(enabled);

        var name = new LineEdit
        {
            Text = track.Name,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        name.TextChanged += value =>
        {
            track.Name = value;
            MarkChanged();
        };
        row.AddChild(name);

        var delete = new Button
        {
            Text = "X",
            TooltipText = "Delete track",
            CustomMinimumSize = new Vector2(28, 0)
        };
        delete.Pressed += () =>
        {
            Checkpoint();
            _timeline.Tracks.RemoveAt(trackIndex);
            ClearSelection();
            MarkChanged();
            RefreshAll();
        };
        row.AddChild(delete);
        return row;
    }

    private void RefreshCanvas()
    {
        _durationSpin?.SetValueNoSignal(_timeline?.Duration ?? 0);
        if (_playheadSpin != null) _playheadSpin.MaxValue = _timeline?.Duration ?? 0;
        _canvas.SnapStep = _snapStep;
        _canvas.Bind(
            _timeline,
            _zoom,
            _playhead,
            _snap,
            _selectedTrackIndex,
            _selectedClipIndex,
            _selectedMarkerIndex);
    }

    private void AddTrack()
    {
        if (_timeline == null)
            return;

        Checkpoint();
        _timeline.Tracks.Add(new FlowTimelineTrack { Name = $"轨道 {_timeline.Tracks.Count + 1}" });
        _selectedTrackIndex = _timeline.Tracks.Count - 1;
        _selectedClipIndex = -1;
        _selectedMarkerIndex = -1;
        MarkChanged();
        RefreshAll();
    }

    private void AddClip()
    {
        if (_timeline == null) return;
        ShowActionSelector(_addClipButton, GraphTimelineActionKind.Clip, action =>
        {
            Checkpoint();
            if (_timeline.Tracks.Count == 0) _timeline.Tracks.Add(new FlowTimelineTrack { Name = "轨道 1" });
            int track = _selectedTrackIndex >= 0 && _selectedTrackIndex < _timeline.Tracks.Count ? _selectedTrackIndex : 0;
            var clip = new FlowTimelineClip { Name = GraphCallableCatalog.Name(action.GetType()), Action = action, StartTime = SnapTime(_playhead), Duration = .2f };
            _timeline.Duration = Mathf.Max(_timeline.Duration, clip.EndTime);
            _timeline.Tracks[track].Clips.Add(clip);
            _selectedTrackIndex = track; _selectedClipIndex = _timeline.Tracks[track].Clips.Count - 1; _selectedMarkerIndex = -1;
            MarkChanged(); RefreshAll();
        });
    }

    private void AddMarker()
    {
        if (_timeline == null) return;
        ShowActionSelector(_addMarkerButton, GraphTimelineActionKind.Marker, action =>
        {
            Checkpoint();
            _timeline.Markers.Add(new FlowTimelineMarker { Label = GraphCallableCatalog.Name(action.GetType()), Time = SnapTime(_playhead), Actions = new() { action } });
            ClearSelection(); _selectedMarkerIndex = _timeline.Markers.Count - 1;
            MarkChanged(); RefreshAll();
        });
    }

    private void SelectClip(int trackIndex, int clipIndex)
    {
        _selectedTrackIndex = trackIndex;
        _selectedClipIndex = clipIndex;
        _selectedMarkerIndex = -1;
        RefreshAll();
    }

    private void DeleteSelectedClip()
    {
        if (_timeline == null ||
            _selectedTrackIndex < 0 ||
            _selectedTrackIndex >= _timeline.Tracks.Count)
        {
            return;
        }

        FlowTimelineTrack track = _timeline.Tracks[_selectedTrackIndex];
        if (track?.Clips == null ||
            _selectedClipIndex < 0 ||
            _selectedClipIndex >= track.Clips.Count)
        {
            return;
        }

        Checkpoint();
        track.Clips.RemoveAt(_selectedClipIndex);
        ClearSelection();
        MarkChanged();
        RefreshAll();
    }

    private void DeleteSelectedMarker()
    {
        if (_timeline == null ||
            _timeline.Markers == null ||
            _selectedMarkerIndex < 0 ||
            _selectedMarkerIndex >= _timeline.Markers.Count)
        {
            return;
        }

        Checkpoint();
        _timeline.Markers.RemoveAt(_selectedMarkerIndex);
        ClearSelection();
        MarkChanged();
        RefreshAll();
    }

    private void SelectMarker(int markerIndex)
    {
        _selectedMarkerIndex = markerIndex;
        _selectedTrackIndex = -1;
        _selectedClipIndex = -1;
        RefreshAll();
    }

    private void ClearSelection()
    {
        _selectedTrackIndex = -1;
        _selectedClipIndex = -1;
        _selectedMarkerIndex = -1;
    }

    private void MarkChanged()
    {
        _timeline?.NormalizeTimelineData();
        _boundGraph?.MarkDirty();
        _canvas.QueueRedraw();
        if (_dragSnapshot == null) CommitEdit();
    }

    private float SnapTime(float time)
    {
        if (!_snap)
            return Mathf.Clamp(time, 0f, _timeline?.Duration ?? 0f);

        return Mathf.Clamp(Mathf.Round(time / _snapStep) * _snapStep, 0f, _timeline?.Duration ?? 0f);
    }

    private Button AddHeaderButton(string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        _header.AddChild(button);
        return button;
    }

    public void CancelPendingDrag()
    {
        string snapshot = _dragSnapshot; _dragSnapshot = null;
        if (snapshot != null && _timeline != null) ApplyTimelineSnapshot(_boundGraph, _timeline.Id, snapshot);
    }

    private void Checkpoint() => _lastSnapshot = _timeline == null ? null : GraphJsonHelper.Serialize(_timeline);

    private void CommitEdit()
    {
        if (_timeline == null) return;
        string after = GraphJsonHelper.Serialize(_timeline);
        if (_lastSnapshot != null && _lastSnapshot != after && _getUndoRedo?.Invoke() is { } undo)
        {
            GraphAsset graph = _boundGraph;
            undo.CreateAction("编辑时间轴 / Edit Timeline");
            undo.AddDoMethod(this, MethodName.ApplyTimelineSnapshot, graph, _timeline.Id, after);
            undo.AddUndoMethod(this, MethodName.ApplyTimelineSnapshot, graph, _timeline.Id, _lastSnapshot);
            undo.CommitAction(execute: false);
        }
        _lastSnapshot = after;
    }

    private void ApplyTimelineSnapshot(GraphAsset graph, string nodeId, string json)
    {
        if (graph?.FindNodeById(nodeId) is not FlowTimelineNodeData target) return;
        var snapshot = GraphJsonHelper.Deserialize<FlowTimelineNodeData>(json);
        target.Duration = snapshot.Duration; target.Tracks = snapshot.Tracks;
        target.Markers = snapshot.Markers; target.CancelActions = snapshot.CancelActions;
        graph.MarkDirty();
        if (target == _timeline && GodotObject.IsInstanceValid(_root))
        {
            _lastSnapshot = json; _canvas.ResetInteraction(); ClearSelection(); RefreshAll();
        }
    }

    private void ZoomAround(float factor, float mouseX)
    {
        float oldZoom = _zoom;
        float viewportX = mouseX - _scroll.ScrollHorizontal;
        _zoom = Mathf.Clamp(_zoom * factor, .05f, 8);
        _zoomSpin?.SetValueNoSignal(_zoom);
        RefreshCanvas();
        float scroll = mouseX * _zoom / oldZoom - viewportX;
        Callable.From(() => { if (GodotObject.IsInstanceValid(_scroll)) _scroll.ScrollHorizontal = Mathf.Max(0, (int)scroll); }).CallDeferred();
    }

    private static void ClearChildren(Control control)
    {
        foreach (Node child in control.GetChildren())
        {
            GraphEditorSignalCleanup.DisconnectSubtree(child);
            control.RemoveChild(child);
            child.QueueFree();
        }
    }
}
#endif
