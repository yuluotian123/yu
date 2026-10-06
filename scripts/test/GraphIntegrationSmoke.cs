#if TOOLS
using System;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using GameLogic;
using Godot;

[Tool]
public partial class GraphIntegrationSmoke : Node
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private)!.GetValue(target)!;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        GD.Print("PASS: " + message);
    }
    private async Task Frames(int count = 3)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private static EditorProperty FindProperty(Node root, string property)
    {
        if (root is EditorProperty editor && editor.GetEditedProperty().ToString() == property) return editor;
        foreach (Node child in root.GetChildren())
        {
            var found = FindProperty(child, property);
            if (found != null) return found;
        }
        return null;
    }

    private async Task CheckDetails(GraphPlugin plugin, GraphCanvasEditorWindow editor, FlowGraphAsset graph)
    {
        var details = Field<GraphSelectionInspectorPanel>(editor, "_selectionInspector");
        var native = Field<EditorInspector>(details, "_nativeInspector");
        details.ShowGraph();
        await Frames();
        var name = FindProperty(native, "Graph/Name");
        Check(name != null && FindProperty(native, "GraphJson") == null, "Graph settings expose editable defaults without raw graph JSON");
        name.EmitChanged("Graph/Name", "Details smoke graph");
        Check(graph.ResourceName == "Details smoke graph", "Native graph settings edit the actual graph");
        var history = plugin.GetUndoRedo().GetHistoryUndoRedo(plugin.GetUndoRedo().GetObjectHistoryId(details.InspectedObject));
        history.Undo();
        Check(graph.ResourceName == "", "Graph settings participate in native undo");
        history.Redo();

        var animation = new CharacterAnimationComponent3D { LocomotionGraph = new HfsmGraphAsset() };
        var profile = new CharacterMovementProfile();
        const string profilePath = "res://.godot/details-profile.tres";
        Check(ResourceSaver.Save(profile, profilePath) == Error.Ok, "External resource fixture saves");
        profile.TakeOverPath(profilePath);
        var movement = new CharacterMovementComponent3D { Profile = profile };
        var host = new GameObject3D { Name = "DetailsHost" };
        var sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();
        sceneRoot.AddChild(host);
        host.Owner = sceneRoot;
        // Non-tool scripts loaded by the editor are placeholders. Constructed C# instances
        // run _Ready, so add exported defaults afterward to avoid runtime initialization.
        host.Components.Add(animation);
        host.Components.Add(movement);
        plugin.OpenGraphEditor(animation.LocomotionGraph, animation);
        await Frames(5);
        var components = Field<GraphComponentPanel>(editor, "_componentPanel");
        Check(components.SelectedHost == host, "Components resolve the edited scene host");
        var tree = Field<Tree>(components, "_tree");
        TreeItem movementItem = null;
        for (var item = tree.GetRoot()?.GetFirstChild(); item != null; item = item.GetNext())
            if (item.HasMeta("inspector_target") && item.GetMeta("inspector_target").AsGodotObject() == movement) movementItem = item;
        Check(movementItem != null, "Component rows retain the serialized component instance");
        movementItem.Select(0);
        tree.EmitSignal(Tree.SignalName.ItemSelected);
        await Frames();
        Check(details.InspectedObject == movement, "Selecting a component opens its native Details");
        var disabled = FindProperty(native, "StartDisabled");
        Check(disabled != null && FindProperty(native, "BodySize") != null, "Native Details contain component properties and export groups");
        disabled.EmitChanged("StartDisabled", true);
        Check(movement.StartDisabled, "Component property editing changes the serialized default");
        history = plugin.GetUndoRedo().GetHistoryUndoRedo(plugin.GetUndoRedo().GetObjectHistoryId(movement));
        history.Undo();
        Check(!movement.StartDisabled, "Component edits support undo");
        history.Redo();
        Check(movement.StartDisabled, "Component edits support redo");

        native.EmitSignal(EditorInspector.SignalName.ResourceSelected, profile, "Profile");
        await Frames();
        Check(details.InspectedObject == profile, "Referenced resources open within the same Details panel");
        FindProperty(native, "MoveSpeed").EmitChanged("MoveSpeed", 456f);
        details.SaveChanges(false);
        var reloaded = ResourceLoader.Load(profilePath, cacheMode: ResourceLoader.CacheMode.Ignore);
        Check(Mathf.IsEqualApprox(reloaded.Get("MoveSpeed").AsSingle(), 456f), "Saving Details persists external resource edits");
        Field<Button>(details, "_backButton").EmitSignal(Button.SignalName.Pressed);
        await Frames();
        Check(details.InspectedObject == movement, "Resource navigation returns to its component");
        editor.GetType().GetMethod("ShowHostProperties", Private)!.Invoke(editor, null);
        await Frames();
        Check(details.InspectedObject == host && FindProperty(native, "position") != null, "Host Properties exposes the actual scene node and transform");

        using var packed = new PackedScene();
        Check(packed.Pack(host) == Error.Ok && ResourceSaver.Save(packed, "res://.godot/details-host.tscn") == Error.Ok, "Edited component defaults serialize into a scene");
        var saved = ResourceLoader.Load<PackedScene>("res://.godot/details-host.tscn", cacheMode: ResourceLoader.CacheMode.Ignore).Instantiate();
        Check(saved.Get("Components").AsGodotArray()[1].AsGodotObject().Get("StartDisabled").AsBool(), "Component default survives scene reload");
        saved.Free();
        host.QueueFree();
        await Frames();
        Check(details.InspectedObject is GraphSettingsInspectorObject, "Deleting the inspected host safely returns to graph settings");
        plugin.OpenGraphEditor(graph);
        await Frames();
        editor.SelectNode(graph.Nodes[0].Id);
        await Frames();
        Check(details.InspectedObject == null && !native.Visible, "Selecting a graph node restores its custom Details");
        details.ShowConnection(new FlowConnection());
        Check(!native.Visible, "Connection Details retain their custom editor");
    }
    private async Task CheckAnimationPicker(GraphPlugin plugin, GraphCanvasEditorWindow editor)
    {
        var graph = new HfsmGraphAsset();
        var state = new HfsmAnimationStateNodeData { StateName = "idle" };
        graph.Nodes.Add(state);
        var host = new GameObject3D { Name = "AnimationPickerHost" };
        Node sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();
        sceneRoot.AddChild(host); host.Owner = sceneRoot;
        var visual = new Node3D { Name = "CustomVisual" }; host.AddChild(visual); visual.Owner = sceneRoot;
        var frames = new SpriteFrames(); frames.RemoveAnimation("default");
        frames.AddAnimation("idle"); frames.AddAnimation("run");
        frames.SetAnimationSpeed("run", 12); frames.SetAnimationLoop("run", false);
        var sprite = new AnimatedSprite3D { Name = "Body", SpriteFrames = frames }; visual.AddChild(sprite); sprite.Owner = sceneRoot;
        var component = new CharacterAnimationComponent3D { SpritePath = new NodePath("CustomVisual/Body"), LocomotionGraph = graph };
        host.Components.Add(component);
        plugin.OpenGraphEditor(graph, component);
        await Frames(5);
        var canvas = Field<GraphEdit>(editor, "_graphEdit");
        var view = canvas.GetNode<GraphNode>(state.Id);
        var details = Field<GraphSelectionInspectorPanel>(editor, "_selectionInspector");
        details.ShowNode(view); await Frames();
        var inspector = Field<VBoxContainer>(details, "_content").GetChildren().OfType<GraphAnimationStateInspector>().Single();
        var context = Field<GraphEditorContext>(inspector, "_context");
        Check(GraphAnimationStateInspector.ResolveFrames(context, out _) == frames && component.AnimationInstance == null,
            "Animation picker resolves a custom SpritePath from serialized components without starting the runtime");
        Check(inspector.GetNode<FoldableContainer>("AdvancedPlayback").Folded,
            "Request key, behaviour binding and priority are grouped in collapsed advanced settings");
        Check(!view.FindChildren("*", "Label", true, false).Cast<Label>().Any(label => label.Text is "In" or "Out") &&
            view.GetInputPortCount() == 1 && view.GetOutputPortCount() == 1,
            "Animation state boxes hide In/Out text while retaining transition topology");
        var button = inspector.GetNode<Button>("AnimationPicker");
        Check(button.Text == "idle" && inspector.GetNode<Label>("AnimationInfo").Text.Contains("跟随状态名"),
            "Existing state-name fallback is clearly described in the picker");
        button.EmitSignal(Button.SignalName.Pressed); await Frames();
        var picker = Field<SearchablePopup<string>>(inspector, "_picker");
        Field<LineEdit>(picker, "_searchBox").Text = "run";
        Field<LineEdit>(picker, "_searchBox").EmitSignal(LineEdit.SignalName.TextChanged, "run"); await Frames();
        var tree = Field<Tree>(picker, "_tree");
        Check(tree.GetRoot().GetFirstChild().GetText(0).Contains("12 FPS") && tree.GetRoot().GetFirstChild().GetText(0).Contains("单次"),
            "Searchable animation choices include frame rate and loop information");
        tree.GetRoot().GetFirstChild().Select(0); tree.EmitSignal(Tree.SignalName.ItemActivated); await Frames();
        Check(state.AnimationName == "run" && button.Text == "run" && view.Title.Contains("run") &&
            view.FindChildren("*", "Label", true, false).Cast<Label>().Any(label => label.Text == "动画：run"),
            "Choosing an animation updates serialized data, Inspector and the state box summary");
        graph.SaveJsonFields();
        var restored = new HfsmGraphAsset { GraphJson = graph.GraphJson };
        Check(((HfsmAnimationStateNodeData)restored.FindNodeById(state.Id)).AnimationName == "run", "Chosen animation survives graph serialization");
        frames.RemoveAnimation("run"); inspector._Process(1);
        Check(state.AnimationName == "run" && inspector.GetNode<Label>("AnimationInfo").Text.Contains("找不到"),
            "Removed animations are reported without silently replacing the configured animation");
        var alternate = new SpriteFrames(); alternate.RemoveAnimation("default"); alternate.AddAnimation("attack");
        sprite.SpriteFrames = alternate; inspector._Process(1);
        button.EmitSignal(Button.SignalName.Pressed); await Frames();
        picker = Field<SearchablePopup<string>>(inspector, "_picker"); tree = Field<Tree>(picker, "_tree");
        Check(tree.GetRoot().GetFirstChild().GetText(0).StartsWith("attack"), "Reopening the picker reads a newly assigned SpriteFrames resource");
        // Swap the library after opening: a stale result must never write into another host's graph.
        sprite.SpriteFrames = frames;
        tree.GetRoot().GetFirstChild().Select(0); tree.EmitSignal(Tree.SignalName.ItemActivated); await Frames();
        Check(state.AnimationName == "run", "Stale picker results are rejected when the animation source changes");
        component.SpritePath = new NodePath("MissingSprite"); inspector._Process(1);
        Check(button.Disabled && inspector.GetNode<Label>("AnimationInfo").Text.Contains("SpritePath"),
            "Missing sprite bindings disable selection and explain where to fix them");
        component.SpritePath = new NodePath("CustomVisual/Body");
        // Test Godot's editor placeholder wrappers using a packed scene, not only C# instances.
        using var packed = new PackedScene();
        visual.Owner = host; sprite.Owner = host;
        Check(packed.Pack(host) == Error.Ok, "Animation picker scene fixture packs");
        var clone = packed.Instantiate(); sceneRoot.AddChild(clone);
        var cloneContext = new GraphEditorContext { CurrentGraph = graph, RootGraph = graph, ResolveHost = () => clone };
        Check(GraphAnimationStateInspector.ResolveFrames(cloneContext, out _) != null,
            "Animation discovery also supports scene-instantiated editor components");
        clone.QueueFree();
        using var playerScene = ResourceLoader.Load<PackedScene>("res://assets/scenes/player.tscn");
        var player = playerScene.Instantiate(); sceneRoot.AddChild(player);
        Check(GraphAnimationStateInspector.ResolveFrames(new GraphEditorContext { ResolveHost = () => player }, out _)?.HasAnimation("idle") == true,
            "The actual player scene exposes its animation library in editor placeholder mode");
        player.QueueFree();
        var flowNode = new FlowEntryNodeData();
        var flowView = GraphNodeViewBuilder.CreateNodeUI(flowNode, new GraphEditorContext { CurrentGraph = new FlowGraphAsset() });
        AddChild(flowView);
        Check(flowView.FindChildren("*", "Label", true, false).Cast<Label>().Any(label => label.Text == flowNode.GetOutputPortName(0)),
            "Flow graph nodes retain visible named output ports");
        flowView.QueueFree();
        host.QueueFree(); await Frames();
        inspector._Process(1);
        Check(button.Disabled, "Deleting the selected host safely disables the animation picker");
        plugin.OpenGraphEditor(new HfsmGraphAsset()); await Frames();
    }

    private async Task CheckTransitionEditing(GraphPlugin plugin, GraphCanvasEditorWindow editor)
    {
        var graph = new HfsmGraphAsset();
        var idle = new HfsmAnimationStateNodeData { StateName = "Idle", Position = new Vector2(20, 40) };
        var run = new HfsmAnimationStateNodeData { StateName = "Run", Position = new Vector2(290, 230) };
        var any = new HfsmAnyStateNodeData { Position = new Vector2(20, 320) };
        graph.Nodes.Add(idle); graph.Nodes.Add(run); graph.Nodes.Add(any);
        plugin.OpenGraphEditor(graph);
        await Frames(5);
        var canvas = Field<GraphEdit>(editor, "_graphEdit");
        canvas.Zoom = 0.65f;
        canvas.ScrollOffset = Vector2.Zero;
        await Frames();
        var idleView = canvas.GetNode<GraphNode>(idle.Id);
        var runView = canvas.GetNode<GraphNode>(run.Id);
        var service = Field<GraphConnectionEditorService>(editor, "_connectionEditor");
        var history = plugin.GetUndoRedo().GetHistoryUndoRedo((int)EditorUndoRedoManager.SpecialHistory.GlobalHistory);
        var begin = editor.GetType().GetMethod("BeginStateTransition", Private)!;
        var complete = editor.GetType().GetMethod("CompleteStateTransition", Private)!;
        editor._Input(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Right,
            Position = idleView.GetGlobalRect().GetCenter() });
        var menu = editor.GetChildren().OfType<PopupMenu>().LastOrDefault();
        Check(menu != null && menu.GetItemText(0).Contains("创建转换"), "Right-clicking the state body opens Make Transition");
        menu.EmitSignal(PopupMenu.SignalName.IdPressed, 0L);
        menu.Hide();
        await Frames();
        Check(Field<string>(editor, "_transitionSourceId") == idle.Id, "Make Transition arms the selected source state");
        Check(Field<Label>(service, "_hint").Text.Contains("Esc"), "Pending transition displays cancellation guidance");
        // An Any State node has no inputs and cannot be used as the target.
        complete.Invoke(editor, new object[] { any.Id });
        Check(graph.Connections.Count == 0 && Field<string>(editor, "_transitionSourceId") == idle.Id,
            "Invalid transition target leaves the gesture active without creating a connection");
        editor._Input(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left,
            Position = runView.GetGlobalRect().GetCenter() });
        Check(graph.Connections.Count == 1 && Field<string>(editor, "_transitionSourceId") == null,
            "Clicking the target body creates one transition and ends the gesture");
        await Frames();
        Check(service.SelectedConnection == graph.Connections[0], "New transition is selected for immediate condition editing");
        Check(!idleView.Selected && !runView.Selected, "Connection selection clears node selection");
        var details = Field<GraphSelectionInspectorPanel>(editor, "_selectionInspector");
        Check(Field<Label>(details, "_subtitleLabel").Text.Contains("Idle") && Field<Label>(details, "_subtitleLabel").Text.Contains("Run"),
            "Transition Details show readable source and target names");
        begin.Invoke(editor, new object[] { idle.Id, 0 });
        complete.Invoke(editor, new object[] { run.Id });
        Check(graph.Connections.Count == 1, "Duplicate transition gesture does not create a connection or undo action");
        editor._Input(new InputEventKey { Pressed = true, Keycode = Key.Escape });
        Check(Field<string>(editor, "_transitionSourceId") == null, "Escape cancels pending creation");
        history.Undo();
        Check(graph.Connections.Count == 0, "Undo after a duplicate request removes only the successful creation");
        history.Redo();
        Check(graph.Connections.Count == 1, "Redo restores the transition");
        var transition = (HfsmTransitionConnection)graph.Connections[0];
        transition.TransitionName = "奔跑转换与完整的条件配置测试名称超过三十二个字符后应该显示省略号并保留提示";
        transition.Priority = 17; transition.CompletionOnly = true; transition.UseMode = GraphConditionUseMode.Or;
        transition.Conditions.Add(new HfsmTriggerCondition { TriggerName = "StartRun" });
        graph.MarkDirty();
        string expected = GraphJsonHelper.Serialize(transition);
        // A wide custom condition editor must scroll instead of locking the dock width.
        details.ShowConnection(transition);
        var wideContent = new Control { CustomMinimumSize = new Vector2(1200, 24) };
        Field<VBoxContainer>(details, "_content").AddChild(wideContent);
        await Frames();
        var detailsSplit = Field<HSplitContainer>(editor, "_rightContentSplit");
        Check(detailsSplit.DraggingEnabled && detailsSplit.DraggerVisibility == SplitContainer.DraggerVisibilityEnum.Visible,
            "Connection Details have a visible enabled resize divider");
        Check(details.Root.GetCombinedMinimumSize().X <= 260 && Field<ScrollContainer>(details, "_customScroll").GetHScrollBar().Visible,
            "Wide connection properties scroll without inflating the Details minimum width");
        Vector2I originalWindowSize = GetTree().Root.Size;
        GetTree().Root.Size = new Vector2I(1800, 1000);
        await Frames();
        int savedOffset = detailsSplit.SplitOffsets[0];
        detailsSplit.SplitOffsets = new[] { 0 };
        await Frames();
        float narrowWidth = details.Root.Size.X;
        detailsSplit.SplitOffsets = new[] { detailsSplit.SplitOffsets[0] - 80 };
        await Frames();
        Check(details.Root.Size.X > narrowWidth + 50, "Connection Details can expand through the actual splitter layout");
        detailsSplit.SplitOffsets = new[] { detailsSplit.SplitOffsets[0] + 80 };
        await Frames();
        Check(details.Root.Size.X < narrowWidth + 2, "Connection Details can shrink even while wide conditions are displayed");
        Control dragArea = detailsSplit.GetDragAreaControls()[0];
        Vector2 dragStart = dragArea.GetGlobalRect().GetCenter();
        Vector2 dragEnd = dragStart - new Vector2(40, 0);
        var viewport = editor.GetViewport();
        viewport.PushInput(new InputEventMouseMotion { Position = dragStart, GlobalPosition = dragStart }, true);
        viewport.PushInput(new InputEventMouseButton { Position = dragStart, GlobalPosition = dragStart, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        await Frames(1);
        viewport.PushInput(new InputEventMouseMotion { Position = dragEnd, GlobalPosition = dragEnd, Relative = new Vector2(-40, 0), ButtonMask = MouseButtonMask.Left }, true);
        viewport.PushInput(new InputEventMouseButton { Position = dragEnd, GlobalPosition = dragEnd, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        await Frames();
        Check(details.Root.Size.X > narrowWidth + 30, "Dragging the actual divider immediately resizes connection Details");
        detailsSplit.SplitOffsets = new[] { savedOffset };
        GetTree().Root.Size = originalWindowSize;
        wideContent.QueueFree();
        await Frames();
        var labels = Field<System.Collections.Generic.Dictionary<string, Label>>(service, "_connectionLabels");
        Label label = labels.Values.Single();
        Check(label.Text.EndsWith("…") && label.Size.X <= 220 && label.TooltipText.Contains(transition.TransitionName), "Long connection labels stay compact and retain a full tooltip");
        label.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left });
        Check(service.SelectedConnection == transition && canvas.HasFocus(), "Clicking a connection label selects it and focuses the canvas");
        editor._Input(new InputEventKey { Pressed = true, Keycode = Key.Delete });
        Check(graph.Connections.Count == 0, "Delete removes the selected connection");
        history.Undo();
        Check(GraphJsonHelper.Serialize(graph.Connections[0]) == expected, "Undo restores the transition type, name, priority, completion flag and condition tree");
        history.Redo();
        Check(graph.Connections.Count == 0, "Redo deletes the same configured transition");
        history.Undo();
        await Frames();
        canvas.EmitSignal(GraphEdit.SignalName.DisconnectionRequest, new StringName(idle.Id), 0L, new StringName(run.Id), 0L);
        Check(graph.Connections.Count == 0, "Port disconnection uses the same undoable command");
        history.Undo();
        Check(GraphJsonHelper.Serialize(graph.Connections[0]) == expected, "Undo after port disconnection also preserves all metadata");
        await Frames();
        service.ClearSelection();
        labels.Values.Single().EmitSignal(Control.SignalName.MouseEntered);
        Check(!service.HandleShortcut(new InputEventKey { Pressed = true, Keycode = Key.Delete }) && graph.Connections.Count == 1,
            "Hovering a connection alone never authorizes Delete");
        var pointsMethod = service.GetType().GetMethod("GetConnectionPoints", Private)!;
        foreach (float zoom in new[] { 0.65f, 1f, 1.3f })
        {
            canvas.Zoom = zoom;
            canvas.ScrollOffset = new Vector2(31, 17);
            await Frames();
            var points = (Vector2[])pointsMethod.Invoke(service, new object[] { graph.Connections[0] })!;
            GraphConnectionEditorService.SampleLine(points, 0.65f, out Vector2 point, out Vector2 direction);
            Check(points.Length >= 2 && direction.Length() > 0.99f &&
                points[0].DistanceTo(idleView.Position + idleView.GetOutputPortPosition(0) * zoom) < 1f,
                "Arrow geometry follows the native connection after pan/zoom: " + zoom);
            Check(service.HandleGraphEditInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left,
                Position = point, GlobalPosition = canvas.GlobalPosition + point }) && service.SelectedConnection == graph.Connections[0],
                "Native curved line can be selected at zoom: " + zoom);
        }
        begin.Invoke(editor, new object[] { idle.Id, 0 });
        editor._Input(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Right });
        Check(Field<string>(editor, "_transitionSourceId") == null, "Right-click cancels the pending gesture");
        begin.Invoke(editor, new object[] { idle.Id, 0 });
        var other = new FlowGraphAsset();
        plugin.OpenGraphEditor(other);
        await Frames();
        Check(Field<string>(editor, "_transitionSourceId") == null && service.SelectedConnection == null,
            "Switching graphs clears both pending creation and connection selection");
        history.Redo();
        Check(graph.Connections.Count == 0 && other.Connections.Count == 0, "Connection undo history remains bound to its original graph after switching");
        history.Undo();
        Check(GraphJsonHelper.Serialize(graph.Connections[0]) == expected && other.Connections.Count == 0,
            "Cross-graph undo restores the original graph without touching the displayed graph");
    }

    public override void _Ready()
    {
        if (OS.GetEnvironment("YU_GRAPH_EDITOR_SMOKE") == "1") Callable.From(Run).CallDeferred();
    }
    private async void Run()
    {
        try
        {
            await Frames(15);
            var plugin = GraphPlugin.Instance;
            Check(plugin != null && plugin._HasMainScreen(), "Loaded plugin provides the Graph main screen");
            var editor = Field<GraphCanvasEditorWindow>(plugin, "_editorWindow");
            Check(editor.GetParent() == EditorInterface.Singleton.GetEditorMainScreen(), "Graph is embedded in the actual editor main screen");
            Check(editor is MarginContainer, "Graph host is a Control instead of a Window");
            var graph = new FlowGraphAsset();
            graph.Nodes.Add(new FlowEntryNodeData());
            plugin.OpenGraphEditor(graph);
            await Frames();
            Check(editor.IsVisibleInTree(), "Opening a graph displays the embedded workspace");
            var canvas = Field<GraphEdit>(editor, "_graphEdit");
            Check(canvas.Size.X >= 320 && canvas.Size.Y >= 200, "Embedded canvas has a usable layout");
            Check(canvas.GetNodeOrNull<GraphNode>(graph.Nodes[0].Id) != null, "Existing graph node views load");
            var other = new FlowGraphAsset();
            for (int i = 0; i < 8; i++) { plugin.OpenGraphEditor(other); plugin.OpenGraphEditor(graph); await Frames(1); }
            Check(Field<GraphAsset>(editor, "_currentGraph") == graph, "Repeated graph switching retains the active graph");
            var components = Field<GraphComponentPanel>(editor, "_componentPanel");
            var search = Field<LineEdit>(components, "_search");
            for (int i = 0; i < 8; i++) { search.Text = "movement"; search.Text = ""; await Frames(1); }
            Check(GodotObject.IsInstanceValid(Field<Tree>(components, "_tree")), "Component search survives repeated filtering");
            var animationGraph = new GameLogic.HfsmGraphAsset();
            animationGraph.BlackboardEntries.Add(new GraphBlackboardEntry { Key = "Speed" });
            var variables = new GraphAnimationVariablesPanel(() => animationGraph);
            AddChild(variables.Root);
            variables.SetHostAvailable(true);
            for (int i = 0; i < 8; i++) { variables.Refresh(); await Frames(1); }
            var variablesTree = Field<Tree>(variables, "_tree");
            Check(variablesTree.Columns == 4 && variablesTree.GetRoot().GetFirstChild().GetText(1).Length > 0, "Animation variable rows retain all four columns after refresh");
            var picker = new SearchablePopup<string>(new[] { "Beta", "Alpha" }, item => item, item => item.Substring(0, 1));
            picker.ShowBelow(canvas);
            await Frames();
            var pickerSearch = Field<LineEdit>(picker, "_searchBox");
            pickerSearch.Text = "Alpha";
            pickerSearch.Text = "";
            await Frames();
            var pickerTree = Field<Tree>(picker, "_tree");
            var group = pickerTree.GetRoot().GetFirstChild();
            group.GetFirstChild().Select(0);
            string chosen = null;
            picker.OnItemSelected += item => chosen = item;
            pickerTree.EmitSignal(Tree.SignalName.ItemActivated);
            Check(chosen == null, "Search activation waits until Tree finishes its input event");
            await Frames();
            Check(chosen == "Alpha", "Grouped search activates the correct result after filtering");
            variables.Root.QueueFree();
            await CheckDetails(plugin, editor, graph);
            await CheckAnimationPicker(plugin, editor);
            await CheckTimelineEditing(plugin, editor);
            await CheckTransitionEditing(plugin, editor);
            plugin.OpenGraphEditor(graph);
            await Frames();
            var flowActions = GraphCallableCatalog.Actions(GraphCallableUsage.Flow);
            var btActions = GraphCallableCatalog.Actions(GraphCallableUsage.BehaviorTree);
            Check(flowActions.Contains(typeof(PatrolAction)) && btActions.Contains(typeof(PatrolAction))
                && btActions.Contains(typeof(DebugLog)), "Shared menus include generic movement and debugging actions");
            Check(!GraphCallableCatalog.Actions(GraphCallableUsage.Timeline).Contains(typeof(PatrolAction))
                && !btActions.Contains(typeof(AbilitySlashVisualAction)), "Menus filter actions by their execution lifecycle");
            Check(GraphCallableCatalog.Name(typeof(PatrolAction)) == "往返巡逻（Patrol）"
                && GraphCallableCatalog.Category(typeof(PatrolAction)) == "移动", "Action names have Chinese translations and functional categories");
            foreach (var type in SubTypeCache.GetSubTypes<GraphActionBase>())
                Check(!string.IsNullOrWhiteSpace(type.GetCustomAttribute<GraphCallableAttribute>()?.ChineseName), type.Name + " has a Chinese name");
            var single = new GraphActionNodeData { Action = new PatrolAction() };
            var singleUi = single.CreateInspectorUI(new GraphEditorContext { CurrentGraph = graph });
            editor.AddChild(singleUi);
            var choose = (Button)singleUi.GetChild(0);
            Check(choose.Text.Contains("往返巡逻"), "Single action selector displays the same translated name");
            var actionPicker = new SearchablePopup<Type>(flowActions, GraphCallableCatalog.Name, GraphCallableCatalog.Category, GraphCallableCatalog.SearchText);
            actionPicker.ShowBelow(choose);
            var actionSearch = Field<LineEdit>(actionPicker, "_searchBox");
            var actionTree = Field<Tree>(actionPicker, "_tree");
            foreach (string query in new[] { "往返巡逻", "PatrolAction", "Patrol" })
            {
                actionSearch.Text = query; actionSearch.EmitSignal(LineEdit.SignalName.TextChanged, query); await Frames();
                var result = actionTree.GetRoot()?.GetFirstChild()?.GetFirstChild();
                Check(result != null && result.GetText(0).Contains("往返巡逻"), "Action search accepts " + query);
            }
            Field<PopupPanel>(actionPicker, "_popup").Hide(); singleUi.QueueFree(); await Frames();
            var characterGraph = new CharacterGraphAsset();
            var characterActions = GraphCallableCatalog.ActionsForGraph(characterGraph);
            Check(!characterActions.Contains(typeof(GraphComponentCallAction)) && !characterActions.Contains(typeof(GraphComponentGetAction))
                && !characterActions.Contains(typeof(GraphComponentSetAction)) && !characterActions.Contains(typeof(UseAbilityAction))
                && characterActions.Contains(typeof(WaitSecondsAction)), "Character action menu keeps shared actions and removes duplicate component and ability paths");
            Check(flowActions.Contains(typeof(UseAbilityAction)) && flowActions.Contains(typeof(GraphComponentCallAction)), "Other Flow graphs retain their component and ability actions");
            var allowed = characterGraph.GetAllowedNodeTypes();
            Check(allowed.Contains(nameof(FlowSequenceNodeData)) && allowed.Contains(nameof(FlowWaitEventNodeData))
                && allowed.Contains(nameof(FlowPublishEventNodeData)) && !allowed.Any(name => name.StartsWith("CharacterJump") || name.StartsWith("CharacterAddMovement")),
                "Character menu contains general flow nodes with no legacy movement types");
            var inputNode = new CharacterInputActionNodeData { ActionName = "jump", NegativeAction = "left", PositiveAction = "right" };
            var inputUi = inputNode.CreateInspectorUI(new GraphEditorContext { CurrentGraph = characterGraph }); editor.AddChild(inputUi);
            var inputMode = inputUi.GetNode<OptionButton>("TriggerMode");
            var inputFields = inputUi.GetNode<VBoxContainer>("ModeFields");
            foreach (var mode in Enum.GetValues<CharacterInputTriggerMode>())
            {
                inputMode.Select((int)mode); inputMode.EmitSignal(OptionButton.SignalName.ItemSelected, (long)mode); await Frames();
                bool axis = mode == CharacterInputTriggerMode.Axis1D;
                Check((inputFields.GetNodeOrNull<LineEdit>("NegativeAction") != null) == axis
                    && (inputFields.GetNodeOrNull<LineEdit>("ActionName") != null) != axis, "Input fields follow mode " + mode);
                var labels = inputFields.GetChildren().OfType<HBoxContainer>().SelectMany(row => row.GetChildren().OfType<Label>()).Select(label => label.Text).ToArray();
                Check(labels.Contains("缓存秒数") == (mode == CharacterInputTriggerMode.Pressed)
                    && labels.Contains("按住秒数") == (mode == CharacterInputTriggerMode.Held)
                    && labels.Contains("触发阈值") == axis, "Input timing and threshold fields follow mode " + mode);
            }
            Check(inputNode.ActionName == "jump" && inputNode.NegativeAction == "left", "Switching input modes preserves configured values");
            inputUi.QueueFree(); await Frames();
            var sequence = new FlowSequenceNodeData(); characterGraph.Nodes.Add(sequence);
            var sequenceContext = new GraphEditorContext { CurrentGraph = characterGraph };
            var sequenceView = GraphNodeViewBuilder.CreateNodeUI(sequence, sequenceContext); editor.AddChild(sequenceView);
            var sequenceUi = sequence.CreateInspectorUI(sequenceContext.WithGraphNode(sequence, sequenceView)); editor.AddChild(sequenceUi);
            ((SpinBox)sequenceUi.GetChild(1)).Value = 4; await Frames();
            Check(sequenceView.GetOutputPortCount() == 4, "Sequence output count updates its actual graph ports");
            sequenceUi.QueueFree(); sequenceView.QueueFree(); await Frames();
            var lifecycle = new CharacterLifecycleEventNodeData(); characterGraph.Nodes.Add(lifecycle);
            var lifecycleView = GraphNodeViewBuilder.CreateNodeUI(lifecycle, sequenceContext); editor.AddChild(lifecycleView);
            var lifecycleUi = (OptionButton)lifecycle.CreateInspectorUI(sequenceContext.WithGraphNode(lifecycle, lifecycleView)); editor.AddChild(lifecycleUi);
            characterGraph.Connections.Add(new CharacterGraphConnection { FromNode = lifecycle.Id, ToNode = sequence.Id });
            foreach (var eventType in Enum.GetValues<CharacterLifecycleEvent>())
            {
                lifecycleUi.Select((int)eventType); lifecycleUi.EmitSignal(OptionButton.SignalName.ItemSelected, (long)eventType); await Frames();
                Check(lifecycle.Event == eventType && lifecycleView.Title == "Event " + eventType
                    && lifecycleView.GetChildren().OfType<Label>().Single().Text == eventType.ToString(),
                    "Lifecycle selection updates both the canvas title and body: " + eventType);
                Check(lifecycleView.GetOutputPortCount() == 1 && characterGraph.GetOutgoingConnections(lifecycle.Id).Count == 1,
                    "Lifecycle refresh preserves ports and connections: " + eventType);
            }
            characterGraph.SaveJsonFields();
            var savedLifecycleGraph = new CharacterGraphAsset { GraphJson = characterGraph.GraphJson };
            Check(savedLifecycleGraph.TryLoadDocument(out _) && ((CharacterLifecycleEventNodeData)savedLifecycleGraph.FindNodeById(lifecycle.Id)).Event == CharacterLifecycleEvent.EndPlay,
                "Changed lifecycle event survives save and reload");
            lifecycleUi.EmitSignal(OptionButton.SignalName.ItemSelected, (long)CharacterLifecycleEvent.Update);
            lifecycleView.QueueFree(); lifecycleUi.QueueFree(); await Frames();
            Check(!GodotObject.IsInstanceValid(lifecycleView), "Pending lifecycle refresh tolerates deleting its node");
            GD.Print("GRAPH_INTEGRATION_SMOKE_OK");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
#endif
