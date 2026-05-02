#if TOOLS
using Godot;

/// <summary>
/// 图编辑命令服务。
/// </summary>
/// <remarks>
/// 这个服务只执行“对图数据和 GraphEdit 视图的一次确定修改”。Undo/Redo 动作仍由
/// <see cref="EditorUndoRedoManager"/> 在窗口层创建，但具体增删节点、连线的实现集中到这里，
/// 避免同一段修改逻辑散落在多个 partial 文件中。
/// </remarks>
public static class GraphCommandService
{
    /// <summary>添加节点。</summary>
    public static GraphNodeData AddNode(
        GraphAsset graph,
        string nodeType,
        string nodeId,
        Vector2 position,
        System.Action<GraphNodeData> createNodeView)
    {
        GraphNodeData data = GraphTypeRegistry.CreateNodeData(nodeType);
        data.Id = nodeId;
        data.Position = position;
        graph.Nodes.Add(data);
        graph.MarkDirty();
        createNodeView?.Invoke(data);
        return data;
    }

    /// <summary>删除节点及相关视图。</summary>
    public static void RemoveNode(GraphAsset graph, GraphEdit graphEdit, string nodeId)
    {
        graph.RemoveNode(nodeId);
        var node = graphEdit.GetNodeOrNull<GraphNode>(new NodePath(nodeId));
        node?.QueueFree();
    }

    /// <summary>添加连线。</summary>
    public static bool AddConnection(
        GraphAsset graph,
        GraphEdit graphEdit,
        string fromNode,
        int fromPort,
        string toNode,
        int toPort)
    {
        bool success = graph.ConnectNodes(fromNode, fromPort, toNode, toPort);
        if (success)
            graphEdit.ConnectNode(fromNode, fromPort, toNode, toPort);

        return success;
    }

    /// <summary>删除连线。</summary>
    public static void RemoveConnection(
        GraphAsset graph,
        GraphEdit graphEdit,
        string fromNode,
        int fromPort,
        string toNode,
        int toPort)
    {
        graphEdit.DisconnectNode(fromNode, fromPort, toNode, toPort);
        for (int i = graph.Connections.Count - 1; i >= 0; i--)
        {
            GraphConnection connection = graph.Connections[i];
            if (connection.Matches(fromNode, fromPort, toNode, toPort))
            {
                graph.Connections.RemoveAt(i);
                break;
            }
        }

        graph.MarkDirty();
    }
}
#endif
