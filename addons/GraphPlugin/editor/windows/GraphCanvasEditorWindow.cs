#if TOOLS
using System.Collections.Generic;
using Godot;

[Tool]
public partial class GraphCanvasEditorWindow : Window
{
    private GraphEdit _graphEdit;
    private GraphAsset _currentGraph;
    private GraphEditorController _controller;
    private VBoxContainer _mainContainer;
    private readonly GraphClipboardService _clipboard = new();
    private GraphBlackboardPanel _blackboardPanel;
    private GraphSubGraphNavigator _subGraphNavigator;
    private GraphConnectionEditorService _connectionEditor;
    private GraphExplorerPanel _explorerPanel;
    private GraphTimelinePanel _timelinePanel;
    private GraphRuntimeDebugPanel _runtimeDebugPanel;
    private HBoxContainer _breadcrumbBar;
    private HBoxContainer _toolbar;
    private VSplitContainer _workArea;
    private CheckButton _runtimeDebugToggle;
    private double _runtimeDebugRefreshElapsed;
    private string _boundTimelineNodeId = string.Empty;

    public EditorUndoRedoManager _undoRedo { get; set; }

    public override void _Ready()
    {
        Title = "GraphCanvas Editor";
        CloseRequested += CloseGraphEditor;
        CreateToolbar();
        CreateGraphEdit();
    }

    private void CloseGraphEditor()
    {
        OnSave();
        _blackboardPanel?.Close();
        _explorerPanel?.Close();
        _timelinePanel?.Clear();
        if (_runtimeDebugToggle != null)
            _runtimeDebugToggle.ButtonPressed = false;
        else
            _runtimeDebugPanel?.SetVisible(false);
        ClearRuntimeDebugHighlights();
        Hide();
    }

    private void CreateToolbar()
    {
        _mainContainer = new VBoxContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1
        };
        AddChild(_mainContainer);

