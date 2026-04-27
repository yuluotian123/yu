#if TOOLS
using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

[Tool]
public partial class GraphCanvasEditorWindow : Window
{
    private GraphEdit _graphEdit;
    private GraphAsset _currentGraph;
    private VBoxContainer _mainContainer;
    private List<Dictionary> _clipboard = new();
    private GraphConnection _selectedConnection = null;
    private GraphConnection _hoveredConnection = null;
    private System.Collections.Generic.Dictionary<string, Label> _connectionLabels = new();

    // ── 子图导航栈 ────────────────────────────────────────────────────────────
    /// <summary>
    /// 导航历史栈。每个元素为 (GraphAsset 图资源, 节点在父图中的 Id)。
    /// 栈顶为当前正在编辑的图，栈底为根图。
    /// </summary>
    private Stack<(GraphAsset graph, string label)> _graphStack = new();
    private HBoxContainer _breadcrumbBar;
    private HBoxContainer _toolbar;

    public EditorUndoRedoManager _undoRedo { get; set; }

    public override void _Ready()
    {
        Title = "GraphCanvas 编辑器";
        CloseRequested += () => {OnSave(); Hide();};
        CreateToolbar();
        CreateGraphEdit();
    }

    private void CreateToolbar()
    {
        _mainContainer = new VBoxContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1
        };
        AddChild(_mainContainer);

        // ── 面包屑导航栏 ──────────────────────────────────────────────────────
        _breadcrumbBar = new HBoxContainer();
        _breadcrumbBar.CustomMinimumSize = new Vector2(0, 28);
        _breadcrumbBar.AddThemeConstantOverride("separation", 4);
        // 默认隐藏，进入子图时显示
        _breadcrumbBar.Visible = false;
        _mainContainer.AddChild(_breadcrumbBar);

        // ── 主工具栏 ──────────────────────────────────────────────────────────
        _toolbar = new HBoxContainer();
        _toolbar.CustomMinimumSize = new Vector2(0, 40);
        _mainContainer.AddChild(_toolbar);

        var saveBtn = new Button { Text = "保存 (Ctrl+S)" };
        saveBtn.Pressed += OnSave;
        _toolbar.AddChild(saveBtn);

        var clearBtn = new Button { Text = "清空" };
        clearBtn.Pressed += OnClear;
        _toolbar.AddChild(clearBtn);

        var blackboardBtn = new Button { Text = "Blackboard" };
        blackboardBtn.Pressed += OpenBlackboardWindow;
        _toolbar.AddChild(blackboardBtn);

        _toolbar.AddChild(new VSeparator());
        _toolbar.AddChild(new Label { Text = "右键添加节点" });

