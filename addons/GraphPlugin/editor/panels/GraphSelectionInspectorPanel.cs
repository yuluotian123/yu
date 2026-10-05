#if TOOLS
using System;
using System.Collections.Generic;
using Godot;

public sealed class GraphSelectionInspectorPanel
{
    private readonly Func<GraphAsset> _getCurrentGraph;
    private readonly Func<GraphEditorContext> _createContext;
    private readonly Func<GraphNode, GraphNodeData, Control> _buildExtraNodeInspector;
    private readonly MarginContainer _root;
    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly VBoxContainer _content;
    private readonly ScrollContainer _customScroll;
    private readonly EditorInspector _nativeInspector;
    private readonly Button _backButton;
    private readonly Dictionary<GraphAsset, GraphSettingsInspectorObject> _settings = new();
    private readonly HashSet<Resource> _resourcesToSave = new();
    private readonly Stack<(GodotObject target, string title, string context)> _navigation = new();
    private GodotObject _target;
    private Node _sceneContext;
    private string _objectTitle;
    private string _objectContext;
    private int _selectionVersion;
    private bool _sceneMayHaveEdits;
    public GodotObject InspectedObject => _nativeInspector.GetEditedObject();

    public GraphSelectionInspectorPanel(
        Func<GraphAsset> getCurrentGraph,
        Func<GraphEditorContext> createContext,
        Func<GraphNode, GraphNodeData, Control> buildExtraNodeInspector = null)
    {
        _getCurrentGraph = getCurrentGraph;
        _createContext = createContext;
        _buildExtraNodeInspector = buildExtraNodeInspector;

        _root = new MarginContainer
        {
            CustomMinimumSize = new Vector2(240f, 0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 0.45f,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _root.AddThemeConstantOverride("margin_left", 8);
        _root.AddThemeConstantOverride("margin_top", 8);
        _root.AddThemeConstantOverride("margin_right", 8);
        _root.AddThemeConstantOverride("margin_bottom", 8);

        var layout = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        layout.AddThemeConstantOverride("separation", 6);
        _root.AddChild(layout);

        _titleLabel = new Label
        {
            Text = "Details",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        layout.AddChild(_titleLabel);

        _subtitleLabel = new Label
        {
            Text = "No selection",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _subtitleLabel.AddThemeColorOverride("font_color", new Color(0.68f, 0.68f, 0.68f));
        layout.AddChild(_subtitleLabel);
        _backButton = new Button { Text = "← Back to owner", Visible = false };
        _backButton.Pressed += () =>
        {
            if (_navigation.Count == 0) return;
            var previous = _navigation.Pop();
            ShowObject(previous.target, previous.title, previous.context, false);
        };
        layout.AddChild(_backButton);
        layout.AddChild(new HSeparator());

        var scroll = new ScrollContainer
        {
            // Isolate wide condition editors from the dock minimum width.
            // Disabled scrolling propagates their minimum width into the splitter.
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        layout.AddChild(scroll);
        _customScroll = scroll;

        // Keep one inspector alive: Godot undo actions refer back to this control.
        _nativeInspector = new EditorInspector
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Visible = false
        };
        layout.AddChild(_nativeInspector);
        _nativeInspector.PropertyEdited += OnNativePropertyEdited;
        _nativeInspector.ResourceSelected += (resource, _) =>
        {
            int version = _selectionVersion;
            Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(_root) || _root.IsQueuedForDeletion()
                    || version != _selectionVersion || !GodotObject.IsInstanceValid(resource)) return;
                if (resource is GraphAsset graph)
                {
                    GraphPlugin.Instance?.OpenGraphEditor(graph);
                    return;
                }
                _navigation.Push((_target, _objectTitle, _objectContext));
                ShowObject(resource, string.IsNullOrEmpty(resource.ResourceName) ? resource.GetClass().ToString() : resource.ResourceName,
                    "Resource properties · changes are saved with the graph or scene", false);
            }).CallDeferred();
        };

        _content = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _content.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_content);

        Clear();
    }

