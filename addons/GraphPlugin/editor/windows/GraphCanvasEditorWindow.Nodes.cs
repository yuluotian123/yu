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
        _componentPanel?.Refresh();
        var dynamicEntries = new System.Collections.Generic.List<GraphNodeSearchService.GraphNodeSearchEntry>();
        foreach (GraphComponentTypeDescriptor descriptor in _componentPanel?.GetAvailableDescriptors() ??
                 System.Array.Empty<GraphComponentTypeDescriptor>())
        {
            foreach (GraphComponentActionDescriptor action in descriptor.Actions)
            {
                string typeName = descriptor.TypeName;
                string memberId = action.MemberId;
                dynamicEntries.Add(new GraphNodeSearchService.GraphNodeSearchEntry
                {
                    Label = $"{descriptor.DisplayName} / {action.DisplayName}",
                    Group = $"Component / {descriptor.DisplayName} / Actions",
                    SearchText = $"{descriptor.TypeName} {action.MemberId} {action.DisplayName}",
                    DynamicAction = () => CreateComponentCallNode(typeName, memberId, graphPosition)
                });
            }
            foreach (GraphComponentValueDescriptor value in descriptor.Values)
            {
                if (_currentGraph is GameLogic.HfsmGraphAsset)
                    continue;
                string typeName = descriptor.TypeName;
                string memberId = value.MemberId;
                if (value.CanRead)
                {
                    dynamicEntries.Add(new GraphNodeSearchService.GraphNodeSearchEntry
                    {
                        Label = $"{descriptor.DisplayName} / Get {value.DisplayName}",
                        Group = $"Component / {descriptor.DisplayName} / Values",
                        SearchText = $"{descriptor.TypeName} {memberId} {value.DisplayName} Get",
                        DynamicAction = () => CreateComponentValueNode(typeName, memberId, false, graphPosition)
                    });
                }
                if (value.CanWrite)
                {
                    dynamicEntries.Add(new GraphNodeSearchService.GraphNodeSearchEntry
                    {
                        Label = $"{descriptor.DisplayName} / Set {value.DisplayName}",
                        Group = $"Component / {descriptor.DisplayName} / Values",
                        SearchText = $"{descriptor.TypeName} {memberId} {value.DisplayName} Set",
                        DynamicAction = () => CreateComponentValueNode(typeName, memberId, true, graphPosition)
                    });
                }
            }
        }

        GraphNodeSearchService.Show(
            _currentGraph,
            _graphEdit,
            position,
            dynamicEntries,
            nodeType => CreateNewNode(nodeType, graphPosition),
            _ => { });
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

    private void CreateComponentCallNode(string componentTypeName, string actionId) =>
        CreateComponentCallNode(componentTypeName, actionId, ToGraphPosition(new Vector2(320, 220)));

    private void CreateComponentCallNode(string componentTypeName, string actionId, Vector2 position)
    {
        if (_currentGraph == null || string.IsNullOrWhiteSpace(componentTypeName) || string.IsNullOrWhiteSpace(actionId))
            return;

        string nodeType;
        if (_currentGraph is BehaviorTreeGraphAsset)
            nodeType = nameof(BehaviorTreeComponentCallNodeData);
        else if (_currentGraph is GameLogic.HfsmGraphAsset)
            nodeType = nameof(GameLogic.HfsmComponentActionStateNodeData);
        else
            nodeType = nameof(GraphComponentCallNodeData);

        GraphNodeData data = GraphTypeRegistry.CreateNodeData(nodeType);
        data.Position = position;
        if (data is GraphComponentCallNodeData flowCall)
        {
            flowCall.ComponentTypeName = componentTypeName;
            flowCall.MemberId = actionId;
            flowCall.InitializeArguments();
        }
        else if (data is BehaviorTreeComponentCallNodeData behaviorCall)
        {
            behaviorCall.Call.ComponentTypeName = componentTypeName;
            behaviorCall.Call.ActionId = actionId;
        }
        else if (data is GameLogic.HfsmComponentActionStateNodeData hfsmCall)
        {
            hfsmCall.Call.ComponentTypeName = componentTypeName;
            hfsmCall.Call.ActionId = actionId;
        }

        _currentGraph.Nodes.Add(data);
        _currentGraph.MarkDirty();
        CreateNodeFromData(data);
    }

    private void CreateComponentValueNode(string componentTypeName, string memberId, bool write) =>
        CreateComponentValueNode(componentTypeName, memberId, write, ToGraphPosition(new Vector2(320, 220)));

    private void CreateComponentValueNode(string componentTypeName, string memberId, bool write, Vector2 position)
    {
        if (_currentGraph == null || string.IsNullOrWhiteSpace(componentTypeName) || string.IsNullOrWhiteSpace(memberId))
            return;

        string nodeType;
        if (_currentGraph is BehaviorTreeGraphAsset)
        {
            nodeType = write
                ? nameof(BehaviorTreeComponentSetNodeData)
                : nameof(BehaviorTreeComponentGetNodeData);
        }
        else
        {
            nodeType = write
                ? nameof(GraphComponentSetNodeData)
                : nameof(GraphComponentGetNodeData);
        }
        GraphNodeData data = GraphTypeRegistry.CreateNodeData(nodeType);
        data.Position = position;
        if (data is GraphComponentSetNodeData setNode)
        {
            setNode.ComponentTypeName = componentTypeName;
            setNode.MemberId = memberId;
        }
        else if (data is GraphComponentGetNodeData getNode)
        {
            getNode.ComponentTypeName = componentTypeName;
            getNode.MemberId = memberId;
        }
        else if (data is BehaviorTreeComponentSetNodeData behaviorSet)
        {
            behaviorSet.ComponentTypeName = componentTypeName;
            behaviorSet.MemberId = memberId;
        }
        else if (data is BehaviorTreeComponentGetNodeData behaviorGet)
        {
            behaviorGet.ComponentTypeName = componentTypeName;
            behaviorGet.MemberId = memberId;
        }

        _currentGraph.Nodes.Add(data);
        _currentGraph.MarkDirty();
        CreateNodeFromData(data);
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
