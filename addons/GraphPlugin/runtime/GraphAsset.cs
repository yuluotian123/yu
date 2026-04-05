using System.Collections.Generic;
using System.Linq;
using Godot;

[Tool]
[GlobalClass]
public partial class GraphAsset : Resource
{
    // ── Godot 序列化字段（唯一需要 Export 的字段）────────────────────────────
    [Export] public string NodesJson { get; set; } = "[]";
    [Export] public string ConnectionsJson { get; set; } = "[]";

    public string graphName => ResourcePath.GetFile().Split('.')[0];

    // ── 运行时数据（从 JSON 加载后填充）──────────────────────────────────────
    private List<GraphNodeData> _nodes;
    private List<GraphConnection> _connections;

    public List<GraphNodeData> Nodes
    {
        get
        {
            if (_nodes == null) LoadFromJson();
            return _nodes;
        }
        set
        {
            _nodes = value;
            SaveToJson();
        }
    }
    public List<GraphConnection> Connections
    {
        get
        {
            if (_connections == null) LoadFromJson();
            return _connections;
        }
        set
        {
            _connections = value;
            SaveToJson();
        }
    }

    public GraphNodeData primeNode
    {
        get
        {
            var entryNode = Nodes.Find(t => t.NodeType == "EntryNode");
            if (entryNode != null)
                return entryNode;

            foreach (var node in Nodes)
            {
                if (node.CanBePrime())
                    return node;
            }


            return null;
        }
    }
    private string? _graphType;
    public virtual string GraphType
    {
        get => _graphType ?? this.GetType().Name;
        set => _graphType = value;
    }

    public virtual List<string> GetAllowedNodeTypes() => GraphNodeFactory.GetNodesForGraphType(GraphType);
    public virtual GraphConnection CreateConnection() => new GraphConnection();
    public virtual List<Control> GetCustomToolbarControls() => new();
    public virtual string GetEditorTitle() => GraphType + " 编辑器";

    // ── JSON 序列化 / 反序列化 ────────────────────────────────────────────────

    public void SaveToJson()
    {
        GD.Print("当前节点数量：" + Nodes.Count());
        NodesJson = GraphJsonHelper.SerializeList(_nodes ?? new List<GraphNodeData>());
        ConnectionsJson = GraphJsonHelper.SerializeList(_connections ?? new List<GraphConnection>());
    }

    public void LoadFromJson()
    {
        _nodes = GraphJsonHelper.DeserializeList<GraphNodeData>(NodesJson);
        _connections = GraphJsonHelper.DeserializeList<GraphConnection>(ConnectionsJson);
        TopologicalSortNodes();
    }

    /// <summary>
    /// 根据 connections 对 nodes 进行拓扑排序，使入度为 0 的节点排在前面。
    /// </summary>
    private void TopologicalSortNodes()
    {
        if (_nodes == null || _nodes.Count == 0) return;

        var nodeIndex = new Dictionary<string, int>();
        for (int i = 0; i < _nodes.Count; i++)
            nodeIndex[_nodes[i].Id] = i;

        // 构建邻接表和入度表
        var adj = new Dictionary<string, List<string>>();
        var inDegree = new Dictionary<string, int>();
        foreach (var node in _nodes)
        {
            adj[node.Id] = new List<string>();
            inDegree[node.Id] = 0;
        }

        if (_connections != null)
        {
            foreach (var conn in _connections)
            {
                if (adj.ContainsKey(conn.FromNode) && inDegree.ContainsKey(conn.ToNode))
                {
                    adj[conn.FromNode].Add(conn.ToNode);
                    inDegree[conn.ToNode]++;
                }
            }
        }

        // Kahn 算法 BFS 拓扑排序
        var queue = new Queue<string>();
        foreach (var kv in inDegree)
        {
            if (kv.Value == 0)
                queue.Enqueue(kv.Key);
        }

        var sorted = new List<GraphNodeData>();
        var visited = new HashSet<string>();
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!visited.Add(id)) continue;
            if (nodeIndex.TryGetValue(id, out var idx))
                sorted.Add(_nodes[idx]);