    public Control Root => _root;

    public void Clear()
    {
        _titleLabel.Text = "Details";
        _subtitleLabel.Text = "No selection";
        ClearContent();

        var hint = new Label
        {
            Text = "Select a component, node or connection. Use Host Properties or Graph Settings to edit defaults.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        hint.AddThemeColorOverride("font_color", new Color(0.62f, 0.62f, 0.62f));
        _content.AddChild(hint);
    }

    public void ShowNode(GraphNode graphNode)
    {
        GraphAsset graph = _getCurrentGraph();
        if (graph == null || graphNode == null)
        {
            Clear();
            return;
        }

        string nodeId = graphNode.Name.ToString();
        GraphNodeData nodeData = graph.FindNodeById(nodeId);
        if (nodeData == null)
        {
            Clear();
            return;
        }

        _titleLabel.Text = nodeData.GetDisplayName();
        _subtitleLabel.Text = nodeData.NodeType;
        ClearContent();

        Control ui = nodeData.CreateInspectorUI(_createContext().WithGraphNode(nodeData, graphNode));
        if (ui == null)
        {
            Clear();
            return;
        }

        ui.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _content.AddChild(ui);

        Control extraUi = _buildExtraNodeInspector?.Invoke(graphNode, nodeData);
        if (extraUi == null)
            return;

        extraUi.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _content.AddChild(new HSeparator());
        _content.AddChild(extraUi);
    }

    public void ShowConnection(GraphConnection connection)
    {
        if (connection == null)
        {
            Clear();
            return;
        }

        _titleLabel.Text = connection.GetDisplayName();
        GraphAsset graph = _getCurrentGraph();
        _subtitleLabel.Text = $"{graph?.FindNodeById(connection.FromNode)?.GetDisplayName() ?? connection.FromNode} → {graph?.FindNodeById(connection.ToNode)?.GetDisplayName() ?? connection.ToNode}";
        ClearContent();

        Control ui = connection.CreateInspectorUI(_createContext().WithConnection(connection));
        if (ui == null)
        {
            Clear();
            return;
        }

        ui.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _content.AddChild(ui);
    }

    private void ClearContent()
    {
        _selectionVersion++;
        _nativeInspector.Edit(null);
        _nativeInspector.Hide();
        _customScroll.Show();
        _target = null;
        _navigation.Clear();
        _backButton.Hide();
        foreach (Node child in _content.GetChildren())
        {
            GraphEditorSignalCleanup.DisconnectSubtree(child);
            _content.RemoveChild(child);
            child.QueueFree();
        }
    }

    public void ShowGraph()
    {
        GraphAsset graph = _getCurrentGraph();
        if (!GodotObject.IsInstanceValid(graph)) { Clear(); return; }
        if (!_settings.TryGetValue(graph, out var settings))
        {
            settings = new GraphSettingsInspectorObject();
            settings.Bind(graph);
            _settings.Add(graph, settings);
        }
        ShowObject(settings, "Graph Settings", $"{graph.GraphType} · graph configuration");
    }

    public void ShowObject(GodotObject target, string title, string context, bool resetNavigation = true)
    {
        if (!GodotObject.IsInstanceValid(target)) { Clear(); return; }
        // Retain the back stack when drilling into a resource.
        var navigation = resetNavigation ? null : _navigation.ToArray();
        ClearContent();
        if (navigation != null)
            for (int i = navigation.Length - 1; i >= 0; i--) _navigation.Push(navigation[i]);
        _target = target;
        _objectTitle = title;
        _objectContext = context;
        var scene = EditorInterface.Singleton.GetEditedSceneRoot();
        if (_sceneContext != scene) _sceneMayHaveEdits = false;
        _sceneContext = scene;
        _titleLabel.Text = title;
        _subtitleLabel.Text = context;
        _backButton.Visible = _navigation.Count > 0;
        _customScroll.Hide();
        TrackResources(target, new HashSet<ulong>());
        _nativeInspector.Show();
        _nativeInspector.Edit(target);
    }

    private void OnNativePropertyEdited(string property)
    {
        if (!GodotObject.IsInstanceValid(_target)) return;
        TrackResources(_target, new HashSet<ulong>());
        if (_target is GraphSettingsInspectorObject settings) settings.Graph.MarkDirty();
        // Node/embedded component edits belong to the edited scene, not to a runtime clone.
        if (_target is Node || _target is Resource resource && resource.IsBuiltIn())
        {
            _sceneMayHaveEdits = true;
            if (GodotObject.IsInstanceValid(_sceneContext) && _sceneContext == EditorInterface.Singleton.GetEditedSceneRoot())
                EditorInterface.Singleton.MarkSceneAsUnsaved();
        }
    }

    private void TrackResources(GodotObject target, HashSet<ulong> visited)
    {
        if (!GodotObject.IsInstanceValid(target) || target is Script || !visited.Add(target.GetInstanceId())) return;
        if (target is Resource resource && !resource.IsBuiltIn() && !string.IsNullOrEmpty(resource.ResourcePath))
            _resourcesToSave.Add(resource);
        foreach (Godot.Collections.Dictionary property in target.GetPropertyList())
        {
            if (((PropertyUsageFlags)property["usage"].AsInt64() & PropertyUsageFlags.Storage) == 0) continue;
            Variant value = target.Get(property["name"].AsStringName());
            TrackValue(value, visited);
        }
    }

    private void TrackValue(Variant value, HashSet<ulong> visited)
    {
        if (value.VariantType == Variant.Type.Object && value.AsGodotObject() is Resource resource) TrackResources(resource, visited);
        else if (value.VariantType == Variant.Type.Array)
            foreach (Variant item in value.AsGodotArray()) TrackValue(item, visited);
        else if (value.VariantType == Variant.Type.Dictionary)
            foreach (Variant item in value.AsGodotDictionary().Values) TrackValue(item, visited);
    }

    public void SaveChanges(bool saveScene)
    {
        foreach (Resource resource in _resourcesToSave)
        {
            if (!GodotObject.IsInstanceValid(resource) || resource is GraphAsset || resource.IsBuiltIn()) continue;
            Error error = ResourceSaver.Save(resource, resource.ResourcePath);
            if (error != Error.Ok) GD.PushError($"[Graph Details] Failed to save {resource.ResourcePath}: {error}");
        }
        if (saveScene && _sceneMayHaveEdits && GodotObject.IsInstanceValid(_sceneContext)
            && _sceneContext == EditorInterface.Singleton.GetEditedSceneRoot() && !string.IsNullOrEmpty(_sceneContext.SceneFilePath))
        {
            if (EditorInterface.Singleton.SaveScene() == Error.Ok) _sceneMayHaveEdits = false;
        }
    }

    public void ValidateSelection()
    {
        if (_target == null) return;
        if (!GodotObject.IsInstanceValid(_target) || _target is Node node && !node.IsInsideTree()
            || _sceneContext != EditorInterface.Singleton.GetEditedSceneRoot()) ShowGraph();
    }

    public bool HandleInspectorUndo(InputEvent @event, EditorUndoRedoManager undoRedo)
    {
        if (!_nativeInspector.Visible || !GodotObject.IsInstanceValid(_target) || undoRedo == null
            || @event is not InputEventKey { Pressed: true, Echo: false } key || !(key.CtrlPressed || key.MetaPressed)) return false;
        if (key.Keycode != Key.Z && key.Keycode != Key.Y) return false;
        var history = undoRedo.GetHistoryUndoRedo(undoRedo.GetObjectHistoryId(_target));
        if (key.Keycode == Key.Z && !key.ShiftPressed) history.Undo(); else history.Redo();
        return true;
    }
}
#endif
