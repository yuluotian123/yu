using System.Collections.Generic;
using Godot;

/// <summary>
/// 子图节点数据。节点只保存子图资源路径；进入子图、绑定子图等编辑器按钮由
/// GraphCanvasEditorWindow 在创建节点 UI 时注入。
/// </summary>
public partial class SubGraphNodeData : GraphNodeData
{
    /// <summary>子图资源路径，通常是 .tres 文件。</summary>
    public string SubGraphPath { get; set; } = string.Empty;

    private GraphAsset _cachedSubGraph;

    public override List<string> GetGraphTypes()
        => new List<string> { "All" };

    public override string GetDisplayName()
    {
        if (!string.IsNullOrEmpty(SubGraphPath))
        {
            string fileName = SubGraphPath.GetFile().GetBaseName();
            return string.IsNullOrEmpty(fileName) ? "子图" : fileName;
        }

        return "子图（未绑定）";
    }

    public override Color GetNodeColor() => new Color(0.4f, 0.6f, 1.0f);

    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;

    public override int GetInputMaxConnections(int port) => 1;
    public override int GetOutputMaxConnections(int port) => -1;

    /// <summary>
    /// 加载子图资源。路径为空、资源不存在或类型不匹配时返回 null。
    /// </summary>
    public virtual GraphAsset GetSubGraph()
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

    /// <summary>路径改变后清除缓存。</summary>
    public virtual void InvalidateCache()
    {
        _cachedSubGraph = null;
    }

    public virtual GraphAsset CreateSubGraphAsset()
    {
        return new GraphAsset();
    }

    public virtual bool AcceptsSubGraph(GraphAsset graph)
    {
        return graph != null;
    }

    public virtual string GetSubGraphTypeName()
    {
        return nameof(GraphAsset);
    }

    /// <summary>
    /// 创建节点内部 UI。编辑器专属的进入/绑定按钮会由 GraphCanvasEditorWindow 额外注入。
    /// </summary>
    public override void CreateUI(GraphEditorContext context)
    {
        var vbox = new VBoxContainer
        {
            Name = "SubGraphContent",
            CustomMinimumSize = new Vector2(160, 0)
        };

        var pathLabel = new Label
        {
            Name = "PathLabel",
            Text = string.IsNullOrEmpty(SubGraphPath) ? "未绑定子图" : GetDisplayName(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        vbox.AddChild(pathLabel);

        context.GraphNode.AddChild(vbox);
    }
}
