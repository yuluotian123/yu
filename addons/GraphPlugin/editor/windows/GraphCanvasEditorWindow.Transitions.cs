#if TOOLS
using Godot;

/// <summary>状态图从节点整体发起转换，无需拖中细小的端口。</summary>
public partial class GraphCanvasEditorWindow
{
    private string _transitionSourceId;
    private int _transitionSourcePort;
    private string _transitionMessage;

    public override void _Input(InputEvent @event)
    {
        if (!IsVisibleInTree() || _currentGraph is not StateGraphAsset || _graphEdit == null) return;
        // Handle before GraphEdit: a target click must not start dragging the target node.
        if (_transitionSourceId != null)
        {
            if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape } ||
                @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
            {
                CancelStateTransition();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } targetClick)
            {
                if (!_graphEdit.GetGlobalRect().HasPoint(targetClick.Position)) { CancelStateTransition(); return; }
                GraphNode target = FindStateNodeAt(targetClick.Position);
                if (target != null) CompleteStateTransition(target.Name);
                else _transitionMessage = "请选择目标状态；Esc / 右键取消";
                GetViewport().SetInputAsHandled();
                return;
            }
        }
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } menuClick &&
            _graphEdit.GetGlobalRect().HasPoint(menuClick.Position))
        {
            GraphNode node = FindStateNodeAt(menuClick.Position);
            if (node == null) return;
            ShowStateTransitionMenu(node, _graphEdit.GetScreenPosition() + menuClick.Position - _graphEdit.GlobalPosition);
            GetViewport().SetInputAsHandled();
        }
        // Delete is consumed before GraphEdit's native DeleteNodesRequest.
        if (_graphEdit.HasFocus() && _connectionEditor?.HandleShortcut(@event) == true)
            GetViewport().SetInputAsHandled();
    }

    private GraphNode FindStateNodeAt(Vector2 position)
    {
        var children = _graphEdit.GetChildren();
        for (int i = children.Count - 1; i >= 0; i--)
            if (children[i] is GraphNode node && node.Visible && node.GetGlobalRect().HasPoint(position)) return node;
        return null;
    }

    private void ShowStateTransitionMenu(GraphNode node, Vector2 screenPosition)
    {
        GraphNodeData data = _currentGraph.FindNodeById(node.Name);
        if (data == null) return;
        var popup = new PopupMenu();
        for (int port = 0; port < data.GetOutputCount(); port++)
            popup.AddItem(data.GetOutputCount() == 1 ? "创建转换 / Make Transition" : $"创建转换：{data.GetOutputPortName(port)}", port);
        if (data.GetOutputCount() == 0)
        {
            popup.AddItem("此节点没有输出端口", 0);
            popup.SetItemDisabled(0, true);
        }
        GraphAsset graph = _currentGraph;
        string id = data.Id;
        popup.IdPressed += port => { if (_currentGraph == graph) BeginStateTransition(id, (int)port); };
        popup.PopupHide += popup.QueueFree;
        AddChild(popup);
        popup.Position = (Vector2I)screenPosition;
        popup.Popup();
    }

    private void BeginStateTransition(string sourceId, int port)
    {
        GraphNodeData source = _currentGraph?.FindNodeById(sourceId);
        if (_currentGraph is not StateGraphAsset || source == null || port < 0 || port >= source.GetOutputCount()) return;
        _transitionSourceId = sourceId;
        _transitionSourcePort = port;
        _transitionMessage = $"从 {source.GetDisplayName()} 创建转换：点击目标状态 · Esc / 右键取消";
        _connectionEditor?.ClearSelection();
        _graphEdit.GrabFocus();
        UpdateStateTransitionPreview();
    }

    private void CompleteStateTransition(string targetId)
    {
        GraphNodeData source = _currentGraph.FindNodeById(_transitionSourceId);
        GraphNodeData target = _currentGraph.FindNodeById(targetId);
        if (source == null || target == null) { CancelStateTransition(); return; }
        for (int port = 0; port < target.GetInputCount(); port++)
        {
            if (source.GetOutputPortType(_transitionSourcePort) != target.GetInputPortType(port)) continue;
            if (_currentGraph.HasConnection(source.Id, _transitionSourcePort, target.Id, port))
            {
                _transitionMessage = "这条转换已存在，请选择其他目标 · Esc / 右键取消";
                return;
            }
            int before = _currentGraph.Connections.Count;
            OnConnectionRequest(source.Id, _transitionSourcePort, target.Id, port);
            if (_currentGraph.Connections.Count > before) CancelStateTransition();
            else _transitionMessage = "端口连接数已达上限，请选择其他目标 · Esc / 右键取消";
            return;
        }
        _transitionMessage = "此节点没有可接收转换的输入端口 · Esc / 右键取消";
    }

    private void CancelStateTransition()
    {
        _transitionSourceId = null;
        _transitionMessage = null;
        _connectionEditor?.SetTransitionPreview(null, 0, Vector2.Zero);
    }

    private void UpdateStateTransitionPreview()
    {
        if (_transitionSourceId != null && _currentGraph.FindNodeById(_transitionSourceId) == null) CancelStateTransition();
        _connectionEditor?.SetTransitionPreview(_transitionSourceId, _transitionSourcePort, _graphEdit.GetLocalMousePosition(), _transitionMessage);
    }
}
#endif
