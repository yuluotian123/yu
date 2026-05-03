#if TOOLS
using System;
using Godot;

public sealed partial class GraphTimelinePanel
{
    private readonly Func<GraphAsset> _getCurrentGraph;
    private readonly Func<GraphEditorContext> _createContext;
    private readonly VBoxContainer _root;
    private readonly HBoxContainer _header;
    private readonly VBoxContainer _trackList;
    private readonly VBoxContainer _inspector;
    private readonly GraphTimelineCanvas _canvas;

    private FlowTimelineNodeData _timeline;
    private int _selectedTrackIndex = -1;
    private int _selectedClipIndex = -1;
    private int _selectedMarkerIndex = -1;
    private float _zoom = 1f;
    private float _playhead;
    private bool _snap = true;

    public GraphTimelinePanel(Func<GraphAsset> getCurrentGraph, Func<GraphEditorContext> createContext)
    {
        _getCurrentGraph = getCurrentGraph;
        _createContext = createContext;

        _root = new VBoxContainer
        {
            Visible = false,
            CustomMinimumSize = new Vector2(0, 260),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _root.AddThemeConstantOverride("separation", 4);

        _header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 34) };
        _root.AddChild(_header);

        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        body.AddThemeConstantOverride("separation", 6);
        _root.AddChild(body);

        _trackList = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(170, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        body.AddChild(_trackList);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(360, 180)
        };
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
        _canvas.PlayheadChanged += value =>
        {
            _playhead = value;
            RefreshHeader();
        };
        _canvas.Changed += () =>
        {
            MarkChanged();
            RefreshInspector();
        };
        scroll.AddChild(_canvas);

        var inspectorScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            CustomMinimumSize = new Vector2(300, 180),
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

        _timeline = timeline;
        _timeline.NormalizeTimelineData();
        _playhead = Mathf.Clamp(_playhead, 0f, _timeline.Duration);
        _root.Visible = true;
        RefreshAll();
    }

    public void Clear()
    {
        _timeline = null;
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
            Text = "Timeline",
            CustomMinimumSize = new Vector2(90, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        _header.AddChild(new Label { Text = "Duration", VerticalAlignment = VerticalAlignment.Center });
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

            _timeline.Duration = (float)value;
            _timeline.NormalizeTimelineData();
            _playhead = Mathf.Clamp(_playhead, 0f, _timeline.Duration);
            MarkChanged();
            RefreshTrackList();
            RefreshInspector();
            RefreshCanvas();
        };
        _header.AddChild(duration);

        _header.AddChild(new Label { Text = "Zoom", VerticalAlignment = VerticalAlignment.Center });
        var zoom = new SpinBox
        {
            MinValue = 0.25,
            MaxValue = 8,
            Step = 0.25,
            Value = _zoom,
            CustomMinimumSize = new Vector2(76, 0)
        };
        zoom.ValueChanged += value =>
        {
            _zoom = (float)value;
            RefreshCanvas();
        };
        _header.AddChild(zoom);

        _header.AddChild(new Label { Text = "Time", VerticalAlignment = VerticalAlignment.Center });
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
        _header.AddChild(playhead);

        var snap = new CheckBox
        {
            Text = "Snap",
            ButtonPressed = _snap
        };
        snap.Toggled += value =>
        {
            _snap = value;
            RefreshCanvas();
        };
        _header.AddChild(snap);

        AddHeaderButton("Add Track", AddTrack);
        AddHeaderButton("Add Clip", AddClip);
        AddHeaderButton("Add Marker", AddMarker);
    }

    private void RefreshTrackList()
    {
        ClearChildren(_trackList);
        if (_timeline == null)
            return;

        _trackList.AddChild(new Label { Text = "Tracks" });
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
        var row = new HBoxContainer();
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

        _timeline.Tracks.Add(new FlowTimelineTrack { Name = $"Track {_timeline.Tracks.Count + 1}" });
        _selectedTrackIndex = _timeline.Tracks.Count - 1;
        _selectedClipIndex = -1;
        _selectedMarkerIndex = -1;
        MarkChanged();
        RefreshAll();
    }

    private void AddClip()
    {
        if (_timeline == null)
            return;

        if (_timeline.Tracks.Count == 0)
            _timeline.Tracks.Add(new FlowTimelineTrack { Name = "Track 1" });

        int trackIndex = _selectedTrackIndex >= 0 && _selectedTrackIndex < _timeline.Tracks.Count
            ? _selectedTrackIndex
            : 0;

        var clip = new FlowTimelineClip
        {
            Name = "Clip",
            StartTime = SnapTime(_playhead),
            Duration = Mathf.Min(0.2f, Mathf.Max(0.01f, _timeline.Duration - _playhead))
        };
        _timeline.Tracks[trackIndex].Clips.Add(clip);
        _selectedTrackIndex = trackIndex;
        _selectedClipIndex = _timeline.Tracks[trackIndex].Clips.Count - 1;
        _selectedMarkerIndex = -1;
        MarkChanged();
        RefreshAll();
    }

    private void AddMarker()
    {
        if (_timeline == null)
            return;

        _timeline.Markers.Add(new FlowTimelineMarker
        {
            Label = $"Marker {_timeline.Markers.Count + 1}",
            Time = SnapTime(_playhead)
        });
        _selectedMarkerIndex = _timeline.Markers.Count - 1;
        _selectedTrackIndex = -1;
        _selectedClipIndex = -1;
        MarkChanged();
        RefreshAll();
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

        track.Clips.RemoveAt(_selectedClipIndex);
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
        _getCurrentGraph()?.MarkDirty();
        _canvas.QueueRedraw();
    }

    private float SnapTime(float time)
    {
        if (!_snap)
            return Mathf.Clamp(time, 0f, _timeline?.Duration ?? 0f);

        return Mathf.Clamp(Mathf.Round(time / 0.05f) * 0.05f, 0f, _timeline?.Duration ?? 0f);
    }

    private void AddHeaderButton(string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        _header.AddChild(button);
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
