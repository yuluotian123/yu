#if TOOLS
using Godot;

/// <summary>
/// 连线相关的窗口信号转发。
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
            return;
        }

        DoAddConnection(fromNode, (int)fromPort, toNode, (int)toPort);
    }

    private void OnDisconnectionRequest(StringName fromNode, long fromPort, StringName toNode, long toPort)
    {
        if (_undoRedo != null)
        {
            _undoRedo.CreateAction("Remove Connection");
            _undoRedo.AddDoMethod(this, MethodName.DoRemoveConnection, fromNode, (int)fromPort, toNode, (int)toPort);
            _undoRedo.AddUndoMethod(this, MethodName.DoAddConnection, fromNode, (int)fromPort, toNode, (int)toPort);
            _undoRedo.CommitAction();
            return;
        }

        DoRemoveConnection(fromNode, (int)fromPort, toNode, (int)toPort);
    }

    private void DeleteConnectionWithUndo(GraphConnection connection)
    {
        if (connection == null)
            return;

        StringName fromNode = connection.FromNode;
        StringName toNode = connection.ToNode;
        int fromPort = connection.FromPort;
        int toPort = connection.ToPort;

        if (_undoRedo != null)
        {
            _undoRedo.CreateAction("Remove Connection");
            _undoRedo.AddDoMethod(this, MethodName.DoRemoveConnection, fromNode, fromPort, toNode, toPort);
            _undoRedo.AddUndoMethod(this, MethodName.DoAddConnection, fromNode, fromPort, toNode, toPort);
            _undoRedo.CommitAction();
            return;
        }

        DoRemoveConnection(fromNode, fromPort, toNode, toPort);
    }

    private void DoAddConnection(StringName fromNode, int fromPort, StringName toNode, int toPort)
    {
        GraphCommandService.AddConnection(_currentGraph, _graphEdit, fromNode, fromPort, toNode, toPort);
    }

    private void DoRemoveConnection(StringName fromNode, int fromPort, StringName toNode, int toPort)
    {
        GraphCommandService.RemoveConnection(_currentGraph, _graphEdit, fromNode, fromPort, toNode, toPort);
    }
}
#endif