        _toolbar.AddChild(new VSeparator());
        _toolbar.AddChild(new Label { Text = "撤回 (ctrl+Z)" });
    }

    private void CreateGraphEdit()
    {
        _graphEdit = new GraphEdit
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            RightDisconnects = true,
            ShowZoomLabel = true
        };
        _mainContainer.AddChild(_graphEdit);

        _graphEdit.ConnectionRequest += OnConnectionRequest;
        _graphEdit.DisconnectionRequest += OnDisconnectionRequest;
        _graphEdit.PopupRequest += OnPopupRequest;
        _graphEdit.DeleteNodesRequest += OnDeleteNodes;
        _graphEdit.CopyNodesRequest += OnCopyNodes;
        _graphEdit.PasteNodesRequest += OnPasteNodes;
        _graphEdit.GuiInput += OnGraphEditInput;
    }

    public void LoadGraph(GraphAsset graph)
    {
        // 清空连接标签缓存和引用
        foreach (var label in _connectionLabels.Values)
            label.QueueFree();
        _connectionLabels.Clear();
        _selectedConnection = null;
        _hoveredConnection = null;

        _currentGraph = graph;
        Title = graph.GetEditorTitle();
        AddCustomToolbarControls();

        // 立即移除所有子节点
        foreach (var child in _graphEdit.GetChildren())
        {
            if (child is GraphNode or Label)
            {
                _graphEdit.RemoveChild(child);
                child.QueueFree();
            }
        }

        // 创建节点
        foreach (var nodeData in graph.Nodes)
            CreateNodeFromData(nodeData);

        // 延迟建立连接，等待节点端口初始化完成
        foreach (var conn in graph.Connections)
            CallDeferred(MethodName.DeferredConnectNode, conn.FromNode, conn.FromPort, conn.ToNode, conn.ToPort);
    }

    private void DeferredConnectNode(string fromNode, int fromPort, string toNode, int toPort)
    {
        _graphEdit.ConnectNode(fromNode, fromPort, toNode, toPort);
    }

    private GraphEditorContext CreateEditorContext()
    {
        GraphAsset rootGraph = _currentGraph;
        foreach (var item in _graphStack)
            rootGraph = item.graph;

        GraphBlackboardNode globalBlackboard = null;
        var blackboardNodes = FindBlackboardNodesInEditedScene();
        if (blackboardNodes.Count > 0)
            globalBlackboard = blackboardNodes[0];

        return new GraphEditorContext
        {
            CurrentGraph = _currentGraph,
            RootGraph = rootGraph,
            GraphEdit = _graphEdit,
            GlobalBlackboard = globalBlackboard
        };
    }

    private void AddCustomToolbarControls()
    {
        if (_currentGraph == null || _toolbar == null) return;

        // 先清理上一张图留下的自定义控件
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
        if (_currentGraph == null) return;

        if (!GraphBlackboardValidator.TryValidate(_currentGraph.BlackboardEntries, out string blackboardError))
        {
            var dialog = new AcceptDialog
            {
                Title = "Blackboard Error",
                DialogText = blackboardError
            };
            AddChild(dialog);
            dialog.PopupCentered();
            return;
        }

        // 构建 Id -> NodeData 索引，避免 O(n²) 查找
        var nodeDict = new System.Collections.Generic.Dictionary<string, GraphNodeData>();
        foreach (var nodeData in _currentGraph.Nodes)
            nodeDict[nodeData.Id] = nodeData;

        foreach (var child in _graphEdit.GetChildren())
        {
            if (child is GraphNode gn)
            {
                if (nodeDict.TryGetValue(gn.Name, out var nodeData))
                    nodeData.Position = gn.PositionOffset;
            }
        }

        // 序列化数据到 JSON 再保存
        _currentGraph.SaveToJson();
        ResourceSaver.Save(_currentGraph, _currentGraph.ResourcePath);
        GD.Print($"图已保存: {_currentGraph.ResourcePath}");
    }
    private void OnClear()
    {
        // 快照序列化为 JSON 字符串，绕开 Godot Variant 限制
        var snapshotNodesJson = GraphJsonHelper.SerializeList(_currentGraph.Nodes);
        var snapshotConnsJson = GraphJsonHelper.SerializeList(_currentGraph.Connections);

        if (_undoRedo != null)
        {
            _undoRedo.CreateAction("清空图");
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
        _currentGraph.Nodes.Clear();
        _currentGraph.Connections.Clear();
        foreach (var child in _graphEdit.GetChildren())
        {
            if (child is GraphNode gn)
                gn.QueueFree();
        }
    }
    private void DoRestoreSnapshot(string nodesJson, string connectionsJson)
    {
        DoClear();
        var nodes = GraphJsonHelper.DeserializeList<GraphNodeData>(nodesJson);
        var connections = GraphJsonHelper.DeserializeList<GraphConnection>(connectionsJson);
        foreach (var data in nodes)
        {
            _currentGraph.Nodes.Add(data);
            CreateNodeFromData(data);
        }
        foreach (var conn in connections)
        {
            _currentGraph.Connections.Add(conn);
            _graphEdit.ConnectNode(conn.FromNode, conn.FromPort, conn.ToNode, conn.ToPort);
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible || _currentGraph == null) return;
        UpdateConnectionLabels();
        if (Input.IsKeyPressed(Key.Delete) && _hoveredConnection != null)
        {
            DeleteHoveredConnection();
            GetViewport().SetInputAsHandled();
        }
    }
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed)
        {
            if (key.Keycode == Key.S && key.CtrlPressed)
            {
                OnSave();
                GetViewport().SetInputAsHandled();
            }
            else if (key.Keycode == Key.Z && key.CtrlPressed && !key.ShiftPressed)
            {
                _undoRedo?.GetHistoryUndoRedo((int)EditorUndoRedoManager.SpecialHistory.GlobalHistory).Undo();
                GetViewport().SetInputAsHandled();
            }
            else if ((key.Keycode == Key.Z && key.CtrlPressed && key.ShiftPressed) ||
                     (key.Keycode == Key.Y && key.CtrlPressed))
            {
                _undoRedo?.GetHistoryUndoRedo((int)EditorUndoRedoManager.SpecialHistory.GlobalHistory).Redo();
                GetViewport().SetInputAsHandled();
            }
        }
    }
    private void OnGraphEditInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            var conn = FindConnectionAtPosition(mb.Position);
            if (conn != null && mb.ButtonIndex == MouseButton.Right)
            {
                _selectedConnection = conn;
                var menuPos = Position + _graphEdit.Position + mb.Position;
                ShowConnectionMenu(menuPos);
                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventMouseMotion mm)
        {
            _hoveredConnection = FindConnectionAtPosition(mm.Position);
        }
    }
}
#endif
