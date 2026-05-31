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
            _subGraphNavigator?.InjectSubGraphEnterButton(node, subData);
    }

    private void OnPopupRequest(Vector2 position)
    {
        Vector2 graphPosition = ToGraphPosition(position);
        GraphNodeSearchService.Show(_currentGraph, _graphEdit, position, nodeType => CreateNewNode(nodeType, graphPosition));
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

    private Vector2 ToGraphPosition(Vector2 localPosition)
    {
        if (_graphEdit == null)
            return localPosition;

        float zoom = _graphEdit.Zoom;
        if (Mathf.IsZeroApprox(zoom))
            zoom = 1f;

        return (localPosition + _graphEdit.ScrollOffset) / zoom;
    }

    private void DoRemoveNode(StringName nodeId)
    {
        GraphCommandService.RemoveNode(_currentGraph, _graphEdit, nodeId);
        if (_boundTimelineNodeId == nodeId.ToString())
        {
            _timelinePanel?.Clear();
            _boundTimelineNodeId = string.Empty;
        }
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

    private void OnNodeSelected(Node node)
    {
        if (node is GraphNode graphNode)
            _selectionInspector?.ShowNode(graphNode);
    }

    private void OnNodeDeselected(Node node)
    {
        GraphNode selectedNode = GetSingleSelectedGraphNode();
        if (selectedNode != null)
            _selectionInspector?.ShowNode(selectedNode);
        else
            _selectionInspector?.Clear();
    }

    private GraphNode GetSingleSelectedGraphNode()
    {
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

        return selectedGraphNode;
    }

    private Control BuildExtraNodeInspector(GraphNode graphNode, GraphNodeData nodeData)
    {
        return nodeData is SubGraphNodeData subData
            ? _subGraphNavigator?.CreateSubGraphInspectorControls(graphNode, subData)
            : null;
    }
}
#endif
