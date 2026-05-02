using Godot;
using System.Collections.Generic;

/// <summary>
/// 图编辑器上下文。
/// </summary>
/// <remarks>
/// 自定义节点 UI、连线 UI 和黑板值 UI 都通过这个对象访问当前图、根图、父图链、
/// GraphEdit 控件和当前正在编辑的数据对象。它只在 editor 代码中使用，不进入运行时序列化。
/// </remarks>
public sealed class GraphEditorContext
{
    /// <summary>当前正在编辑的图。</summary>
    public GraphAsset CurrentGraph { get; set; }

    /// <summary>子图导航链最上层的根图。</summary>
    public GraphAsset RootGraph { get; set; }

    /// <summary>当前图的父图链，直接父图排在前面。</summary>
    public List<GraphAsset> ParentGraphs { get; set; } = new();

    /// <summary>承载节点视图的 Godot GraphEdit。</summary>
    public GraphEdit GraphEdit { get; set; }

    /// <summary>当前节点视图。仅在节点 UI 构建时有值。</summary>
    public GraphNode GraphNode { get; set; }

    /// <summary>当前节点数据。仅在节点 UI 构建时有值。</summary>
    public GraphNodeData NodeData { get; set; }

    /// <summary>当前连线数据。仅在连线编辑 UI 构建时有值。</summary>
    public GraphConnection Connection { get; set; }

    /// <summary>当前编辑场景中的全局黑板节点。</summary>
    public GraphBlackboardNode GlobalBlackboard { get; set; }

    /// <summary>当前黑板条目。仅在黑板值 UI 构建时有值。</summary>
    public GraphBlackboardEntry BlackboardEntry { get; set; }

    /// <summary>创建带当前节点信息的上下文副本。</summary>
    public GraphEditorContext WithGraphNode(GraphNodeData nodeData, GraphNode graphNode)
    {
        return CopyBase(new GraphEditorContext
        {
            NodeData = nodeData,
            GraphNode = graphNode
        });
    }

    /// <summary>创建带当前连线信息的上下文副本。</summary>
    public GraphEditorContext WithConnection(GraphConnection connection)
    {
        return CopyBase(new GraphEditorContext
        {
            Connection = connection
        });
    }

    /// <summary>创建带当前黑板条目信息的上下文副本。</summary>
    public GraphEditorContext WithBlackboardEntry(GraphBlackboardEntry entry)
    {
        return CopyBase(new GraphEditorContext
        {
            BlackboardEntry = entry
        });
    }

    private GraphEditorContext CopyBase(GraphEditorContext target)
    {
        target.CurrentGraph = CurrentGraph;
        target.RootGraph = RootGraph;
        target.ParentGraphs = ParentGraphs;
        target.GraphEdit = GraphEdit;
        target.GlobalBlackboard = GlobalBlackboard;
        return target;
    }
}
