#if TOOLS
using Godot;

/// <summary>
/// Node operations.
/// </summary>
public partial class GraphCanvasEditorWindow
{
    private void CreateNodeFromData(GraphNodeData data)
    {
        var node = GraphNodeFactory.CreateNodeUI(data, CreateEditorContext());
        _graphEdit.AddChild(node);

        if (data is SubGraphNodeData subData)
            InjectSubGraphButton(node, subData);
    }

    private void InjectSubGraphButton(GraphNode node, SubGraphNodeData subData)
    {
        var content = node.GetNodeOrNull<VBoxContainer>("SubGraphContent");
        if (content == null)
            return;

        var enterBtn = new Button
        {
            Text = "Enter SubGraph >",
            TooltipText = string.IsNullOrEmpty(subData.SubGraphPath)
                ? "Bind a subgraph resource first"
                : $"Enter: {subData.GetDisplayName()}",
            Disabled = string.IsNullOrEmpty(subData.SubGraphPath)
        };
        enterBtn.Name = "EnterBtn";
        enterBtn.Pressed += () => TryEnterSubGraph(subData);
        content.AddChild(enterBtn);

        var bindBtn = new Button
        {
            Text = string.IsNullOrEmpty(subData.SubGraphPath)
                ? "Bind SubGraph Resource..."
                : "Replace SubGraph Resource...",
            TooltipText = "Select or create a GraphAsset resource file"
        };
        bindBtn.Name = "BindBtn";
        bindBtn.Pressed += () => ShowBindSubGraphDialog(subData, node);
        content.AddChild(bindBtn);
    }

    private void OnPopupRequest(Vector2 position)
    {
        if (_currentGraph == null)
            return;

        var allowedNodes = _currentGraph.GetAllowedNodeTypes();
        if (allowedNodes.Count == 0)
            return;

        var popup = new SearchablePopup<string>(
            allowedNodes,
            nodeType => nodeType,
            null);

        popup.OnItemSelected += nodeType => CreateNewNode(nodeType, position);

        var anchor = new Control { Position = position };
        _graphEdit.AddChild(anchor);
        popup.ShowBelow(anchor);
        anchor.QueueFree();
    }

    private void CreateNewNode(string nodeType, Vector2 position)
    {
        var data = GraphNodeFactory.CreateNodeData(nodeType);
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
        var data = GraphNodeFactory.CreateNodeData(nodeType);
        data.Id = nodeId;
        data.Position = position;
        _currentGraph.Nodes.Add(data);
        CreateNodeFromData(data);
    }

    private void DoRemoveNode(StringName nodeId)
    {
        _currentGraph.RemoveNode(nodeId);
        var node = _graphEdit.GetNodeOrNull<GraphNode>(new NodePath(nodeId));
        node?.QueueFree();
    }

    private void OnDeleteNodes(Godot.Collections.Array<StringName> nodes)
    {
        foreach (var nodeName in nodes)
            DoRemoveNode(nodeName);
    }
}
#endif
