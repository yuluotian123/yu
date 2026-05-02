#if TOOLS
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// 图快照和批量恢复服务。
/// </summary>
/// <remarks>
/// Undo/Redo 需要把一批节点和连线序列化成稳定字符串。这个服务集中处理
/// “序列化快照、清空图、恢复快照、粘贴批量节点”四件事，避免窗口层散落
/// JSON 操作和视图重建细节。
/// </remarks>
public static class GraphSnapshotService
{
    /// <summary>序列化当前节点列表。</summary>
    public static string CaptureNodes(GraphAsset graph)
    {
        return GraphJsonHelper.SerializeList(graph?.Nodes ?? new List<GraphNodeData>());
    }

    /// <summary>序列化当前连线列表。</summary>
    public static string CaptureConnections(GraphAsset graph)
    {
        return GraphJsonHelper.SerializeList(graph?.Connections ?? new List<GraphConnection>());
    }

    /// <summary>清空图数据和 GraphEdit 视图。</summary>
    public static void Clear(
        GraphAsset graph,
        GraphEditorController controller,
        GraphConnectionEditorService connectionEditor)
    {
        if (graph == null)
            return;

        graph.Nodes.Clear();
        graph.Connections.Clear();
        graph.MarkDirty();
        connectionEditor?.Reset();
        controller?.ClearGraphEdit();
    }

    /// <summary>从序列化快照恢复图数据和视图。</summary>
    public static void Restore(
        GraphAsset graph,
        GraphEdit graphEdit,
        GraphEditorController controller,
        GraphConnectionEditorService connectionEditor,
        Action<GraphNodeData> createNodeView,
        string nodesJson,
        string connectionsJson)
    {
        Clear(graph, controller, connectionEditor);
        AddSerialized(graph, graphEdit, createNodeView, nodesJson, connectionsJson);
    }

    /// <summary>把序列化节点和连线追加到当前图。</summary>
    public static void AddSerialized(
        GraphAsset graph,
        GraphEdit graphEdit,
        Action<GraphNodeData> createNodeView,
        string nodesJson,
        string connectionsJson)
    {
        List<GraphNodeData> nodes = GraphJsonHelper.DeserializeList<GraphNodeData>(nodesJson);
        List<GraphConnection> connections = GraphJsonHelper.DeserializeList<GraphConnection>(connectionsJson);
        Add(graph, graphEdit, createNodeView, nodes, connections);
    }

    /// <summary>把节点和连线追加到当前图，并同步创建视图。</summary>
    public static void Add(
        GraphAsset graph,
        GraphEdit graphEdit,
        Action<GraphNodeData> createNodeView,
        IList<GraphNodeData> nodes,
        IList<GraphConnection> connections)
    {
        if (graph == null)
            return;

        if (nodes != null)
        {
            foreach (GraphNodeData node in nodes)
            {
                if (node == null)
                    continue;

                graph.Nodes.Add(node);
                createNodeView?.Invoke(node);
            }
        }

        if (connections != null)
        {
            foreach (GraphConnection connection in connections)
            {
                if (connection == null)
                    continue;

                graph.Connections.Add(connection);
                graphEdit?.ConnectNode(connection.FromNode, connection.FromPort, connection.ToNode, connection.ToPort);
            }
        }

        graph.MarkDirty();
    }
}
#endif
