#if TOOLS
using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Connection operations and connection label handling.
/// </summary>
public partial class GraphCanvasEditorWindow
{
    private void OnConnectionRequest(StringName fromNode, long fromPort, StringName toNode, long toPort)
    {
        if (_undoRedo != null)
        {
            _undoRedo.CreateAction("Add Connection");
            _undoRedo.AddDoMethod(this, MethodName.DoAddConnection, fromNode, (int)fromPort, toNode, (int)toPort);
            _undoRedo.AddUndoMethod(this, MethodName.DoRemoveConnection, fromNode, (int)fromPort, toNode, (int)toPort);
            _undoRedo.CommitAction();
        }
        else
        {
            DoAddConnection(fromNode, (int)fromPort, toNode, (int)toPort);
        }
    }

    private void OnDisconnectionRequest(StringName fromNode, long fromPort, StringName toNode, long toPort)
    {
        if (_undoRedo != null)
        {
            _undoRedo.CreateAction("Remove Connection");
            _undoRedo.AddDoMethod(this, MethodName.DoRemoveConnection, fromNode, (int)fromPort, toNode, (int)toPort);
            _undoRedo.AddUndoMethod(this, MethodName.DoAddConnection, fromNode, (int)fromPort, toNode, (int)toPort);
            _undoRedo.CommitAction();
        }
        else
        {
            DoRemoveConnection(fromNode, (int)fromPort, toNode, (int)toPort);
        }
    }

    private void DoAddConnection(StringName fromNode, int fromPort, StringName toNode, int toPort)
    {
        bool success = _currentGraph.ConnectNodes(fromNode, fromPort, toNode, toPort);
        if (success)
            _graphEdit.ConnectNode(fromNode, fromPort, toNode, toPort);
    }

    private void DoRemoveConnection(StringName fromNode, int fromPort, StringName toNode, int toPort)
    {
        _graphEdit.DisconnectNode(fromNode, fromPort, toNode, toPort);
        for (int i = _currentGraph.Connections.Count - 1; i >= 0; i--)
        {
            var conn = _currentGraph.Connections[i];
            if (conn.FromNode == fromNode && conn.FromPort == fromPort &&
                conn.ToNode == toNode && conn.ToPort == toPort)
            {
                _currentGraph.Connections.RemoveAt(i);
                break;
            }
        }
    }

    private Vector2 GetPortPositionInLocal(GraphNode node, bool isOutput, int port)
    {
        var portOffset = isOutput ? node.GetOutputPortPosition(port) : node.GetInputPortPosition(port);
        var globalPos = node.GlobalPosition + portOffset * _graphEdit.Zoom;
        return globalPos - _graphEdit.GlobalPosition;
    }

    private GraphConnection FindConnectionAtPosition(Vector2 pos)
    {
        if (_currentGraph == null)
            return null;

        foreach (var conn in _currentGraph.Connections)
        {
            var fromNode = _graphEdit.GetNodeOrNull<GraphNode>(conn.FromNode);
            var toNode = _graphEdit.GetNodeOrNull<GraphNode>(conn.ToNode);
            if (fromNode == null || toNode == null)
                continue;

            if (conn.FromPort < 0 || conn.FromPort >= fromNode.GetOutputPortCount())
                continue;

            if (conn.ToPort < 0 || conn.ToPort >= toNode.GetInputPortCount())
                continue;

            var fromPos = GetPortPositionInLocal(fromNode, true, conn.FromPort);
            var toPos = GetPortPositionInLocal(toNode, false, conn.ToPort);
            if (IsPointNearLine(pos, fromPos, toPos, 10.0f))
                return conn;
        }

        return null;
    }

    private bool IsPointNearLine(Vector2 point, Vector2 lineStart, Vector2 lineEnd, float threshold)
    {
        var lineVec = lineEnd - lineStart;
        var pointVec = point - lineStart;
        var lineLen = lineVec.Length();
        if (lineLen == 0)
            return point.DistanceTo(lineStart) <= threshold;

        var t = Mathf.Clamp(pointVec.Dot(lineVec) / (lineLen * lineLen), 0.0f, 1.0f);
        var projection = lineStart + t * lineVec;
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
        AddChild(popup);
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
                DeleteConnection();
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

        var ui = _selectedConnection.CreateEditUI(CreateEditorContext().WithConnection(_selectedConnection));
        ui.Name = "edit_ui";
        ui.CustomMinimumSize = new Vector2(360, 0);
        ui.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        ui.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        margin.AddChild(ui);
        scroll.AddChild(margin);
        dialog.AddChild(scroll);

        var connRef = _selectedConnection;
        dialog.Confirmed += () =>
        {
            RemoveConnectionLabel(connRef);
            CreateConnectionLabel(connRef);
            _currentGraph.SaveToJson();
            ResourceSaver.Save(_currentGraph, _currentGraph.ResourcePath);
            GD.Print("Connection updated");
        };
        AddChild(dialog);
        dialog.PopupCentered(dialogSize);
    }

