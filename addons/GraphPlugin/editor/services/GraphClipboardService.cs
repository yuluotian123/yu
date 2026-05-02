#if TOOLS
using System.Collections.Generic;
using Godot;

/// <summary>
/// 图剪贴板服务。
/// </summary>
/// <remarks>
/// 复制节点时会同时记录选中节点之间的内部连线；粘贴时重新生成节点 id，
/// 并把内部连线端点映射到新 id。
/// </remarks>
public sealed class GraphClipboardService
{
    private readonly List<string> _nodeJson = new();
    private readonly List<string> _connectionJson = new();

    /// <summary>剪贴板是否为空。</summary>
    public bool IsEmpty => _nodeJson.Count == 0;

    /// <summary>复制当前 GraphEdit 中选中的节点和内部连线。</summary>
    public void Copy(GraphAsset graph, GraphEdit graphEdit)
    {
        _nodeJson.Clear();
        _connectionJson.Clear();

        if (graph == null || graphEdit == null)
            return;

        var selectedIds = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (Node child in graphEdit.GetChildren())
        {
            if (child is GraphNode graphNode && graphNode.Selected)
                selectedIds.Add(graphNode.Name);
        }

        foreach (GraphNodeData node in graph.Nodes)
        {
            if (selectedIds.Contains(node.Id))
                _nodeJson.Add(GraphJsonHelper.Serialize(node));
        }

        foreach (GraphConnection connection in graph.Connections)
        {
            if (selectedIds.Contains(connection.FromNode) && selectedIds.Contains(connection.ToNode))
                _connectionJson.Add(GraphJsonHelper.Serialize(connection));
        }
    }

    /// <summary>创建粘贴数据。</summary>
    public GraphClipboardPasteData CreatePasteData(Vector2 offset)
    {
        var paste = new GraphClipboardPasteData();
        var idMap = new Dictionary<string, string>(System.StringComparer.Ordinal);

        foreach (string json in _nodeJson)
        {
            GraphNodeData node = GraphJsonHelper.Deserialize<GraphNodeData>(json);
            if (node == null)
                continue;

            string oldId = node.Id;
            node.Id = GeneratePasteId();
            node.Position += offset;
            idMap[oldId] = node.Id;
            paste.Nodes.Add(node);
        }

        foreach (string json in _connectionJson)
        {
            GraphConnection connection = GraphJsonHelper.Deserialize<GraphConnection>(json);
            if (connection == null)
                continue;

            if (!idMap.TryGetValue(connection.FromNode, out string newFrom) ||
                !idMap.TryGetValue(connection.ToNode, out string newTo))
            {
                continue;
            }

            connection.FromNode = newFrom;
            connection.ToNode = newTo;
            paste.Connections.Add(connection);
        }

        return paste;
    }

    private static string GeneratePasteId()
    {
        ulong time = Time.GetTicksUsec();
        uint rand = (uint)GD.Randi();
        return $"paste_{time:x}_{rand:x}";
    }
}

/// <summary>粘贴结果数据。</summary>
public sealed class GraphClipboardPasteData
{
    /// <summary>新节点。</summary>
    public List<GraphNodeData> Nodes { get; } = new();

    /// <summary>新连线。</summary>
    public List<GraphConnection> Connections { get; } = new();
}
#endif
