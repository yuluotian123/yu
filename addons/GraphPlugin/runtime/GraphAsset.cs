using System.Collections.Generic;
using System.Linq;
using Godot;

[Tool]
[GlobalClass]
public partial class GraphAsset : Resource
{
    [Export] public string NodesJson { get; set; } = "[]";
    [Export] public string ConnectionsJson { get; set; } = "[]";
    [Export] public string BlackboardJson { get; set; } = "[]";

    public string graphName => ResourcePath.GetFile().Split('.')[0];

    private List<GraphNodeData> _nodes;
    private List<GraphConnection> _connections;
    private List<GraphBlackboardEntry> _blackboardEntries;

    public List<GraphNodeData> Nodes
    {
        get
        {
            if (_nodes == null)
                LoadFromJson();
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
            if (_connections == null)
                LoadFromJson();
            return _connections;
        }
        set
        {
            _connections = value;
            SaveToJson();
        }
    }

    public List<GraphBlackboardEntry> BlackboardEntries
    {
        get
        {
            if (_blackboardEntries == null)
                LoadFromJson();
            return _blackboardEntries;
        }
        set
        {
            _blackboardEntries = value;
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

    private string _graphType;
    public virtual string GraphType
    {
        get => _graphType ?? GetType().Name;
        set => _graphType = value;
    }

    public virtual List<string> GetAllowedNodeTypes() => GraphNodeFactory.GetNodesForGraphType(GraphType);
    public virtual GraphConnection CreateConnection() => new GraphConnection();
    public virtual List<Control> GetCustomToolbarControls() => new();
    public virtual string GetEditorTitle() => GraphType + " Editor";

    public void SaveToJson()
    {
        if (_blackboardEntries == null)
            _blackboardEntries = GraphJsonHelper.DeserializeList<GraphBlackboardEntry>(BlackboardJson);

        GD.Print("Current node count: " + Nodes.Count());
        NodesJson = GraphJsonHelper.SerializeList(_nodes ?? new List<GraphNodeData>());
        ConnectionsJson = GraphJsonHelper.SerializeList(_connections ?? new List<GraphConnection>());
        BlackboardJson = GraphJsonHelper.SerializeList(_blackboardEntries ?? new List<GraphBlackboardEntry>());
    }

    public void LoadFromJson()
    {
        _nodes = GraphJsonHelper.DeserializeList<GraphNodeData>(NodesJson);
        _connections = GraphJsonHelper.DeserializeList<GraphConnection>(ConnectionsJson);
        _blackboardEntries = GraphJsonHelper.DeserializeList<GraphBlackboardEntry>(BlackboardJson);
        TopologicalSortNodes();
    }

    private void TopologicalSortNodes()
    {
        if (_nodes == null || _nodes.Count == 0)
            return;

        var nodeIndex = new Dictionary<string, int>();
        for (int i = 0; i < _nodes.Count; i++)
            nodeIndex[_nodes[i].Id] = i;

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
            if (!visited.Add(id))
                continue;

            if (nodeIndex.TryGetValue(id, out var idx))
                sorted.Add(_nodes[idx]);

            foreach (var next in adj[id])
            {
                inDegree[next]--;
                if (inDegree[next] == 0)
                    queue.Enqueue(next);
            }
        }

        foreach (var node in _nodes)
        {
            if (!visited.Contains(node.Id))
                sorted.Add(node);
        }

        _nodes = sorted;
    }

    public bool ConnectNodes(string fromNode, int fromPort, string toNode, int toPort)
    {
        if (HasConnection(fromNode, fromPort, toNode, toPort))
        {
            GD.PushWarning($"Connection already exists: {fromNode}:{fromPort} -> {toNode}:{toPort}");
            return false;
        }

        var fromNodeData = FindNodeById(fromNode);
        if (fromNodeData != null)
        {
            int maxOut = fromNodeData.GetOutputMaxConnections(fromPort);
            if (maxOut >= 0)
            {
                int outCount = Connections.Count(c => c.FromNode == fromNode && c.FromPort == fromPort);
                if (outCount >= maxOut)
                {
                    GD.PushWarning($"Output port {fromNode}:{fromPort} reached max connection count {maxOut}; rejecting new connection.");
                    return false;
                }
            }
        }

        var toNodeData = FindNodeById(toNode);
        if (toNodeData != null)
        {
            int maxIn = toNodeData.GetInputMaxConnections(toPort);
            if (maxIn >= 0)
            {
                int inCount = Connections.Count(c => c.ToNode == toNode && c.ToPort == toPort);
                if (inCount >= maxIn)
                {
                    GD.PushWarning($"Input port {toNode}:{toPort} reached max connection count {maxIn}; rejecting new connection.");
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