    private Vector2I GetConnectionEditDialogSize()
    {
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        int width = Math.Clamp((int)(viewportSize.X * 0.55f), 480, 760);
        int height = Math.Clamp((int)(viewportSize.Y * 0.72f), 360, 680);
        return new Vector2I(width, height);
    }

    private void DeleteConnection()
    {
        if (_selectedConnection == null)
            return;

        DeleteConnectionInternal(_selectedConnection);
        _selectedConnection = null;
    }

    private void DeleteHoveredConnection()
    {
        if (_hoveredConnection == null)
            return;

        DeleteConnectionInternal(_hoveredConnection);
        _hoveredConnection = null;
    }

    private void DeleteConnectionInternal(GraphConnection conn)
    {
        var fromNode = new StringName(conn.FromNode);
        var fromPort = conn.FromPort;
        var toNode = new StringName(conn.ToNode);
        var toPort = conn.ToPort;
        if (_undoRedo != null)
        {
            _undoRedo.CreateAction("Remove Connection");
            _undoRedo.AddDoMethod(this, MethodName.DoRemoveConnection, fromNode, fromPort, toNode, toPort);
            _undoRedo.AddUndoMethod(this, MethodName.DoAddConnection, fromNode, fromPort, toNode, toPort);
            _undoRedo.CommitAction();
        }
        else
        {
            DoRemoveConnection(fromNode, fromPort, toNode, toPort);
        }
    }

    private void UpdateConnectionLabels()
    {
        var keysToRemove = new List<string>();
        foreach (var connKey in _connectionLabels.Keys)
        {
            bool found = false;
            foreach (var conn in _currentGraph.Connections)
            {
                if (GetConnectionKey(conn) == connKey)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                keysToRemove.Add(connKey);
        }

        foreach (var key in keysToRemove)
        {
            var label = _connectionLabels[key];
            label.QueueFree();
            _connectionLabels.Remove(key);
        }

        foreach (var conn in _currentGraph.Connections)
        {
            var connKey = GetConnectionKey(conn);
            if (!_connectionLabels.ContainsKey(connKey))
                CreateConnectionLabel(conn);
            UpdateConnectionLabelPosition(conn);
        }
    }

    private void CreateConnectionLabel(GraphConnection conn)
    {
        var label = conn.CreateConnectionLabel();
        var connKey = GetConnectionKey(conn);
        _connectionLabels[connKey] = label;
        _graphEdit.AddChild(label);
    }

    private void UpdateConnectionLabelPosition(GraphConnection conn)
    {
        var fromNode = _graphEdit.GetNodeOrNull<GraphNode>(conn.FromNode);
        var toNode = _graphEdit.GetNodeOrNull<GraphNode>(conn.ToNode);
        if (fromNode == null || toNode == null)
            return;

        if (conn.FromPort < 0 || conn.FromPort >= fromNode.GetOutputPortCount())
            return;

        if (conn.ToPort < 0 || conn.ToPort >= toNode.GetInputPortCount())
            return;

        var fromPos = GetPortPositionInLocal(fromNode, true, conn.FromPort);
        var toPos = GetPortPositionInLocal(toNode, false, conn.ToPort);
        var midLocal = (fromPos + toPos) / 2.0f;
        var connKey = GetConnectionKey(conn);
        if (_connectionLabels.ContainsKey(connKey))
        {
            var label = _connectionLabels[connKey];
            label.Position = midLocal - new Vector2(label.Size.X / 2, label.Size.Y + 5);
        }
    }

    private void RemoveConnectionLabel(GraphConnection conn)
    {
        var connKey = GetConnectionKey(conn);
        if (_connectionLabels.ContainsKey(connKey))
        {
            var label = _connectionLabels[connKey];
            label.QueueFree();
            _connectionLabels.Remove(connKey);
        }
    }

    private string GetConnectionKey(GraphConnection conn) => $"{conn.FromNode}:{conn.FromPort}->{conn.ToNode}:{conn.ToPort}";
}
#endif