            foreach (var next in adj[id])
            {
                inDegree[next]--;
                if (inDegree[next] == 0)
                    queue.Enqueue(next);
            }
        }

        // 未被排序到的节点（环或孤立）追加到末尾
        foreach (var node in _nodes)
        {
            if (!visited.Contains(node.Id))
                sorted.Add(node);
        }

        _nodes = sorted;
    }

    // ── 图操作方法 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 尝试建立连接。成功返回 true，被规则拒绝返回 false。
    /// </summary>
    public bool ConnectNodes(string fromNode, int fromPort, string toNode, int toPort)
    {
        if (HasConnection(fromNode, fromPort, toNode, toPort))
        {
            GD.PushWarning($"连接已存在: {fromNode}:{fromPort} -> {toNode}:{toPort}");
            return false;
        }

        // ── 检查输出端口最大链接数 ────────────────────────────────────────────
        var fromNodeData = FindNodeById(fromNode);
        if (fromNodeData != null)
        {
            int maxOut = fromNodeData.GetOutputMaxConnections(fromPort);
            if (maxOut >= 0)
            {
                int outCount = Connections.Count(c => c.FromNode == fromNode && c.FromPort == fromPort);
                if (outCount >= maxOut)
                {
                    GD.PushWarning($"输出端口 {fromNode}:{fromPort} 已达最大连接数 {maxOut}，拒绝建立新连接");
                    return false;
                }
            }
        }

        // ── 检查输入端口最大链接数 ────────────────────────────────────────────
        var toNodeData = FindNodeById(toNode);
        if (toNodeData != null)
        {
            int maxIn = toNodeData.GetInputMaxConnections(toPort);
            if (maxIn >= 0)
            {
                int inCount = Connections.Count(c => c.ToNode == toNode && c.ToPort == toPort);
                if (inCount >= maxIn)
                {
                    GD.PushWarning($"输入端口 {toNode}:{toPort} 已达最大连接数 {maxIn}，拒绝建立新连接");
                    return false;
                }
            }
        }

        var conn = CreateConnection();
        conn.FromNode = fromNode;
        conn.FromPort = fromPort;
        conn.ToNode = toNode;
        conn.ToPort = toPort;
        Connections.Add(conn);
        SaveToJson();
        return true;
    }

    public bool HasConnection(string fromNode, int fromPort, string toNode, int toPort)
    {
        foreach (var conn in Connections)
        {
            if (conn.FromNode == fromNode && conn.FromPort == fromPort &&
                conn.ToNode == toNode && conn.ToPort == toPort)
                return true;
        }
        return false;
    }

    public void RemoveNode(string nodeId)
    {
        for (int i = Nodes.Count - 1; i >= 0; i--)
        {
            if (Nodes[i].Id == nodeId)
            {
                Nodes.RemoveAt(i);
                break;
            }
        }
        RemoveConnectionsForNode(nodeId);
        SaveToJson();
    }

    public void RemoveConnectionsForNode(string nodeId)
    {
        for (int i = Connections.Count - 1; i >= 0; i--)
        {
            var conn = Connections[i];
            if (conn.FromNode == nodeId || conn.ToNode == nodeId)
                Connections.RemoveAt(i);
        }
        SaveToJson();
    }

    public List<GraphConnection> GetIngoingConnections(string nodeId)
    {
        var result = new List<GraphConnection>();
        foreach (var conn in Connections)
        {
            if (conn.FromNode == nodeId)
                result.Add(conn);
        }
        return result;
    }

    public List<GraphConnection> GetOutgoingConnections(string nodeId)
    {
        var result = new List<GraphConnection>();
        foreach (var conn in Connections)
        {
            if (conn.FromNode == nodeId)
                result.Add(conn);
        }
        return result;
    }

    public GraphNodeData FindNodeById(string nodeId)
    {
        foreach (var node in Nodes)
        {
            if (node.Id == nodeId)
                return node;
        }
        return null;
    }
}
