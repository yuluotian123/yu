#if TOOLS
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// 连线编辑和连线标签服务。
/// </summary>
public sealed class GraphConnectionEditorService
{
    private readonly Window _owner;
    private readonly GraphEdit _graphEdit;
    private readonly Func<GraphAsset> _getCurrentGraph;
    private readonly Func<GraphEditorContext> _createContext;
    private readonly Action<GraphConnection> _deleteConnection;
    private readonly Dictionary<string, Label> _connectionLabels = new();
    private GraphConnection _selectedConnection;
    private GraphConnection _hoveredConnection;

    /// <summary>创建连线编辑服务。</summary>
    public GraphConnectionEditorService(
        Window owner,
        GraphEdit graphEdit,
        Func<GraphAsset> getCurrentGraph,
        Func<GraphEditorContext> createContext,
        Action<GraphConnection> deleteConnection)
    {
        _owner = owner;
        _graphEdit = graphEdit;
        _getCurrentGraph = getCurrentGraph;
        _createContext = createContext;
        _deleteConnection = deleteConnection;
    }

    /// <summary>重置连线标签和选择状态。</summary>
    public void Reset()
    {
        foreach (Label label in _connectionLabels.Values)
            label.QueueFree();
        _connectionLabels.Clear();
        _selectedConnection = null;
        _hoveredConnection = null;
    }

