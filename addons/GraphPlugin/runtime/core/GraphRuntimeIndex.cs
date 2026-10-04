using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 图运行时索引。
/// </summary>
/// <remarks>
/// FlowGraph、StateGraph、MissionGraph 都需要频繁按节点和端口查连线。
/// 这个索引把常见查询预先整理成字典，避免运行时每次从完整列表线性扫描。
/// </remarks>
public sealed class GraphRuntimeIndex
{
    private readonly GraphAsset _graph;
    private GraphDocument _document;
    private long _version = -1;
    private readonly Dictionary<string, GraphNodeData> _nodesById = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, List<GraphConnection>> _outgoing = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, List<GraphConnection>> _incoming = new(System.StringComparer.Ordinal);

    /// <summary>
    /// 创建索引。
    /// </summary>
    public GraphRuntimeIndex(GraphAsset graph)
    {
        _graph = graph;
        EnsureCurrent();
    }

    private void EnsureCurrent()
    {
        if (_graph == null)
            return;
        GraphDocument document = _graph.Document;
        if (ReferenceEquals(_document, document) && _version == document.StructureVersion)
            return;
        _nodesById.Clear();
        _outgoing.Clear();
        _incoming.Clear();

        foreach (GraphNodeData node in document.Nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id))
                continue;

            _nodesById[node.Id] = node;
            _outgoing[node.Id] = new List<GraphConnection>();
            _incoming[node.Id] = new List<GraphConnection>();
        }

        foreach (GraphConnection connection in document.Connections)
        {
            if (connection == null || string.IsNullOrWhiteSpace(connection.FromNode) ||
                string.IsNullOrWhiteSpace(connection.ToNode))
                continue;

            if (!_outgoing.TryGetValue(connection.FromNode, out List<GraphConnection> outgoing))
            {
                outgoing = new List<GraphConnection>();
                _outgoing[connection.FromNode] = outgoing;
            }

            if (!_incoming.TryGetValue(connection.ToNode, out List<GraphConnection> incoming))
            {
                incoming = new List<GraphConnection>();
                _incoming[connection.ToNode] = incoming;
            }

            outgoing.Add(connection);
            incoming.Add(connection);
        }
        _document = document;
        _version = document.StructureVersion;
    }

    /// <summary>查找节点。</summary>
    public GraphNodeData FindNodeById(string nodeId)
    {
        EnsureCurrent();
        return !string.IsNullOrWhiteSpace(nodeId) && _nodesById.TryGetValue(nodeId, out GraphNodeData node)
            ? node
            : null;
    }

    /// <summary>查询指定接口或基类的节点。</summary>
    public List<TNode> GetNodes<TNode>() where TNode : class
    {
        EnsureCurrent();
        var result = new List<TNode>();
        foreach (GraphNodeData node in _nodesById.Values)
        {
            if (node is TNode typed)
                result.Add(typed);
        }

        return result;
    }

    /// <summary>查询输出连线。</summary>
    public List<GraphConnection> GetOutgoingConnections(string nodeId, int? fromPort = null)
    {
        EnsureCurrent();
        if (string.IsNullOrWhiteSpace(nodeId) || !_outgoing.TryGetValue(nodeId, out List<GraphConnection> list))
            return new List<GraphConnection>();

        return fromPort.HasValue
            ? list.Where(connection => connection.FromPort == fromPort.Value).ToList()
            : new List<GraphConnection>(list);
    }

    /// <summary>查询输入连线。</summary>
    public List<GraphConnection> GetIncomingConnections(string nodeId, int? toPort = null)
    {
        EnsureCurrent();
        if (string.IsNullOrWhiteSpace(nodeId) || !_incoming.TryGetValue(nodeId, out List<GraphConnection> list))
            return new List<GraphConnection>();

        return toPort.HasValue
            ? list.Where(connection => connection.ToPort == toPort.Value).ToList()
            : new List<GraphConnection>(list);
    }
}
