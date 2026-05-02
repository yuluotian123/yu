using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// 图资源基类。
/// </summary>
/// <remarks>
/// <para>
/// V2 中图资源只导出一个 <see cref="GraphJson"/> 字段，内部反序列化为
/// <see cref="GraphDocument"/>。所有节点、连线、本地黑板和编辑器状态都从文档读取。
/// </para>
/// <para>
/// 子类通常只需要覆盖 <see cref="GraphType"/>、<see cref="CreateConnection"/> 和
/// <see cref="GetEditorTitle"/>。运行时查询请优先使用本类的查询方法，它们会走
/// <see cref="GraphRuntimeIndex"/> 缓存。
/// </para>
/// </remarks>
[Tool]
[GlobalClass]
public partial class GraphAsset : Resource
{
    private string _graphJson = string.Empty;
    private GraphDocument _document;
    private GraphRuntimeIndex _runtimeIndex;
    private bool _dirty;

    /// <summary>V2 唯一图存储字段。</summary>
    [Export(PropertyHint.MultilineText)]
    public string GraphJson
    {
        get => _graphJson;
        set
        {
            _graphJson = value ?? string.Empty;
            _document = null;
            _runtimeIndex = null;
            _dirty = false;
        }
    }

    /// <summary>资源文件名，不含扩展名。</summary>
    public string graphName => ResourcePath.GetFile().Split('.')[0];

    /// <summary>图类型。子类应返回稳定常量。</summary>
    public virtual string GraphType { get; set; } = string.Empty;

    /// <summary>完整文档对象。</summary>
    public GraphDocument Document
    {
        get
        {
            EnsureDocument();
            return _document;
        }
    }

    /// <summary>节点列表。</summary>
    public List<GraphNodeData> Nodes
    {
        get => Document.Nodes;
        set
        {
            Document.Nodes = value ?? new List<GraphNodeData>();
            MarkDirty();
        }
    }

    /// <summary>连线列表。</summary>
    public List<GraphConnection> Connections
    {
        get => Document.Connections;
        set
        {
            Document.Connections = value ?? new List<GraphConnection>();
            MarkDirty();
        }
    }

    /// <summary>图本地黑板。</summary>
    public List<GraphBlackboardEntry> BlackboardEntries
    {
        get => Document.BlackboardEntries;
        set
        {
            Document.BlackboardEntries = value ?? new List<GraphBlackboardEntry>();
            MarkDirty();
        }
    }

    /// <summary>图入口节点。</summary>
    public GraphNodeData PrimeNode
    {
        get
        {
            GraphNodeData entryNode = Nodes.Find(node => node.NodeType == "EntryNode" || node.NodeType == "FlowEntryNodeData");
            if (entryNode != null)
                return entryNode;

            return Nodes.FirstOrDefault(node => node?.CanBePrime() == true);
        }
    }

    /// <summary>编辑器中允许创建的节点类型名。</summary>
    public virtual List<string> GetAllowedNodeTypes() => GraphTypeRegistry.GetNodeTypeNamesForGraphType(GraphType);

    /// <summary>创建一条本图默认连线。</summary>
    public virtual GraphConnection CreateConnection() => GraphTypeRegistry.CreateConnection(GraphType);

    /// <summary>图编辑器工具栏自定义控件。</summary>
    public virtual List<Control> GetCustomToolbarControls() => new();

    /// <summary>图编辑器窗口标题。</summary>
    public virtual string GetEditorTitle() => string.IsNullOrWhiteSpace(GraphType) ? "Graph Editor" : GraphType + " Editor";

    /// <summary>标记图数据已改变，并使运行时索引失效。</summary>
    public void MarkDirty()
    {
        _dirty = true;
        _runtimeIndex = null;
    }

    /// <summary>把当前文档写回 <see cref="GraphJson"/>。</summary>
    public void SaveJsonFields()
    {
        EnsureDocument();
        _document.SchemaVersion = 2;
        _document.GraphType = GraphType;
        _graphJson = GraphJsonHelper.Serialize(_document);
        _dirty = false;
    }

    /// <summary>验证图结构。</summary>
    public bool Validate(out GraphValidationResult result)
    {
        result = GraphValidationService.Validate(this);
        return result.IsValid;
    }

    /// <summary>获取或重建运行时索引。</summary>
    public GraphRuntimeIndex GetRuntimeIndex()
    {
        _runtimeIndex ??= new GraphRuntimeIndex(this);
        return _runtimeIndex;
    }

