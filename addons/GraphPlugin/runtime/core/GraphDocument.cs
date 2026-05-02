using System.Collections.Generic;
using Godot;

/// <summary>
/// GraphPlugin V2 的唯一图文档模型。
/// </summary>
/// <remarks>
/// <para>
/// V2 不再把节点、连线、黑板拆成多个导出 JSON 字段，而是把完整图数据写入
/// <see cref="GraphAsset.GraphJson"/>。这样保存、验证、迁移和运行时索引都只需要面对
/// 一个稳定的数据入口。
/// </para>
/// <para>
/// 这个类型只表达“图是什么”，不包含 Godot 编辑器控件，也不包含 Flow/State 的运行语义。
/// </para>
/// </remarks>
public sealed class GraphDocument
{
    /// <summary>当前 GraphJson 格式版本。后续格式变化时只递增这个值。</summary>
    public int SchemaVersion { get; set; } = 2;

    /// <summary>图类型名称，例如 FlowGraph、StateGraph、HfsmGraph、MissionGraph。</summary>
    public string GraphType { get; set; } = string.Empty;

    /// <summary>图内所有节点数据。</summary>
    public List<GraphNodeData> Nodes { get; set; } = new();

    /// <summary>图内所有连线数据。</summary>
    public List<GraphConnection> Connections { get; set; } = new();

    /// <summary>图本地黑板条目。</summary>
    public List<GraphBlackboardEntry> BlackboardEntries { get; set; } = new();

    /// <summary>纯编辑器状态，例如缩放、滚动位置。运行时可以忽略。</summary>
    public GraphEditorState EditorState { get; set; } = new();
}

/// <summary>
/// 图编辑器自己的轻量状态。
/// </summary>
public sealed class GraphEditorState
{
    /// <summary>GraphEdit 的滚动偏移，供后续恢复视口使用。</summary>
    public Vector2 ScrollOffset { get; set; } = Vector2.Zero;

    /// <summary>GraphEdit 缩放值，默认 1。</summary>
    public float Zoom { get; set; } = 1f;
}
