#if TOOLS
using System.Linq;
using Godot;

/// <summary>连线命令。撤销保留完整转换数据，并绑定原始图资源。</summary>
public partial class GraphCanvasEditorWindow
{
    private void OnConnectionRequest(StringName fromNode, long fromPort, StringName toNode, long toPort)
    {
        GraphAsset graph = _currentGraph;
        // Only record successful operations: undoing a rejected duplicate must not remove the original.
        if (graph == null || !graph.ConnectNodes(fromNode, (int)fromPort, toNode, (int)toPort)) return;
        _graphEdit.ConnectNode(fromNode, (int)fromPort, toNode, (int)toPort);
        GraphConnection connection = graph.Connections[^1];
        if (_undoRedo != null)
        {
            _undoRedo.CreateAction("添加连线 / Add Connection");
            _undoRedo.AddDoMethod(this, MethodName.DoRestoreConnection, graph, GraphJsonHelper.Serialize(connection), graph.Connections.Count - 1);
            _undoRedo.AddUndoMethod(this, MethodName.DoRemoveConnection, graph, fromNode, (int)fromPort, toNode, (int)toPort);
            _undoRedo.CommitAction(execute: false);
        }
        if (graph is StateGraphAsset) _connectionEditor?.SelectConnection(connection);
    }

    private void OnDisconnectionRequest(StringName fromNode, long fromPort, StringName toNode, long toPort)
    {
        DeleteConnectionWithUndo(_currentGraph?.Connections.FirstOrDefault(c => c.Matches(fromNode, (int)fromPort, toNode, (int)toPort)));
    }

    private void DeleteConnectionWithUndo(GraphConnection connection)
    {
        GraphAsset graph = _currentGraph;
        if (connection == null || graph == null || !graph.Connections.Contains(connection)) return;
        if (_undoRedo != null)
        {
            _undoRedo.CreateAction("删除连线 / Remove Connection");
            _undoRedo.AddDoMethod(this, MethodName.DoRemoveConnection, graph, new StringName(connection.FromNode), connection.FromPort, new StringName(connection.ToNode), connection.ToPort);
            _undoRedo.AddUndoMethod(this, MethodName.DoRestoreConnection, graph, GraphJsonHelper.Serialize(connection), graph.Connections.IndexOf(connection));
            _undoRedo.CommitAction();
            return;
        }
        DoRemoveConnection(graph, connection.FromNode, connection.FromPort, connection.ToNode, connection.ToPort);
    }

    private void DoRestoreConnection(GraphAsset graph, string json, int index)
    {
        GraphConnection connection = GraphJsonHelper.Deserialize<GraphConnection>(json);
        if (connection == null || graph.HasConnection(connection.FromNode, connection.FromPort, connection.ToNode, connection.ToPort)) return;
        graph.Connections.Insert(System.Math.Clamp(index, 0, graph.Connections.Count), connection);
        graph.MarkDirty();
        if (graph == _currentGraph)
            _graphEdit.ConnectNode(connection.FromNode, connection.FromPort, connection.ToNode, connection.ToPort);
    }

    private void DoRemoveConnection(GraphAsset graph, StringName fromNode, int fromPort, StringName toNode, int toPort)
    {
        if (graph == _currentGraph)
            GraphCommandService.RemoveConnection(graph, _graphEdit, fromNode, fromPort, toNode, toPort);
        else
        {
            for (int i = graph.Connections.Count - 1; i >= 0; i--)
                if (graph.Connections[i].Matches(fromNode, fromPort, toNode, toPort)) graph.Connections.RemoveAt(i);
            graph.MarkDirty();
        }
    }
}
#endif
