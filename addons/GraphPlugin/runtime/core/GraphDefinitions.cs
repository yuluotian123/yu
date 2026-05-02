using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// 端口方向。
/// </summary>
public enum GraphPortDirection
{
    /// <summary>输入端口。</summary>
    Input,

    /// <summary>输出端口。</summary>
    Output
}

/// <summary>
/// 描述一个节点端口的编辑器和连接规则。
/// </summary>
public sealed class GraphPortDefinition
{
    /// <summary>端口显示名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Godot GraphEdit 使用的端口类型编号。</summary>
    public int PortType { get; set; }

    /// <summary>端口颜色。</summary>
    public Color Color { get; set; } = Colors.White;

    /// <summary>最大连接数。-1 表示不限制。</summary>
    public int MaxConnections { get; set; } = -1;

    /// <summary>端口方向。</summary>
    public GraphPortDirection Direction { get; set; }
}

/// <summary>
/// 描述一个可创建节点类型。
/// </summary>
/// <remarks>
/// 注册中心只保存定义并负责查询。真正创建实例时调用 <see cref="CreateNode"/>，
/// 因而“创建能力”属于节点定义本身，而不是旧式万能 Factory。
/// </remarks>
public sealed class GraphNodeDefinition
{
    /// <summary>稳定节点类型名，序列化时写入节点的 NodeType。</summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>编辑器显示名。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>节点在搜索菜单里的分类。</summary>
    public string Category { get; set; } = "General";

    /// <summary>节点可用于哪些图类型。All 表示全局可用。</summary>
    public List<string> GraphTypes { get; set; } = new();

    /// <summary>输入端口定义。</summary>
    public List<GraphPortDefinition> InputPorts { get; set; } = new();

    /// <summary>输出端口定义。</summary>
    public List<GraphPortDefinition> OutputPorts { get; set; } = new();

    /// <summary>额外搜索关键字。</summary>
    public List<string> SearchKeywords { get; set; } = new();

    /// <summary>节点数据 CLR 类型。</summary>
    public Type NodeDataType { get; set; }

    /// <summary>节点实例创建函数。</summary>
    public Func<GraphNodeData> Create { get; set; }

    /// <summary>
    /// 创建一个新的节点数据实例，并确保 NodeType 与定义一致。
    /// </summary>
    public GraphNodeData CreateNode()
    {
        GraphNodeData node = Create?.Invoke();
        if (node == null && NodeDataType != null)
            node = Activator.CreateInstance(NodeDataType) as GraphNodeData;

        node ??= new GraphNodeData();
        node.NodeType = NodeType;
        return node;
    }
}

/// <summary>
/// 描述一个图类型。
/// </summary>
public sealed class GraphTypeDefinition
{
    /// <summary>稳定图类型名。</summary>
    public string GraphType { get; set; } = string.Empty;

    /// <summary>编辑器标题。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>默认连线创建函数。</summary>
    public Func<GraphConnection> CreateConnection { get; set; } = () => new GraphConnection();
}
