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

    public string graphName => ResourcePath.GetFile();

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
            var entryNode = Nodes.Find(t => t.NodeType == "Entry");
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