    /// <summary>处理 GraphEdit 输入。返回 true 表示事件已处理。</summary>
    public bool HandleGraphEditInput(InputEvent @event, Vector2 windowPosition)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            GraphConnection connection = FindConnectionAtPosition(mb.Position);
            if (connection != null && mb.ButtonIndex == MouseButton.Right)
            {
                _selectedConnection = connection;
                Vector2 menuPos = windowPosition + _graphEdit.Position + mb.Position;
                ShowConnectionMenu(menuPos);
                return true;
            }
        }
        else if (@event is InputEventMouseMotion mm)
        {
            _hoveredConnection = FindConnectionAtPosition(mm.Position);
        }

        return false;
    }

    /// <summary>更新所有连线标签。</summary>
    public void UpdateConnectionLabels()
    {
        GraphAsset graph = _getCurrentGraph();
        if (graph == null)
            return;

        var keysToRemove = new List<string>();
        foreach (string connectionKey in _connectionLabels.Keys)
        {
            bool found = false;
            foreach (GraphConnection connection in graph.Connections)
            {
                if (GetConnectionKey(connection) == connectionKey)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                keysToRemove.Add(connectionKey);
        }

        foreach (string key in keysToRemove)
        {
            Label label = _connectionLabels[key];
            label.QueueFree();
            _connectionLabels.Remove(key);
        }

        foreach (GraphConnection connection in graph.Connections)
        {
            string connectionKey = GetConnectionKey(connection);
            if (!_connectionLabels.ContainsKey(connectionKey))
                CreateConnectionLabel(connection);
            UpdateConnectionLabelPosition(connection);
        }
    }

    /// <summary>删除当前鼠标悬停的连线。</summary>
    public bool DeleteHoveredConnection()
    {
        if (_hoveredConnection == null)
            return false;

        GraphConnection connection = _hoveredConnection;
        _hoveredConnection = null;
        _deleteConnection?.Invoke(connection);
        return true;
    }

    private Vector2 GetPortPositionInLocal(GraphNode node, bool isOutput, int port)
    {
        Vector2 portOffset = isOutput ? node.GetOutputPortPosition(port) : node.GetInputPortPosition(port);
        Vector2 globalPos = node.GlobalPosition + portOffset * _graphEdit.Zoom;
        return globalPos - _graphEdit.GlobalPosition;
    }

    private GraphConnection FindConnectionAtPosition(Vector2 position)
    {
        GraphAsset graph = _getCurrentGraph();
        if (graph == null)
            return null;

        foreach (GraphConnection connection in graph.Connections)
        {
            var fromNode = _graphEdit.GetNodeOrNull<GraphNode>(connection.FromNode);
            var toNode = _graphEdit.GetNodeOrNull<GraphNode>(connection.ToNode);
            if (fromNode == null || toNode == null)
                continue;

            if (connection.FromPort < 0 || connection.FromPort >= fromNode.GetOutputPortCount())
                continue;

            if (connection.ToPort < 0 || connection.ToPort >= toNode.GetInputPortCount())
                continue;

            Vector2 fromPos = GetPortPositionInLocal(fromNode, true, connection.FromPort);
            Vector2 toPos = GetPortPositionInLocal(toNode, false, connection.ToPort);
            if (IsPointNearLine(position, fromPos, toPos, 10.0f))
                return connection;
        }

        return null;
    }

    private static bool IsPointNearLine(Vector2 point, Vector2 lineStart, Vector2 lineEnd, float threshold)
    {
        Vector2 lineVector = lineEnd - lineStart;
        Vector2 pointVector = point - lineStart;
        float lineLength = lineVector.Length();
        if (lineLength == 0)
            return point.DistanceTo(lineStart) <= threshold;

        float t = Mathf.Clamp(pointVector.Dot(lineVector) / (lineLength * lineLength), 0.0f, 1.0f);
        Vector2 projection = lineStart + t * lineVector;
        return point.DistanceTo(projection) <= threshold;
    }

    private void ShowConnectionMenu(Vector2 position)
    {
        if (_selectedConnection == null)
            return;

        var popup = new PopupMenu();
        if (_selectedConnection.IsEditable())
            popup.AddItem("Edit Connection", 0);
        popup.AddItem("Delete Connection", 1);
        popup.Position = (Vector2I)position;
        popup.IdPressed += OnConnectionMenuSelected;
        popup.PopupHide += () => popup.QueueFree();
        _owner.AddChild(popup);
        popup.Popup();
    }

    private void OnConnectionMenuSelected(long id)
    {
        switch (id)
        {
            case 0:
                EditConnection();
                break;
            case 1:
                DeleteSelectedConnection();
                break;
        }
    }

    private void EditConnection()
    {
        if (_selectedConnection == null || !_selectedConnection.IsEditable())
            return;

        Vector2I dialogSize = GetConnectionEditDialogSize();
        var dialog = new AcceptDialog
        {
            Title = _selectedConnection.GetDisplayName(),
            DialogAutowrap = true,
            Unresizable = false,
            MinSize = new Vector2I(420, 320),
            Size = dialogSize
        };

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            CustomMinimumSize = new Vector2(dialogSize.X - 40, dialogSize.Y - 110),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        var margin = new MarginContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);

        Control ui = _selectedConnection.CreateEditUI(_createContext().WithConnection(_selectedConnection));
        ui.Name = "edit_ui";
        ui.CustomMinimumSize = new Vector2(360, 0);
        ui.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        ui.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        margin.AddChild(ui);
        scroll.AddChild(margin);
        dialog.AddChild(scroll);

        GraphConnection connectionRef = _selectedConnection;
        dialog.Confirmed += () =>
        {
            RemoveConnectionLabel(connectionRef);
            CreateConnectionLabel(connectionRef);
            GraphAsset graph = _getCurrentGraph();
            if (graph != null)
                GraphSaveService.Save(_owner, graph, _graphEdit, false);
            GD.Print("Connection updated");
        };
        _owner.AddChild(dialog);
        dialog.PopupCentered(dialogSize);
    }

    private Vector2I GetConnectionEditDialogSize()
    {
        Vector2 viewportSize = _owner.GetViewport().GetVisibleRect().Size;
        int width = Math.Clamp((int)(viewportSize.X * 0.55f), 480, 760);
        int height = Math.Clamp((int)(viewportSize.Y * 0.72f), 360, 680);
        return new Vector2I(width, height);
    }

    private void DeleteSelectedConnection()
    {
        if (_selectedConnection == null)
            return;

        GraphConnection connection = _selectedConnection;
        _selectedConnection = null;
        _deleteConnection?.Invoke(connection);
    }

    private void CreateConnectionLabel(GraphConnection connection)
    {
        Label label = connection.CreateConnectionLabel();
        string connectionKey = GetConnectionKey(connection);
        _connectionLabels[connectionKey] = label;
        _graphEdit.AddChild(label);
    }

    private void UpdateConnectionLabelPosition(GraphConnection connection)
    {
        var fromNode = _graphEdit.GetNodeOrNull<GraphNode>(connection.FromNode);
        var toNode = _graphEdit.GetNodeOrNull<GraphNode>(connection.ToNode);
        if (fromNode == null || toNode == null)
            return;

        if (connection.FromPort < 0 || connection.FromPort >= fromNode.GetOutputPortCount())
            return;

        if (connection.ToPort < 0 || connection.ToPort >= toNode.GetInputPortCount())
            return;

        Vector2 fromPos = GetPortPositionInLocal(fromNode, true, connection.FromPort);
        Vector2 toPos = GetPortPositionInLocal(toNode, false, connection.ToPort);
        Vector2 midLocal = (fromPos + toPos) / 2.0f;
        string connectionKey = GetConnectionKey(connection);
        if (_connectionLabels.TryGetValue(connectionKey, out Label label))
            label.Position = midLocal - new Vector2(label.Size.X / 2, label.Size.Y + 5);
    }

    private void RemoveConnectionLabel(GraphConnection connection)
    {
        string connectionKey = GetConnectionKey(connection);
        if (!_connectionLabels.TryGetValue(connectionKey, out Label label))
            return;

        label.QueueFree();
        _connectionLabels.Remove(connectionKey);
    }

    private static string GetConnectionKey(GraphConnection connection)
    {
        return $"{connection.FromNode}:{connection.FromPort}->{connection.ToNode}:{connection.ToPort}";
    }
}
#endif
