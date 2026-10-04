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
    private GraphSelectionInspectorPanel _selectionInspector;
    private GraphComponentPanel _componentPanel;
    private GraphAnimationVariablesPanel _animationVariablesPanel;
    private CheckButton _animationDebugButton;
    private OptionButton _dependencyModeOption;
    private HBoxContainer _breadcrumbBar;
    private HBoxContainer _toolbar;
    private HSplitContainer _contentSplit;
    private HBoxContainer _rightContentSplit;
    private HSplitContainer _animationWorkSplit;
    private VSplitContainer _workArea;
    private string _boundTimelineNodeId = string.Empty;
    private bool _animationDebugVisible = true;
    private bool _initialized;
    private bool _closeRequestedConnected;
    private bool _initialLayoutApplied;

    public EditorUndoRedoManager _undoRedo { get; set; }

    public override void _Ready()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (_initialized &&
            _controller != null &&
            GodotObject.IsInstanceValid(_mainContainer) &&
            GodotObject.IsInstanceValid(_graphEdit))
        {
            return;
        }

        ResetEditorUi();

        GraphEditorTranslationService.DisableAutoTranslate(this);
        Title = "GraphCanvas Editor";
        if (!_closeRequestedConnected)
        {
            CloseRequested += CloseGraphEditor;
            _closeRequestedConnected = true;
        }

        CreateToolbar();
        CreateGraphEdit();
        _initialized = _controller != null;
    }

    private void ResetEditorUi()
    {
        if (_mainContainer != null && GodotObject.IsInstanceValid(_mainContainer))
        {
            GraphEditorSignalCleanup.DisconnectSubtree(_mainContainer);
            RemoveChild(_mainContainer);
            _mainContainer.QueueFree();
        }

        _graphEdit = null;
        _controller = null;
        _mainContainer = null;
        _blackboardPanel = null;
        _subGraphNavigator = null;
        _connectionEditor = null;
        _explorerPanel = null;
        _timelinePanel = null;
        _selectionInspector = null;
        _componentPanel = null;
        _animationVariablesPanel = null;
        _animationDebugButton = null;
        _dependencyModeOption = null;
        _breadcrumbBar = null;
        _toolbar = null;
        _contentSplit = null;
        _rightContentSplit = null;
        _animationWorkSplit = null;
        _workArea = null;
        _boundTimelineNodeId = string.Empty;
        _initialLayoutApplied = false;
        _initialized = false;
    }

    private void CloseGraphEditor()
    {
        OnSave();
        _blackboardPanel?.Close();
        _explorerPanel?.Close();
        _timelinePanel?.Clear();
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

        var componentsBtn = new Button { Text = "Components" };
        componentsBtn.Pressed += () =>
        {
            if (_componentPanel?.Root == null)
                return;
            _componentPanel.Root.Visible = !_componentPanel.Root.Visible;
            if (_componentPanel.Root.Visible)
                _componentPanel.Refresh();
        };
        _toolbar.AddChild(componentsBtn);

        _animationDebugButton = new CheckButton
        {
            Text = "Anim Debug",
            ButtonPressed = true,
            TooltipText = "Show AnimInstance variables and animation-state diagnostics",
            Visible = false
        };
        _animationDebugButton.Toggled += OnAnimationDebugToggled;
        _toolbar.AddChild(_animationDebugButton);

        _toolbar.AddChild(new Label { Text = "Action Dependencies" });
        _dependencyModeOption = new OptionButton();
        _dependencyModeOption.AddItem("HostBound", (int)GraphActionDependencyMode.HostBound);
        _dependencyModeOption.AddItem("Reusable", (int)GraphActionDependencyMode.Reusable);
        _dependencyModeOption.ItemSelected += index =>
        {
            if (_currentGraph != null)
                _currentGraph.ActionDependencyMode = (GraphActionDependencyMode)index;
        };
        _toolbar.AddChild(_dependencyModeOption);

        var explorerBtn = new Button { Text = "Explorer" };
        explorerBtn.Pressed += () => _explorerPanel?.Open();
        _toolbar.AddChild(explorerBtn);

        _toolbar.AddChild(new VSeparator());
        _toolbar.AddChild(new Label { Text = "Right-click to add nodes" });

        _toolbar.AddChild(new VSeparator());
        _toolbar.AddChild(new Label { Text = "Undo (Ctrl+Z)" });
    }

    private void CreateGraphEdit()
    {
        _contentSplit = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            DraggerVisibility = SplitContainer.DraggerVisibilityEnum.Visible,
            DraggingEnabled = true,
            SplitOffsets = new[] { 190 }
        };
        // Make the resize handle obvious in the dark editor theme. Without a
        // visible bar it is easy to mistake the component browser for a fixed
        // width dock.
        _contentSplit.AddThemeConstantOverride("separation", 8);
        _contentSplit.AddThemeStyleboxOverride("split_bar_background", new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.19f, 0.24f, 0.95f),
            ContentMarginLeft = 2,
            ContentMarginRight = 2
        });
        _mainContainer.AddChild(_contentSplit);

        _componentPanel = new GraphComponentPanel(
            this,
            () => _currentGraph,
            CreateComponentCallNode,
            CreateComponentValueNode,
            () => _subGraphNavigator?.GetRootGraph(_currentGraph) ?? _currentGraph);
        _contentSplit.AddChild(_componentPanel.Root);

        _rightContentSplit = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _contentSplit.AddChild(_rightContentSplit);

        _animationVariablesPanel = new GraphAnimationVariablesPanel(() => _currentGraph);

        // Keep the AnimInstance panel adjustable like an editor dock. Long
        // variable names and provider paths can then be inspected without
        // permanently consuming graph canvas space.
        _animationWorkSplit = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            DraggerVisibility = SplitContainer.DraggerVisibilityEnum.Visible,
            DraggingEnabled = true
        };
        _animationWorkSplit.AddThemeConstantOverride("separation", 6);
        _animationWorkSplit.AddThemeStyleboxOverride("split_bar_background", new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.19f, 0.24f, 0.95f),
            ContentMarginLeft = 2,
            ContentMarginRight = 2
        });
        _animationWorkSplit.AddChild(_animationVariablesPanel.Root);

        _workArea = new VSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(500, 320)
        };
        _animationWorkSplit.AddChild(_workArea);
        _rightContentSplit.AddChild(_animationWorkSplit);

        _graphEdit = new GraphEdit
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(500, 280),
            Visible = true,
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
        _graphEdit.NodeSelected += OnNodeSelected;
        _graphEdit.NodeDeselected += OnNodeDeselected;

        CreateServices();
        // SplitOffsets are clamped while a Container has size zero. Apply the
        // compact default after the first layout pass so the divider really
        // starts at the requested position.
        CallDeferred(nameof(ApplyInitialLayout));
    }

    private void ApplyInitialLayout()
    {
        if (_initialLayoutApplied || _contentSplit == null ||
            !GodotObject.IsInstanceValid(_contentSplit))
            return;

        // The Window can be constructed while hidden. Wait for _Process once
        // it is visible instead of recursively deferring forever at size 0.
        if (!Visible || _contentSplit.Size.X <= 0)
            return;

        int leftMinimum = Mathf.CeilToInt(_componentPanel?.Root?.GetCombinedMinimumSize().X ?? 170f);
        int rightMinimum = 500;
        int availableMaximum = Mathf.Max(leftMinimum, Mathf.FloorToInt(_contentSplit.Size.X) - rightMinimum);
        _contentSplit.SplitOffsets = new[] { Mathf.Clamp(190, leftMinimum, availableMaximum) };
        _initialLayoutApplied = true;
    }

    private void CreateServices()
    {
        _blackboardPanel = new GraphBlackboardPanel(
            this,
            () => _currentGraph,
            CreateEditorContext,
            () => _componentPanel?.HasValidHost == true);

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
            DeleteConnectionWithUndo,
            connection => _selectionInspector?.ShowConnection(connection));

        _explorerPanel = new GraphExplorerPanel(
            this,
            () => _currentGraph,
            () => _graphEdit);

        _timelinePanel = new GraphTimelinePanel(
            () => _currentGraph,
            CreateEditorContext);
        _workArea.AddChild(_timelinePanel.Root);
        _selectionInspector = new GraphSelectionInspectorPanel(
            () => _currentGraph,
            CreateEditorContext,
            BuildExtraNodeInspector);
        _rightContentSplit.AddChild(_selectionInspector.Root);
    }

    public void LoadGraph(GraphAsset graph)
    {
        // Subgraph navigation does not carry an Inspector object. Preserve the
        // root graph's source so Component discovery remains bound to its host.
        LoadGraph(graph, _componentPanel?.Source);
    }

    public void SelectNode(string nodeId)
    {
        if (_graphEdit == null || string.IsNullOrWhiteSpace(nodeId))
            return;

        foreach (Node child in _graphEdit.GetChildren())
        {
            if (child is not GraphNode graphNode)
                continue;

            graphNode.Selected = string.Equals(
                graphNode.Name.ToString(), nodeId, System.StringComparison.Ordinal);
        }
    }

    public void LoadGraph(GraphAsset graph, GodotObject source)
    {
        if (graph == null)
            return;

        EnsureInitialized();
        if (_controller == null)
        {
            GD.PushWarning("[GraphCanvasEditorWindow] Window is not initialized yet.");
            return;
        }

        _componentPanel?.SetSource(source);
        _animationVariablesPanel?.SetSource(source);
        LoadGraphInitialized(graph);
    }

    private void LoadGraphInitialized(GraphAsset graph)
    {
        if (!graph.TryLoadDocument(out string loadError))
        {
            var dialog = new AcceptDialog { Title = "Graph Load Failed", DialogText = loadError };
            dialog.Confirmed += dialog.QueueFree;
            dialog.Canceled += dialog.QueueFree;
            AddChild(dialog);
            dialog.PopupCentered();
            return;
        }
        _currentGraph = graph;
        if (_dependencyModeOption != null)
        {
            _dependencyModeOption.Select((int)_currentGraph.ActionDependencyMode);
        }
        if (_currentGraph is GameLogic.CharacterGraphAsset characterGraph)
            characterGraph.MigrateMovementNodesToComponents();
        Title = _componentPanel?.Source is GameLogic.CharacterAnimationComponent2D
            ? "Animation Blueprint - Locomotion"
            : graph.GetEditorTitle();
        AddCustomToolbarControls();
        _connectionEditor?.Reset();
        _explorerPanel?.RefreshIfOpen();
        _timelinePanel?.Clear();
        _selectionInspector?.Clear();
        _componentPanel?.Refresh();
        _animationVariablesPanel?.SetHostAvailable(_componentPanel?.HasValidHost == true);
        _animationVariablesPanel?.Refresh();
        UpdateAnimationDebugUi();
        // Scene resources and C# tool objects can finish initializing one editor
        // frame after the graph window is opened. Refresh once more after that
        // frame so the Component tree can resolve the owning GameObject2D.
        CallDeferred(nameof(DeferredRefreshComponents));
        _boundTimelineNodeId = string.Empty;

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

    private void DeferredRefreshComponents()
    {
        if (_componentPanel != null && GodotObject.IsInstanceValid(_componentPanel.Root))
        {
            _componentPanel.Refresh();
            _animationVariablesPanel?.SetHostAvailable(_componentPanel.HasValidHost);
            _animationVariablesPanel?.RefreshIfChanged();
            UpdateAnimationDebugUi();
        }
    }

    private void OnAnimationDebugToggled(bool visible)
    {
        _animationDebugVisible = visible;
        _animationVariablesPanel?.SetDebugVisible(visible);
    }

    private void UpdateAnimationDebugUi()
    {
        // The inspector may pass either the animation component or the graph
        // resource itself. A locomotion HFSM with a real host in the edited
        // scene is still an Animation Blueprint context.
        bool isAnimationBlueprint = _componentPanel?.HasValidHost == true &&
            (_componentPanel.Source is GameLogic.CharacterAnimationComponent2D ||
             _currentGraph is GameLogic.HfsmGraphAsset);
        if (_animationDebugButton == null || !GodotObject.IsInstanceValid(_animationDebugButton))
        {
            _animationVariablesPanel?.SetDebugVisible(isAnimationBlueprint);
            return;
        }

        _animationDebugButton.Visible = isAnimationBlueprint;
        if (!isAnimationBlueprint)
        {
            _animationVariablesPanel?.SetDebugVisible(false);
            return;
        }

        _animationDebugButton.SetPressedNoSignal(_animationDebugVisible);
        _animationVariablesPanel?.SetDebugVisible(_animationDebugVisible);
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
            GlobalBlackboard = globalBlackboard,
            AvailableComponentTypes = _componentPanel?.GetAvailableDescriptors()
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
            GraphEditorTranslationService.DisableAutoTranslateRecursive(control);
            control.SetMeta("custom_control", true);
            _toolbar.AddChild(control);
            _toolbar.MoveChild(control, 2);
        }
    }

    private void OnSave()
    {
        _componentPanel?.RefreshIfSceneChanged();
        _animationVariablesPanel?.SetHostAvailable(_componentPanel?.HasValidHost == true);
        _animationVariablesPanel?.RefreshIfChanged();
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
        if (!Visible || _currentGraph == null || !GodotObject.IsInstanceValid(_currentGraph))
            return;

        ApplyInitialLayout();
        _componentPanel?.RefreshIfSceneChanged();
        _blackboardPanel?.RefreshIfHostChanged();
        _animationVariablesPanel?.SetHostAvailable(_componentPanel?.HasValidHost == true);
        _animationVariablesPanel?.RefreshIfChanged();
        UpdateAnimationDebugUi();
        _connectionEditor?.UpdateConnectionLabels();
        UpdateTimelinePanelSelection();
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

}
#endif
