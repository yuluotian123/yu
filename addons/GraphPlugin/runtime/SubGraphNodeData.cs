using System.Collections.Generic;
using Godot;

/// <summary>
/// 子图节点数据 —— 内部持有一个独立的 GraphAsset 资源引用。
/// 在编辑器中可通过双击节点或节点上的"进入子图"按钮进入子图编辑。
/// "进入子图"按钮由编辑器层（GraphCanvasEditorWindow）在 CreateNodeFromData 时注入，
/// 因此此类不依赖任何编辑器命名空间。
///
/// 运行时通过 GetSubGraph() 加载子图资源，Execute() 可遍历子图节点执行逻辑。
/// </summary>
[Tool]
public partial class SubGraphNodeData : GraphNodeData
{
    /// <summary>子图资源的文件路径（.tres）</summary>
    public string SubGraphPath { get; set; } = "";

    /// <summary>该子图节点对外暴露的输入端口数量</summary>
    public int InputPortCount { get; set; } = 1;

    /// <summary>该子图节点对外暴露的输出端口数量</summary>
    public int OutputPortCount { get; set; } = 1;

    // ── 运行时缓存（不序列化）────────────────────────────────────────────────
    private GraphAsset _cachedSubGraph = null;

    public SubGraphNodeData()
    {
        NodeType = "SubGraph";
    }

    public override List<string> GetGraphTypes()
        => new List<string> { "All" };

    public override string GetDisplayName()
    {
        if (!string.IsNullOrEmpty(SubGraphPath))
        {
            var fileName = SubGraphPath.GetFile().GetBaseName();
            return string.IsNullOrEmpty(fileName) ? "子图" : fileName;
        }
        return "子图（未绑定）";
    }

    public override Color GetNodeColor() => new Color(0.4f, 0.6f, 1.0f);

    public override int GetInputCount() => InputPortCount;
    public override int GetOutputCount() => OutputPortCount;

    public override int GetInputMaxConnections(int port) => 1;
    public override int GetOutputMaxConnections(int port) => -1;

    // ── 子图资源访问 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 获取子图资源（带缓存）。
    /// 路径未设置或资源不存在时返回 null。
    /// </summary>
    public GraphAsset GetSubGraph()
    {
        if (_cachedSubGraph != null)
            return _cachedSubGraph;

        if (string.IsNullOrEmpty(SubGraphPath))
            return null;

        if (!ResourceLoader.Exists(SubGraphPath))
        {
            GD.PushWarning($"[SubGraph] 子图资源不存在: {SubGraphPath}");
            return null;
        }

        _cachedSubGraph = ResourceLoader.Load<GraphAsset>(SubGraphPath);
        return _cachedSubGraph;
    }

    /// <summary>
    /// 清除缓存的子图资源（路径变更后调用）。
    /// </summary>
    public void InvalidateCache()
    {
        _cachedSubGraph = null;
    }

    // ── 执行 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 默认执行实现：依次执行子图中的所有节点。
    /// 子类可覆盖此方法实现更复杂的执行顺序（如拓扑排序）。
    /// </summary>
    public override void Execute()
    {
        var subGraph = GetSubGraph();
        if (subGraph == null)
        {
            GD.PushWarning($"[SubGraph] 节点 {Id} 无法执行：子图未绑定或加载失败");
            return;
        }

        GD.Print($"[SubGraph] 开始执行子图: {GetDisplayName()}");
        foreach (var nodeData in subGraph.Nodes)
            nodeData.Execute();
        GD.Print($"[SubGraph] 子图执行完毕: {GetDisplayName()}");
    }

    /// <summary>
    /// 验证子图是否可用。
    /// </summary>
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(SubGraphPath))
        {
            GD.PushWarning($"[SubGraph] 节点 {Id} 验证失败：未绑定子图路径");
            return false;
        }
        return GetSubGraph() != null;
    }

    // ── UI ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// 创建节点 UI（纯 runtime 内容）。
    /// 编辑器专属控件（进入子图按钮）由 GraphCanvasEditorWindow 在加载图时额外注入。
    /// </summary>
    public override void CreateUI(GraphNode node)
    {
        var vbox = new VBoxContainer();
        vbox.Name = "SubGraphContent";
        vbox.CustomMinimumSize = new Vector2(160, 0);

        var pathLabel = new Label
        {
            Text = string.IsNullOrEmpty(SubGraphPath) ? "⚠ 未绑定子图" : $"📄 {GetDisplayName()}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        pathLabel.Name = "PathLabel";
        vbox.AddChild(pathLabel);

        node.AddChild(vbox);
    }
}
