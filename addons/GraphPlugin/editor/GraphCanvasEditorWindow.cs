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

    // 鈹€鈹€ 瀛愬浘瀵艰埅鏍?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
    /// <summary>
    /// 瀵艰埅鍘嗗彶鏍堛€傛瘡涓厓绱犱负 (GraphAsset 鍥捐祫婧? 鑺傜偣鍦ㄧ埗鍥句腑鐨?Id)銆?
    /// 鏍堥《涓哄綋鍓嶆鍦ㄧ紪杈戠殑鍥撅紝鏍堝簳涓烘牴鍥俱€?
    /// </summary>
    private Stack<(GraphAsset graph, string label)> _graphStack = new();
    private HBoxContainer _breadcrumbBar;
    private HBoxContainer _toolbar;

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
        CloseBlackboardWindow();
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

        // 鈹€鈹€ 闈㈠寘灞戝鑸爮 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
        _breadcrumbBar = new HBoxContainer();
        _breadcrumbBar.CustomMinimumSize = new Vector2(0, 28);
        _breadcrumbBar.AddThemeConstantOverride("separation", 4);
        // 榛樿闅愯棌锛岃繘鍏ュ瓙鍥炬椂鏄剧ず
        _breadcrumbBar.Visible = false;
        _mainContainer.AddChild(_breadcrumbBar);

        // 鈹€鈹€ 涓诲伐鍏锋爮 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
        _toolbar = new HBoxContainer();
        _toolbar.CustomMinimumSize = new Vector2(0, 40);
        _mainContainer.AddChild(_toolbar);

        var saveBtn = new Button { Text = "淇濆瓨 (Ctrl+S)" };
        saveBtn.Pressed += OnSave;
        _toolbar.AddChild(saveBtn);

        var clearBtn = new Button { Text = "娓呯┖" };
        clearBtn.Pressed += OnClear;
        _toolbar.AddChild(clearBtn);

        var blackboardBtn = new Button { Text = "Blackboard" };
        blackboardBtn.Pressed += OpenBlackboardWindow;
        _toolbar.AddChild(blackboardBtn);

        _toolbar.AddChild(new VSeparator());
        _toolbar.AddChild(new Label { Text = "鍙抽敭娣诲姞鑺傜偣" });

        _toolbar.AddChild(new VSeparator());
        _toolbar.AddChild(new Label { Text = "鎾ゅ洖 (ctrl+Z)" });
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
        // 娓呯┖杩炴帴鏍囩缂撳瓨鍜屽紩鐢?
        foreach (var label in _connectionLabels.Values)
            label.QueueFree();
        _connectionLabels.Clear();
        _selectedConnection = null;
        _hoveredConnection = null;

        _currentGraph = graph;
        Title = graph.GetEditorTitle();
        AddCustomToolbarControls();

        // 绔嬪嵆绉婚櫎鎵€鏈夊瓙鑺傜偣
        foreach (var child in _graphEdit.GetChildren())
        {
            if (child is GraphNode or Label)
            {
                _graphEdit.RemoveChild(child);
                child.QueueFree();
            }
        }

        // 鍒涘缓鑺傜偣
        foreach (var nodeData in graph.Nodes)
            CreateNodeFromData(nodeData);

        // 寤惰繜寤虹珛杩炴帴锛岀瓑寰呰妭鐐圭鍙ｅ垵濮嬪寲瀹屾垚
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
        var parentGraphs = new List<GraphAsset>();
        foreach (var item in _graphStack)
        {
            parentGraphs.Add(item.graph);
            rootGraph = item.graph;
        }

        GraphBlackboardNode globalBlackboard = null;
        var blackboardNodes = FindBlackboardNodesInEditedScene();
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
        if (_currentGraph == null || _toolbar == null) return;

        // 鍏堟竻鐞嗕笂涓€寮犲浘鐣欎笅鐨勮嚜瀹氫箟鎺т欢
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

        // 鏋勫缓 Id -> NodeData 绱㈠紩锛岄伩鍏?O(n虏) 鏌ユ壘
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

        // 搴忓垪鍖栨暟鎹埌 JSON 鍐嶄繚瀛?
        _currentGraph.SaveToJson();
        ResourceSaver.Save(_currentGraph, _currentGraph.ResourcePath);
        GD.Print($"鍥惧凡淇濆瓨: {_currentGraph.ResourcePath}");
    }
    private void OnClear()
    {
        // 蹇収搴忓垪鍖栦负 JSON 瀛楃涓诧紝缁曞紑 Godot Variant 闄愬埗
        var snapshotNodesJson = GraphJsonHelper.SerializeList(_currentGraph.Nodes);
        var snapshotConnsJson = GraphJsonHelper.SerializeList(_currentGraph.Connections);

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
