#if TOOLS
using Godot;

/// <summary>
/// 节点操作相关方法
/// </summary>
public partial class GraphCanvasEditorWindow
{
    private void CreateNodeFromData(GraphNodeData data)
    {
        var node = GraphNodeFactory.CreateNodeUI(data);
        _graphEdit.AddChild(node);

        // 如果是子图节点，在其 UI 上注入"进入子图"按钮
        if (data is SubGraphNodeData subData)
            InjectSubGraphButton(node, subData);
    }
    private void InjectSubGraphButton(GraphNode node, SubGraphNodeData subData)
    {
        var content = node.GetNodeOrNull<VBoxContainer>("SubGraphContent");
        if (content == null) return;

        var enterBtn = new Button
        {
            Text = "进入子图 ▶",
            TooltipText = string.IsNullOrEmpty(subData.SubGraphPath) ? "请先绑定子图资源" : $"进入: {subData.GetDisplayName()}",
            Disabled = string.IsNullOrEmpty(subData.SubGraphPath)
        };
        enterBtn.Name = "EnterBtn";
        enterBtn.Pressed += () => TryEnterSubGraph(subData);
        content.AddChild(enterBtn);

        // 绑定子图资源按钮
        var bindBtn = new Button
        {
            Text = string.IsNullOrEmpty(subData.SubGraphPath) ? "绑定子图资源..." : "更换子图资源...",
            TooltipText = "选择或创建一个 GraphAsset 资源文件"
        };
        bindBtn.Name = "BindBtn";
        bindBtn.Pressed += () => ShowBindSubGraphDialog(subData, node);
        content.AddChild(bindBtn);
    }

    private void OnPopupRequest(Vector2 position)
{
    if (_currentGraph == null) return;
    
    var allowedNodes = _currentGraph.GetAllowedNodeTypes();
    if (allowedNodes.Count == 0) return;
    
    var popup = new SearchablePopup<string>(
        allowedNodes,
        nodeType => nodeType,  // 标签就是节点类型名
        null                    // 暂不分组，可根据需要添加
    );
    
    popup.OnItemSelected += nodeType => CreateNewNode(nodeType, position);
    
    // 需要一个临时控件来定位弹窗
    var anchor = new Control { Position = position };
    _graphEdit.AddChild(anchor);
    popup.ShowBelow(anchor);
    anchor.QueueFree();
}
    private void CreateNewNode(string nodeType, Vector2 position)
    {
        // 先生成 id，以便 undo/redo 时用相同 id 重建
        var data = GraphNodeFactory.CreateNodeData(nodeType);
        data.Position = position;

        if (_undoRedo != null)
        {
            // nodeType / nodeId / position 均为 Variant 兼容类型
            _undoRedo.CreateAction("添加节点");
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
        // 不走 UndoRedo 传 GraphNodeData（非 Variant），直接操作
        foreach (var nodeName in nodes)
            DoRemoveNode(nodeName);
    }
}
#endif