        _breadcrumbBar = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 28)
        };
        _breadcrumbBar.AddThemeConstantOverride("separation", 4);
        _breadcrumbBar.Visible = false;
        _mainContainer.AddChild(_breadcrumbBar);

        _toolbar = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 40)
        };
        _mainContainer.AddChild(_toolbar);

        var saveBtn = new Button { Text = "Save (Ctrl+S)" };
        saveBtn.Pressed += OnSave;
        _toolbar.AddChild(saveBtn);

        var clearBtn = new Button { Text = "Clear" };
        clearBtn.Pressed += OnClear;
        _toolbar.AddChild(clearBtn);

        var arrangeBtn = new Button { Text = "Arrange" };
        arrangeBtn.Pressed += () => _graphEdit?.ArrangeNodes();
        _toolbar.AddChild(arrangeBtn);

        var blackboardBtn = new Button { Text = "Blackboard" };
        blackboardBtn.Pressed += () => _blackboardPanel?.Open();
        _toolbar.AddChild(blackboardBtn);

        var explorerBtn = new Button { Text = "Explorer" };
        explorerBtn.Pressed += () => _explorerPanel?.Open();
        _toolbar.AddChild(explorerBtn);

        _runtimeDebugToggle = new CheckButton { Text = "Runtime Debug" };
        _runtimeDebugToggle.Toggled += OnRuntimeDebugToggled;
        _toolbar.AddChild(_runtimeDebugToggle);

        _toolbar.AddChild(new VSeparator());
        _toolbar.AddChild(new Label { Text = "Right-click to add nodes" });

        _toolbar.AddChild(new VSeparator());
        _toolbar.AddChild(new Label { Text = "Undo (Ctrl+Z)" });
    }

    private void CreateGraphEdit()
    {
        _workArea = new VSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _mainContainer.AddChild(_workArea);

        _graphEdit = new GraphEdit
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            RightDisconnects = true,
            ShowZoomLabel = true
        };
        _workArea.AddChild(_graphEdit);
        _controller = new GraphEditorController(_graphEdit);

        _graphEdit.ConnectionRequest += OnConnectionRequest;
        _graphEdit.DisconnectionRequest += OnDisconnectionRequest;
        _graphEdit.PopupRequest += OnPopupRequest;
        _graphEdit.DeleteNodesRequest += OnDeleteNodes;
        _graphEdit.CopyNodesRequest += OnCopyNodes;
        _graphEdit.PasteNodesRequest += OnPasteNodes;
        _graphEdit.GuiInput += OnGraphEditInput;

        CreateServices();
    }

    private void CreateServices()
    {
        _blackboardPanel = new GraphBlackboardPanel(
            this,
            () => _currentGraph,
            CreateEditorContext);

        _subGraphNavigator = new GraphSubGraphNavigator(
            this,
            _breadcrumbBar,
            () => _currentGraph,
            LoadGraph,
            OnSave);

        _connectionEditor = new GraphConnectionEditorService(
            this,
            _graphEdit,
            () => _currentGraph,
            CreateEditorContext,
            DeleteConnectionWithUndo);

        _explorerPanel = new GraphExplorerPanel(
            this,
            () => _currentGraph,
            () => _graphEdit);

        _timelinePanel = new GraphTimelinePanel(
            () => _currentGraph,
            CreateEditorContext);
        _workArea.AddChild(_timelinePanel.Root);

        _runtimeDebugPanel = new GraphRuntimeDebugPanel();
        _runtimeDebugPanel.OpenGraphRequested += OnRuntimeDebugOpenGraph;
        _mainContainer.AddChild(_runtimeDebugPanel.Root);
    }

    public void LoadGraph(GraphAsset graph)
    {
        _currentGraph = graph;
        Title = graph.GetEditorTitle();
        AddCustomToolbarControls();
        _connectionEditor?.Reset();
        _explorerPanel?.RefreshIfOpen();
        _timelinePanel?.Clear();
        _boundTimelineNodeId = string.Empty;
        ClearRuntimeDebugHighlights();

        _controller.ClearGraphEdit();
        _controller.LoadGraph(
            graph,
            CreateNodeFromData,
            conn => CallDeferred(MethodName.DeferredConnectNode, conn.FromNode, conn.FromPort, conn.ToNode, conn.ToPort));
    }

    public void ResetNavigation()
    {
        _subGraphNavigator?.Reset();
    }

    private void DeferredConnectNode(string fromNode, int fromPort, string toNode, int toPort)
    {
        _graphEdit.ConnectNode(fromNode, fromPort, toNode, toPort);
    }

    private GraphEditorContext CreateEditorContext()
    {
        GraphAsset rootGraph = _subGraphNavigator?.GetRootGraph(_currentGraph) ?? _currentGraph;
        List<GraphAsset> parentGraphs = _subGraphNavigator?.GetParentGraphs() ?? new List<GraphAsset>();

        GraphBlackboardNode globalBlackboard = null;
        var blackboardNodes = GraphBlackboardPanel.FindBlackboardNodesInEditedScene();
        if (blackboardNodes.Count > 0)
            globalBlackboard = blackboardNodes[0];

        return new GraphEditorContext
        {
            CurrentGraph = _currentGraph,
            RootGraph = rootGraph,
            ParentGraphs = parentGraphs,
            GraphEdit = _graphEdit,
            GlobalBlackboard = globalBlackboard
        };
    }

    private void AddCustomToolbarControls()
    {
        if (_currentGraph == null || _toolbar == null)
            return;

        for (int i = _toolbar.GetChildCount() - 1; i >= 0; i--)
        {
            var child = _toolbar.GetChild(i);
            if (child.HasMeta("custom_control"))
                child.QueueFree();
        }

        var customControls = _currentGraph.GetCustomToolbarControls();
        foreach (var control in customControls)
        {
            control.SetMeta("custom_control", true);
            _toolbar.AddChild(control);
            _toolbar.MoveChild(control, 2);
        }
    }

    private void OnSave()
    {
        GraphSaveService.Save(this, _currentGraph, _graphEdit);
    }

    private void OnClear()
    {
        string snapshotNodesJson = GraphSnapshotService.CaptureNodes(_currentGraph);
        string snapshotConnsJson = GraphSnapshotService.CaptureConnections(_currentGraph);

        if (_undoRedo != null)
        {
            _undoRedo.CreateAction("Clear Graph");
            _undoRedo.AddDoMethod(this, MethodName.DoClear);
            _undoRedo.AddUndoMethod(this, MethodName.DoRestoreSnapshot, snapshotNodesJson, snapshotConnsJson);
            _undoRedo.CommitAction();
        }
        else
        {
            DoClear();
        }
    }

    private void DoClear()
    {
        GraphSnapshotService.Clear(_currentGraph, _controller, _connectionEditor);
    }

    private void DoRestoreSnapshot(string nodesJson, string connectionsJson)
    {
        GraphSnapshotService.Restore(
            _currentGraph,
            _graphEdit,
            _controller,
            _connectionEditor,
            CreateNodeFromData,
            nodesJson,
            connectionsJson);
    }

    public override void _Process(double delta)
    {
        if (!Visible || _currentGraph == null)
            return;

        _connectionEditor?.UpdateConnectionLabels();
        UpdateTimelinePanelSelection();
        UpdateRuntimeDebug(delta);
        if (Input.IsKeyPressed(Key.Delete) && _connectionEditor?.DeleteHoveredConnection() == true)
        {
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (GraphEditorShortcutService.Handle(@event, _undoRedo, OnSave))
            GetViewport().SetInputAsHandled();
    }

    private void OnGraphEditInput(InputEvent @event)
    {
        if (_connectionEditor?.HandleGraphEditInput(@event, Position) == true)
            GetViewport().SetInputAsHandled();
    }

    private void UpdateTimelinePanelSelection()
    {
        FlowTimelineNodeData selectedTimeline = GetSingleSelectedTimelineNode(out string selectedNodeId);
        if (selectedTimeline == null)
        {
            if (!string.IsNullOrEmpty(_boundTimelineNodeId))
            {
                _timelinePanel?.Clear();
                _boundTimelineNodeId = string.Empty;
            }
            return;
        }

        if (_boundTimelineNodeId == selectedNodeId)
            return;

        _boundTimelineNodeId = selectedNodeId;
        _timelinePanel?.Bind(selectedTimeline);
    }

    private FlowTimelineNodeData GetSingleSelectedTimelineNode(out string selectedNodeId)
    {
        selectedNodeId = string.Empty;
        GraphNode selectedGraphNode = null;
        int selectedCount = 0;

        foreach (Node child in _graphEdit.GetChildren())
        {
            if (child is not GraphNode graphNode || !graphNode.Selected)
                continue;

            selectedCount++;
            selectedGraphNode = graphNode;
            if (selectedCount > 1)
                return null;
        }

        if (selectedGraphNode == null || _currentGraph == null)
            return null;

        selectedNodeId = selectedGraphNode.Name.ToString();
        return _currentGraph.FindNodeById(selectedNodeId) as FlowTimelineNodeData;
    }

    private void OnRuntimeDebugToggled(bool enabled)
    {
        _runtimeDebugPanel?.SetVisible(enabled);
        _runtimeDebugRefreshElapsed = 999d;
        if (!enabled)
            ClearRuntimeDebugHighlights();
    }

    private void OnRuntimeDebugOpenGraph(GraphAsset graph)
    {
        if (graph == null)
            return;

        OnSave();
        ResetNavigation();
        LoadGraph(graph);
    }

    private void UpdateRuntimeDebug(double delta)
    {
        if (_runtimeDebugToggle?.ButtonPressed != true)
        {
            ClearRuntimeDebugHighlights();
            return;
        }

        _runtimeDebugRefreshElapsed += delta;
        if (_runtimeDebugRefreshElapsed < 0.1d)
            return;

        _runtimeDebugRefreshElapsed = 0d;
        UpdateRuntimeDebugSelectedOwnerPath();
        List<GraphRuntimeDebugSnapshot> snapshots = GraphRuntimeDebugRemoteStore.FindSnapshotsForSelectedOwner();
        GraphRuntimeDebugSnapshot activeSnapshot = GraphRuntimeDebugRemoteStore.ChooseBestSnapshot(snapshots, _currentGraph);
        _runtimeDebugPanel?.Refresh(
            GraphRuntimeDebugRemoteStore.SelectedOwnerPath,
            GraphRuntimeDebugRemoteStore.HasReceivedSnapshots,
            snapshots,
            activeSnapshot,
            _currentGraph);
        ApplyRuntimeDebugHighlights(activeSnapshot);
    }

    private void UpdateRuntimeDebugSelectedOwnerPath()
    {
        EditorSelection selection = EditorInterface.Singleton?.GetSelection();
        if (selection == null)
            return;

        Godot.Collections.Array<Node> selectedNodes = selection.GetSelectedNodes();
        if (selectedNodes == null || selectedNodes.Count == 0 || selectedNodes[0] == null)
            return;

        GraphRuntimeDebugRemoteStore.SetSelectedOwnerPath(GraphRuntimeDebugSnapshotFactory.GetObjectPath(selectedNodes[0]));
    }

    private void ApplyRuntimeDebugHighlights(GraphRuntimeDebugSnapshot snapshot)
    {
        var activeIds = new HashSet<string>();
        GraphRuntimeDebugScopeSnapshot scope = GraphRuntimeDebugUtil.FindScopeForGraph(snapshot?.Scopes, _currentGraph);
        if (scope != null)
        {
            for (int i = 0; i < scope.ActiveNodeIds.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(scope.ActiveNodeIds[i]))
                    activeIds.Add(scope.ActiveNodeIds[i]);
            }
        }

        foreach (Node child in _graphEdit.GetChildren())
        {
            if (child is not GraphNode graphNode)
                continue;

            bool active = activeIds.Contains(graphNode.Name.ToString());
            graphNode.Modulate = active ? new Color(1f, 0.9f, 0.45f) : Colors.White;
        }
    }

    private void ClearRuntimeDebugHighlights()
    {
        if (_graphEdit == null)
            return;

        foreach (Node child in _graphEdit.GetChildren())
        {
            if (child is GraphNode graphNode)
                graphNode.Modulate = Colors.White;
        }
    }
}
#endif
