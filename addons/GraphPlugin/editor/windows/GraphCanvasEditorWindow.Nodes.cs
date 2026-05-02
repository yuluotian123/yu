#if TOOLS
using Godot;

/// <summary>
/// 节点相关的窗口信号转发。
/// </summary>
public partial class GraphCanvasEditorWindow
{
    private void CreateNodeFromData(GraphNodeData data)
    {
        var node = GraphNodeViewBuilder.CreateNodeUI(data, CreateEditorContext());
        _graphEdit.AddChild(node);

        if (data is SubGraphNodeData subData)
            _subGraphNavigator?.InjectSubGraphButtons(node, subData);
    }

    private void OnPopupRequest(Vector2 position)
    {
        GraphNodeSearchService.Show(_currentGraph, _graphEdit, position, nodeType => CreateNewNode(nodeType, position));
    }

    private void CreateNewNode(string nodeType, Vector2 position)
    {
        var data = GraphTypeRegistry.CreateNodeData(nodeType);
        data.Position = position;

        if (_undoRedo != null)
        {
            _undoRedo.CreateAction("Add Node");
            _undoRedo.AddDoMethod(this, MethodName.DoAddNode, nodeType, data.Id, position);
            _undoRedo.AddUndoMethod(this, MethodName.DoRemoveNode, new StringName(data.Id));
            _undoRedo.CommitAction();
        }
        else
        {
            DoAddNode(nodeType, data.Id, position);
        }
    }

    private void DoAddNode(string nodeType, string nodeId, Vector2 position)
    {
        GraphCommandService.AddNode(_currentGraph, nodeType, nodeId, position, CreateNodeFromData);
    }

    private void DoRemoveNode(StringName nodeId)
    {
        GraphCommandService.RemoveNode(_currentGraph, _graphEdit, nodeId);
    }

    private void OnDeleteNodes(Godot.Collections.Array<StringName> nodes)
    {
        if (nodes == null || nodes.Count == 0)
            return;

        string snapshotNodesJson = GraphSnapshotService.CaptureNodes(_currentGraph);
        string snapshotConnsJson = GraphSnapshotService.CaptureConnections(_currentGraph);

        if (_undoRedo != null)
        {
            _undoRedo.CreateAction("Delete Graph Nodes");
            foreach (StringName nodeName in nodes)
                _undoRedo.AddDoMethod(this, MethodName.DoRemoveNode, nodeName);
            _undoRedo.AddUndoMethod(this, MethodName.DoRestoreSnapshot, snapshotNodesJson, snapshotConnsJson);
            _undoRedo.CommitAction();
            return;
        }

        foreach (StringName nodeName in nodes)
            DoRemoveNode(nodeName);
    }
}
#endif
