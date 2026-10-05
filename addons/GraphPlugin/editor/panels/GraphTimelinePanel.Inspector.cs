#if TOOLS
using System;
using System.Linq;
using Godot;

public sealed partial class GraphTimelinePanel
{
    private SearchablePopup<Type> _actionPicker;
    private void RefreshInspector()
    {
        ClearChildren(_inspector);
        if (_timeline == null)
            return;

        if (TryGetSelectedClip(out FlowTimelineClip clip))
        {
            BuildClipInspector(clip);
            return;
        }

        if (TryGetSelectedMarker(out FlowTimelineMarker marker))
        {
            BuildMarkerInspector(marker);
            return;
        }

        _inspector.AddChild(new Label { Text = "选择 Clip 或 Marker 编辑。\n拖动两端调整片段长度。\nCtrl + 滚轮缩放，中键平移。\nAlt 暂停吸附，Esc 取消拖动。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
    }

    private void BuildClipInspector(FlowTimelineClip clip)
    {
        var header = new HBoxContainer();
        header.AddChild(new Label
        {
            Text = "Clip · 持续片段",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = $"Clip Id: {clip.Id}"
        });
        var delete = new Button
        {
            Text = "删除片段",
            TooltipText = "Delete selected clip"
        };
        delete.Pressed += DeleteSelectedClip;
        header.AddChild(delete);
        _inspector.AddChild(header);

        _inspector.AddChild(GraphEditorUi.BuildLineEditRow("名称", clip.Name, "Clip name", value =>
        {
            clip.Name = value;
            MarkChanged();
            RefreshCanvas();
        }));
        _inspector.AddChild(GraphEditorUi.BuildCheckRow("启用", clip.Enabled, value =>
        {
            clip.Enabled = value;
            MarkChanged();
            RefreshCanvas();
        }));
        _inspector.AddChild(GraphEditorUi.BuildSpinRow("开始时间", clip.StartTime, 0, 999999, 0.01, value =>
        {
            clip.StartTime = Mathf.Max(0f, (float)value);
            _timeline.Duration = Mathf.Max(_timeline.Duration, clip.EndTime);
            MarkChanged();
            RefreshCanvas();
        }));
        _inspector.AddChild(GraphEditorUi.BuildSpinRow("持续时间", clip.Duration, 0.01, 999999, 0.01, value =>
        {
            clip.Duration = Mathf.Max(0.01f, (float)value);
            _timeline.Duration = Mathf.Max(_timeline.Duration, clip.EndTime);
            MarkChanged();
            RefreshCanvas();
        }));

        var actionRow = new HBoxContainer();
        actionRow.AddChild(new Label
        {
            Text = clip.Action == null ? "（无动作）" : GraphCallableCatalog.ItemLabel(clip.Action, clip.Action.Description),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true
        });
        var selectAction = new Button { Text = "选择持续动作" };
        selectAction.Pressed += () => ShowActionSelector(selectAction, GraphTimelineActionKind.Clip, action =>
        {
            clip.Action = action;
            if (clip.Name == "Clip" || string.IsNullOrWhiteSpace(clip.Name))
                clip.Name = action == null ? "Clip" : GraphCallableCatalog.Name(action.GetType());
            MarkChanged();
            RefreshAll();
        });
        actionRow.AddChild(selectAction);
        _inspector.AddChild(actionRow);

        _inspector.AddChild(new Label { Text = "持续片段：开始 → 更新 → 结束 / 取消", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        if (clip.Action != null && !GraphTimelineActionRules.Supports(clip.Action.GetType(), GraphTimelineActionKind.Clip))
            _inspector.AddChild(new Label { Text = "此动作不支持 Clip，请更换为持续动作。", AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, .55f, .4f) });
        if (clip.Action != null)
        {
            Control actionUi = clip.Action.CreateEditUI(_createContext());
            if (actionUi != null)
            {
                actionUi.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                _inspector.AddChild(new HSeparator());
                _inspector.AddChild(actionUi);
            }
        }
    }

    private void BuildMarkerInspector(FlowTimelineMarker marker)
    {
        var header = new HBoxContainer();
        header.AddChild(new Label
        {
            Text = "Marker · 瞬时标记",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        });
        var delete = new Button
        {
            Text = "删除标记",
            TooltipText = "Delete selected marker"
        };
        delete.Pressed += DeleteSelectedMarker;
        header.AddChild(delete);
        _inspector.AddChild(header);

        _inspector.AddChild(GraphEditorUi.BuildLineEditRow("名称", marker.Label, "Marker label", value =>
        {
            marker.Label = value;
            MarkChanged();
            RefreshCanvas();
        }));
        _inspector.AddChild(GraphEditorUi.BuildCheckRow("启用", marker.Enabled, value =>
        {
            marker.Enabled = value;
            MarkChanged();
            RefreshCanvas();
        }));
        _inspector.AddChild(GraphEditorUi.BuildSpinRow("触发时间", marker.Time, 0, _timeline.Duration, 0.01, value =>
        {
            marker.Time = Mathf.Clamp((float)value, 0f, _timeline.Duration);
            MarkChanged();
            RefreshCanvas();
        }));

        _inspector.AddChild(new Label { Text = "瞬时标记：经过此时间点时，每个动作只触发一次。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var actions = new ReorderableListControl<GraphActionBase>(
            items: marker.Actions,
            buildItemUi: action => action.CreateEditUI(_createContext()),
            getItemLabel: action => action.Description,
            availableTypes: TimelineActionTypes(GraphTimelineActionKind.Marker),
            factory: type => (GraphActionBase)Activator.CreateInstance(type),
            defaultItemExpanded: false);
        actions.ListChanged += MarkChanged;
        _inspector.AddChild(actions.Build());
    }

    private bool TryGetSelectedClip(out FlowTimelineClip clip)
    {
        clip = null;
        if (_timeline == null ||
            _selectedTrackIndex < 0 ||
            _selectedTrackIndex >= _timeline.Tracks.Count)
        {
            return false;
        }

        FlowTimelineTrack track = _timeline.Tracks[_selectedTrackIndex];
        if (_selectedClipIndex < 0 || _selectedClipIndex >= track.Clips.Count)
            return false;

        clip = track.Clips[_selectedClipIndex];
        return clip != null;
    }

    private bool TryGetSelectedMarker(out FlowTimelineMarker marker)
    {
        marker = null;
        if (_timeline == null ||
            _selectedMarkerIndex < 0 ||
            _selectedMarkerIndex >= _timeline.Markers.Count)
        {
            return false;
        }

        marker = _timeline.Markers[_selectedMarkerIndex];
        return marker != null;
    }

    private void ShowActionSelector(Control anchor, GraphTimelineActionKind kind, Action<GraphActionBase> onSelected)
    {
        var popup = new SearchablePopup<Type>(
            TimelineActionTypes(kind),
            GraphCallableCatalog.Name,
            GraphCallableCatalog.Category,
            GraphCallableCatalog.SearchText);
        _actionPicker = popup;
        var timeline = _timeline;
        popup.OnItemSelected += type =>
        {
            if (_timeline != timeline || !GodotObject.IsInstanceValid(_root) || !_root.IsVisibleInTree()) return;
            GraphActionBase action = (GraphActionBase)Activator.CreateInstance(type);
            onSelected?.Invoke(action);
        };
        popup.ShowBelow(anchor);
    }

    private static System.Collections.Generic.IReadOnlyList<Type> TimelineActionTypes(GraphTimelineActionKind kind) => GraphCallableCatalog.TimelineActions(kind);
}
#endif
