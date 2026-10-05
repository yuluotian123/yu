#if TOOLS
using System;
using System.Linq;
using System.Threading.Tasks;
using GameLogic;
using Godot;

public partial class GraphIntegrationSmoke
{
    private async Task CheckTimelineEditing(GraphPlugin plugin, GraphCanvasEditorWindow editor)
    {
        var clips = GraphCallableCatalog.TimelineActions(GraphTimelineActionKind.Clip);
        var markers = GraphCallableCatalog.TimelineActions(GraphTimelineActionKind.Marker);
        Check(clips.Contains(typeof(AbilityPlayAnimationAction)) && clips.Contains(typeof(AbilityApplyDashVelocityAction)) && clips.Contains(typeof(AbilitySlashClipAction)),
            "Clip menu contains animation requests, sustained dash velocity and continuous slash effects");
        Check(markers.Contains(typeof(AbilityCameraShakeAction)) && markers.Contains(typeof(SetBlackboardAction)) && markers.Contains(typeof(AbilitySlashVisualAction)) &&
            !markers.Intersect(clips).Any() && !clips.Contains(typeof(WaitSecondsAction)),
            "Marker menu contains one-shot actions and is distinct from Clip and task actions");
        var graph = new FlowGraphAsset { ActionDependencyMode = GraphActionDependencyMode.Reusable };
        var timeline = new FlowTimelineNodeData { Duration = 4 };
        timeline.Tracks.Add(new FlowTimelineTrack { Name = "动作", Clips = new() { new FlowTimelineClip { Name = "冲刺", StartTime = .5f, Duration = .5f, Action = new AbilityApplyDashVelocityAction { Speed = 1234 } } } });
        timeline.Markers.Add(new FlowTimelineMarker { Label = "触发", Time = .3f, Actions = new() { new SetBlackboardAction() } });
        graph.Nodes.Add(timeline);
        plugin.OpenGraphEditor(graph); editor.SelectNode(timeline.Id); await Frames(5);
        var panel = Field<GraphTimelinePanel>(editor, "_timelinePanel");
        Check(panel.Root.Visible, "Selecting Timeline opens its embedded editor");
        var canvas = Field<GraphTimelineCanvas>(panel, "_canvas");
        var history = plugin.GetUndoRedo().GetHistoryUndoRedo((int)EditorUndoRedoManager.SpecialHistory.GlobalHistory);
        FlowTimelineClip Clip() => timeline.Tracks[0].Clips[0];
        void Press(float x, float y) => canvas._GuiInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left, Position = new Vector2(x, y) });
        void Move(float x, float y, bool alt = false) => canvas._GuiInput(new InputEventMouseMotion { Position = new Vector2(x, y), ButtonMask = MouseButtonMask.Left, AltPressed = alt });
        void Release(float x, float y) => canvas._GuiInput(new InputEventMouseButton { Pressed = false, ButtonIndex = MouseButton.Left, Position = new Vector2(x, y) });
        float row = GraphTimelineCanvas.TracksTop + 18;
        Press(90, row); Move(125, row); Move(132, row); Release(132, row);
        Check(Mathf.IsEqualApprox(Clip().StartTime, .8f) && Mathf.IsEqualApprox(Clip().Duration, .5f), "Dragging a Clip moves it with the configured snap interval");
        history.Undo(); Check(Mathf.IsEqualApprox(Clip().StartTime, .5f), "One undo restores an entire multi-motion drag");
        history.Redo(); Check(Mathf.IsEqualApprox(Clip().StartTime, .8f), "Redo restores the completed drag");
        Press(133, row); Move(300, row); canvas._GuiInput(new InputEventKey { Pressed = true, Keycode = Key.Escape });
        Check(Mathf.IsEqualApprox(Clip().StartTime, .8f), "Escape restores pre-drag Clip timing");
        Press(113, row); Move(127, row); Release(127, row);
        Check(Mathf.IsEqualApprox(Clip().StartTime, .9f) && Mathf.IsEqualApprox(Clip().EndTime, 1.3f), "Dragging the left edge preserves the Clip end");
        Press(181, row); Move(209, row); Release(209, row);
        Check(Mathf.IsEqualApprox(Clip().EndTime, 1.5f), "Dragging the right edge changes Clip duration");
        Press(140, row); Move(10000, row); Release(10000, row);
        Check(Clip().EndTime <= timeline.Duration + .001f, "Clip movement cannot extend beyond the timeline end");
        history.Undo();
        Press(42, 38); Move(50, 38, true); Release(50, 38);
        Check(Mathf.IsEqualApprox(timeline.Markers[0].Time, .3f + 8f / 140), "Alt temporarily disables Marker time snapping");
        history.Undo(); Check(Mathf.IsEqualApprox(timeline.Markers[0].Time, .3f), "Marker movement supports undo");
        Press(140, row); Release(140, row);
        canvas._GuiInput(new InputEventKey { Pressed = true, Keycode = Key.Delete });
        Check(timeline.Tracks[0].Clips.Count == 0, "Delete removes the selected Clip");
        history.Undo();
        Check(((AbilityApplyDashVelocityAction)Clip().Action).Speed == 1234, "Undo restores the full deleted action configuration");
        var headerBefore = Field<HBoxContainer>(panel, "_header").GetChild(0);
        Press(70, 8); Move(210, 8); Release(210, 8);
        Check(Field<HBoxContainer>(panel, "_header").GetChild(0) == headerBefore, "Scrubbing does not recreate header controls");
        canvas._GuiInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.WheelUp, CtrlPressed = true, Position = new Vector2(100, row) });
        await Frames();
        Check(Field<float>(panel, "_zoom") > 1, "Ctrl-wheel zooms the timeline");
        Field<HBoxContainer>(panel, "_header").GetChildren().OfType<Button>().Single(button => button.Text == "适应").EmitSignal(Button.SignalName.Pressed);
        await Frames();
        Check(canvas.CustomMinimumSize.X <= Mathf.Max(360, Field<ScrollContainer>(panel, "_scroll").Size.X) + 2,
            "Fit scales the timeline to the available viewport");
        async Task ChooseTimelineAction(string search)
        {
            await Frames();
            var picker = Field<SearchablePopup<Type>>(panel, "_actionPicker");
            var input = Field<LineEdit>(picker, "_searchBox");
            input.Text = search; input.EmitSignal(LineEdit.SignalName.TextChanged, search); await Frames();
            var tree = Field<Tree>(picker, "_tree");
            var item = tree.GetRoot().GetFirstChild().GetFirstChild();
            item.Select(0); tree.EmitSignal(Tree.SignalName.ItemActivated); await Frames();
        }
        int beforeClips = timeline.Tracks[0].Clips.Count;
        Field<Button>(panel, "_addClipButton").EmitSignal(Button.SignalName.Pressed);
        await ChooseTimelineAction("持续刀光");
        Check(timeline.Tracks[0].Clips.Count == beforeClips + 1 && timeline.Tracks[0].Clips.Last().Action is AbilitySlashClipAction,
            "Add Clip opens a filtered picker and inserts the chosen action in one step");
        history.Undo(); Check(timeline.Tracks[0].Clips.Count == beforeClips, "Adding a Clip is undoable");
        history.Redo(); Check(timeline.Tracks[0].Clips.Last().Action is AbilitySlashClipAction, "Redo retains the selected Clip action");
        int beforeMarkers = timeline.Markers.Count;
        Field<Button>(panel, "_addMarkerButton").EmitSignal(Button.SignalName.Pressed);
        await ChooseTimelineAction("镜头震动");
        Check(timeline.Markers.Count == beforeMarkers + 1 && timeline.Markers.Last().Actions.Single() is AbilityCameraShakeAction,
            "Add Marker inserts a one-shot action through its own picker");
        graph.SaveJsonFields();
        var reloaded = new FlowGraphAsset { GraphJson = graph.GraphJson };
        Check(((FlowTimelineNodeData)reloaded.FindNodeById(timeline.Id)).Tracks[0].Clips.Last().Action is AbilitySlashClipAction,
            "New Clip action definitions survive save and reload");
        foreach (string path in new[] { "res://assets/abilities/attack_timeline.tres", "res://assets/abilities/dash_timeline.tres" })
        {
            var asset = ResourceLoader.Load<GraphAsset>(path);
            var result = new GraphValidationResult();
            foreach (var node in asset.Nodes.OfType<FlowTimelineNodeData>()) node.Validate(asset, result);
            Check(result.IsValid, "Existing ability timeline remains valid: " + path);
        }
        // Invalid placement is rejected even for graphs loaded from disk.
        Clip().Action = new SetBlackboardAction();
        timeline.Markers[0].Actions[0] = new AbilityPlayAnimationAction();
        var validation = new GraphValidationResult(); timeline.Validate(graph, validation);
        Check(!validation.IsValid, "Graph validation rejects Marker actions in Clips and Clip actions in Markers");
        Clip().Action = new AbilityApplyDashVelocityAction { Speed = 1234 };
        timeline.Markers[0].Actions[0] = new SetBlackboardAction();
        // Switching graphs mid-drag cancels instead of mutating the next graph's history.
        panel.Bind(timeline);
        float scale = 140 * Field<float>(panel, "_zoom");
        float initialStart = Clip().StartTime;
        Press((Clip().StartTime + Clip().Duration * .5f) * scale, row); Move((Clip().StartTime + .3f) * scale, row);
        plugin.OpenGraphEditor(new FlowGraphAsset()); await Frames();
        Check(Mathf.IsEqualApprox(Clip().StartTime, initialStart), "Changing graphs cancels an unfinished timeline drag");
        var switchedSnapshot = new FlowGraphAsset { GraphJson = graph.GraphJson };
        Check(Mathf.IsEqualApprox(((FlowTimelineNodeData)switchedSnapshot.FindNodeById(timeline.Id)).Tracks[0].Clips[0].StartTime, initialStart),
            "Graph switching saves the cancelled drag state rather than its temporary timing");
        CheckTimelineRuntime();
    }

    private void CheckTimelineRuntime()
    {
        var graph = new FlowGraphAsset { ActionDependencyMode = GraphActionDependencyMode.Reusable };
        var timeline = new FlowTimelineNodeData { Duration = 1 };
        timeline.Tracks.Add(new FlowTimelineTrack { Clips = new() { new FlowTimelineClip { StartTime = 0, Duration = .5f, Action = new AbilitySlashClipAction() } } });
        timeline.Markers.Add(new FlowTimelineMarker { Time = .1f, Actions = new() { new AddBlackboardNumberAction { Key = new GraphBlackboardKeyReference { Key = "Count" } } } });
        graph.Nodes.Add(timeline);
        var blackboard = new GraphBlackboardRuntime(); blackboard.PushLocal(graph); blackboard.SetValue("Count", 0f);
        var context = new GraphExecutionContext(graph, blackboard);
        var host = new GameObject2D(); var visual = new Node2D { Name = "VisualRoot" }; host.AddChild(visual); context.UserData.Add(host);
        var runtime = new FlowGraphRuntime(graph, context);
        timeline.Enter(runtime, context);
        var slash = visual.GetNode<Polygon2D>("AttackSlash");
        Check(slash.Visible, "Slash Clip starts its visual automatically");
        timeline.Tick(runtime, context, .2); timeline.Tick(runtime, context, .2);
        Check(blackboard.GetValue("Count", 0f) == 1, "A Marker fires exactly once across multiple timeline updates");
        timeline.Tick(runtime, context, .2);
        Check(!slash.Visible, "Slash Clip hides its visual when the clip ends");
        timeline.Exit(runtime, context);
        timeline.Enter(runtime, context); timeline.Exit(runtime, context);
        Check(!slash.Visible, "Cancelling a timeline also hides active Slash Clips");
        host.Free();
    }
}
#endif
