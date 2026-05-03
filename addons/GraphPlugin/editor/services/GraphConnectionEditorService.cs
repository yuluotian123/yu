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
    private readonly Action<GraphConnection> _selectConnection;
    private readonly Dictionary<string, Label> _connectionLabels = new();
    private GraphConnection _selectedConnection;
    private GraphConnection _hoveredConnection;

    /// <summary>创建连线编辑服务。</summary>
    public GraphConnectionEditorService(
        Window owner,
        GraphEdit graphEdit,
        Func<GraphAsset> getCurrentGraph,
        Func<GraphEditorContext> createContext,
        Action<GraphConnection> deleteConnection,
        Action<GraphConnection> selectConnection = null)
    {
        _owner = owner;
        _graphEdit = graphEdit;
        _getCurrentGraph = getCurrentGraph;
        _createContext = createContext;
        _deleteConnection = deleteConnection;
        _selectConnection = selectConnection;
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
            if (connection != null && mb.ButtonIndex == MouseButton.Left)
            {
                _selectedConnection = connection;
                _selectConnection?.Invoke(connection);
                return true;
            }

            if (connection != null && mb.ButtonIndex == MouseButton.Right)
            {
                _selectedConnection = connection;
                _selectConnection?.Invoke(connection);
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
            GraphConnection matchedConnection = null;
            foreach (GraphConnection connection in graph.Connections)
            {
                if (GetConnectionKey(connection) == connectionKey)
                {
                    matchedConnection = connection;
                    break;
                }
            }

            if (matchedConnection == null)
                keysToRemove.Add(connectionKey);
            else if (_connectionLabels.TryGetValue(connectionKey, out Label existingLabel))
                existingLabel.Text = matchedConnection.GetDisplayName();
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
            case 1:
                DeleteSelectedConnection();
                break;
        }
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
