#if TOOLS
using System;
using GameLogic;
using Godot;

public sealed partial class GraphTimelinePanel
{
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

        _inspector.AddChild(new Label { Text = "Select a clip or marker." });
    }

    private void BuildClipInspector(FlowTimelineClip clip)
    {
        var header = new HBoxContainer();
        header.AddChild(new Label
        {
            Text = "Clip",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = $"Clip Id: {clip.Id}"
        });
        var delete = new Button
        {
            Text = "Delete Clip",
            TooltipText = "Delete selected clip"
        };
        delete.Pressed += DeleteSelectedClip;
        header.AddChild(delete);
        _inspector.AddChild(header);

        _inspector.AddChild(SkillActionEditorHelper.BuildLineEditRow("Name", clip.Name, "Clip name", value =>
        {
            clip.Name = value;
            MarkChanged();
            RefreshCanvas();
        }));
        _inspector.AddChild(SkillActionEditorHelper.BuildCheckRow("Enabled", clip.Enabled, value =>
        {
            clip.Enabled = value;
            MarkChanged();
            RefreshCanvas();
        }));
        _inspector.AddChild(SkillActionEditorHelper.BuildSpinRow("Start", clip.StartTime, 0, 999999, 0.01, value =>
        {
            clip.StartTime = Mathf.Max(0f, (float)value);
            MarkChanged();
            RefreshCanvas();
        }));
        _inspector.AddChild(SkillActionEditorHelper.BuildSpinRow("Duration", clip.Duration, 0, 999999, 0.01, value =>
        {
            clip.Duration = Mathf.Max(0f, (float)value);
            MarkChanged();
            RefreshCanvas();
        }));

        var actionRow = new HBoxContainer();
        actionRow.AddChild(new Label
        {
            Text = clip.Action?.Description ?? "(no action)",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true
        });
        var selectAction = new Button { Text = "Select Action" };
        selectAction.Pressed += () => ShowActionSelector(selectAction, action =>
        {
            clip.Action = action;
            if (clip.Name == "Clip" || string.IsNullOrWhiteSpace(clip.Name))
                clip.Name = action?.Description ?? "Clip";
            MarkChanged();
            RefreshAll();
        });
        actionRow.AddChild(selectAction);
        _inspector.AddChild(actionRow);

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
            Text = "Marker",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        });
        var delete = new Button
        {
            Text = "Delete Marker",
            TooltipText = "Delete selected marker"
        };
        delete.Pressed += DeleteSelectedMarker;
        header.AddChild(delete);
        _inspector.AddChild(header);

        _inspector.AddChild(SkillActionEditorHelper.BuildLineEditRow("Label", marker.Label, "Marker label", value =>
        {
            marker.Label = value;
            MarkChanged();
            RefreshCanvas();
        }));
        _inspector.AddChild(SkillActionEditorHelper.BuildCheckRow("Enabled", marker.Enabled, value =>
        {
            marker.Enabled = value;
            MarkChanged();
            RefreshCanvas();
        }));
        _inspector.AddChild(SkillActionEditorHelper.BuildSpinRow("Time", marker.Time, 0, _timeline.Duration, 0.01, value =>
        {
            marker.Time = Mathf.Clamp((float)value, 0f, _timeline.Duration);
            MarkChanged();
            RefreshCanvas();
        }));

        var actions = new ReorderableListControl<GraphActionBase>(
            items: marker.Actions,
            buildItemUi: action => action.CreateEditUI(_createContext()),
            getItemLabel: action => action.Description,
            availableTypes: SubTypeCache.GetSubTypes<GraphActionBase>(),
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

    private void ShowActionSelector(Control anchor, Action<GraphActionBase> onSelected)
    {
        var popup = new SearchablePopup<Type>(
            SubTypeCache.GetSubTypes<GraphActionBase>(),
            type => type.Name,
            type => type.Namespace);
        popup.OnItemSelected += type =>
        {
            GraphActionBase action = (GraphActionBase)Activator.CreateInstance(type);
            onSelected?.Invoke(action);
        };
        popup.ShowBelow(anchor);
    }
}
#endif