    /// <summary>创建连线。</summary>
    public bool ConnectNodes(string fromNode, int fromPort, string toNode, int toPort)
    {
        if (HasConnection(fromNode, fromPort, toNode, toPort))
        {
            GD.PushWarning($"[GraphAsset] 连线已存在：{fromNode}:{fromPort} -> {toNode}:{toPort}");
            return false;
        }

        GraphNodeData fromNodeData = FindNodeById(fromNode);
        GraphNodeData toNodeData = FindNodeById(toNode);
        if (fromNodeData == null || toNodeData == null)
        {
            GD.PushWarning($"[GraphAsset] 连线端点节点不存在：{fromNode} -> {toNode}");
            return false;
        }

        if (fromPort < 0 || fromPort >= fromNodeData.GetOutputCount())
        {
            GD.PushWarning($"[GraphAsset] 输出端口越界：{fromNode}:{fromPort}");
            return false;
        }

        if (toPort < 0 || toPort >= toNodeData.GetInputCount())
        {
            GD.PushWarning($"[GraphAsset] 输入端口越界：{toNode}:{toPort}");
            return false;
        }

        if (fromNodeData.GetOutputPortType(fromPort) != toNodeData.GetInputPortType(toPort))
        {
            GD.PushWarning($"[GraphAsset] 端口类型不兼容：{fromNode}:{fromPort} -> {toNode}:{toPort}");
            return false;
        }

        if (fromNodeData != null)
        {
            int maxOut = fromNodeData.GetOutputMaxConnections(fromPort);
            if (maxOut >= 0 && Connections.Count(c => c.FromNode == fromNode && c.FromPort == fromPort) >= maxOut)
            {
                GD.PushWarning($"[GraphAsset] 输出端口连接数达到上限：{fromNode}:{fromPort}");
                return false;
            }
        }

        if (toNodeData != null)
        {
            int maxIn = toNodeData.GetInputMaxConnections(toPort);
            if (maxIn >= 0 && Connections.Count(c => c.ToNode == toNode && c.ToPort == toPort) >= maxIn)
            {
                GD.PushWarning($"[GraphAsset] 输入端口连接数达到上限：{toNode}:{toPort}");
                return false;
            }
        }

        GraphConnection connection = CreateConnection();
        connection.FromNode = fromNode;
        connection.FromPort = fromPort;
        connection.ToNode = toNode;
        connection.ToPort = toPort;
        Connections.Add(connection);
        MarkDirty();
        return true;
    }

    /// <summary>是否已有完全相同端点的连线。</summary>
    public bool HasConnection(string fromNode, int fromPort, string toNode, int toPort)
    {
        return Connections.Any(connection => connection.Matches(fromNode, fromPort, toNode, toPort));
    }

    /// <summary>删除节点及其关联连线。</summary>
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
        MarkDirty();
    }

    /// <summary>删除某个节点相关的全部连线。</summary>
    public void RemoveConnectionsForNode(string nodeId)
    {
        for (int i = Connections.Count - 1; i >= 0; i--)
        {
            GraphConnection connection = Connections[i];
            if (connection.FromNode == nodeId || connection.ToNode == nodeId)
                Connections.RemoveAt(i);
        }

        MarkDirty();
    }

    /// <summary>查询输入连线。</summary>
    public List<GraphConnection> GetIncomingConnections(string nodeId, int? toPort = null)
    {
        return GetRuntimeIndex().GetIncomingConnections(nodeId, toPort);
    }

    /// <summary>旧拼写兼容移除后的正确入口，保留语义名称。</summary>
    public List<GraphConnection> GetIngoingConnections(string nodeId)
    {
        return GetIncomingConnections(nodeId);
    }

    /// <summary>查询输出连线。</summary>
    public List<GraphConnection> GetOutgoingConnections(string nodeId, int? fromPort = null)
    {
        return GetRuntimeIndex().GetOutgoingConnections(nodeId, fromPort);
    }

    /// <summary>按 id 查找节点。</summary>
    public GraphNodeData FindNodeById(string nodeId)
    {
        return GetRuntimeIndex().FindNodeById(nodeId);
    }

    private void EnsureDocument()
    {
        if (_document != null)
            return;

        _document = string.IsNullOrWhiteSpace(_graphJson)
            ? new GraphDocument()
            : GraphJsonHelper.Deserialize<GraphDocument>(_graphJson) ?? new GraphDocument();

        _document.SchemaVersion = 2;
        if (string.IsNullOrWhiteSpace(_document.GraphType))
            _document.GraphType = GraphType;

        _document.Nodes ??= new List<GraphNodeData>();
        _document.Connections ??= new List<GraphConnection>();
        _document.BlackboardEntries ??= new List<GraphBlackboardEntry>();
        _document.EditorState ??= new GraphEditorState();
    }
}
