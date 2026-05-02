#if TOOLS
using System;
using Godot;

/// <summary>
/// 图编辑器控制器。
/// </summary>
/// <remarks>
/// 控制器保存当前编辑上下文，并负责把图文档加载到 GraphEdit。窗口仍然创建控件和弹窗，
/// 但“当前图是什么、加载时要遍历哪些节点和连线”集中在这里。
/// </remarks>
public sealed class GraphEditorController
{
    private readonly GraphEdit _graphEdit;

    /// <summary>创建控制器。</summary>
    public GraphEditorController(GraphEdit graphEdit)
    {
        _graphEdit = graphEdit;
    }

    /// <summary>当前正在编辑的图。</summary>
    public GraphAsset CurrentGraph { get; private set; }

    /// <summary>
    /// 加载图，并把节点和连线交给窗口提供的视图回调。
    /// </summary>
    public void LoadGraph(
        GraphAsset graph,
        Action<GraphNodeData> createNodeView,
        Action<GraphConnection> createConnectionView)
    {
        CurrentGraph = graph;
        if (graph == null)
            return;

        foreach (GraphNodeData node in graph.Nodes)
            createNodeView?.Invoke(node);

        foreach (GraphConnection connection in graph.Connections)
            createConnectionView?.Invoke(connection);

        RestoreEditorState(graph);
    }

    private void RestoreEditorState(GraphAsset graph)
    {
        if (_graphEdit == null || graph?.Document?.EditorState == null)
            return;

        _graphEdit.Zoom = graph.Document.EditorState.Zoom <= 0f ? 1f : graph.Document.EditorState.Zoom;
        _graphEdit.ScrollOffset = graph.Document.EditorState.ScrollOffset;
    }

    /// <summary>清空 GraphEdit 中的节点控件。</summary>
    public void ClearGraphEdit()
    {
        if (_graphEdit == null)
            return;

        foreach (Node child in _graphEdit.GetChildren())
        {
            if (child is GraphNode or Label)
            {
                _graphEdit.RemoveChild(child);
                child.QueueFree();
            }
        }
    }
}
#endif
