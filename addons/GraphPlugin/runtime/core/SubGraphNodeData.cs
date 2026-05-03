using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Node data for a subgraph reference. The editor injects enter/bind buttons.
/// </summary>
public partial class SubGraphNodeData : GraphNodeData
{
    /// <summary>Resource path for the child graph, usually a .tres file.</summary>
    public string SubGraphPath { get; set; } = string.Empty;

    private GraphAsset _cachedSubGraph;

    public override List<string> GetGraphTypes()
        => new List<string> { "All" };

    public override string GetDisplayName()
    {
        if (!string.IsNullOrEmpty(SubGraphPath))
        {
            string fileName = SubGraphPath.GetFile().GetBaseName();
            return string.IsNullOrEmpty(fileName) ? "SubGraph" : fileName;
        }

        return "SubGraph (Unbound)";
    }

    public override Color GetNodeColor() => new Color(0.4f, 0.6f, 1.0f);

    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;

    public override int GetInputMaxConnections(int port) => 1;
    public override int GetOutputMaxConnections(int port) => -1;

    public virtual GraphAsset GetSubGraph()
    {
        if (_cachedSubGraph != null)
            return _cachedSubGraph;

        if (string.IsNullOrEmpty(SubGraphPath))
            return null;

        if (!ResourceLoader.Exists(SubGraphPath))
        {
            GD.PushWarning($"[SubGraph] Subgraph resource does not exist: {SubGraphPath}");
            return null;
        }

        _cachedSubGraph = ResourceLoader.Load<GraphAsset>(SubGraphPath);
        return _cachedSubGraph;
    }

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
        return graph != null && GetSubGraphType().IsInstanceOfType(graph);
    }

    /// <summary>
    /// 编辑器资源槽允许绑定的子图资源类型。
    /// </summary>
    public virtual Type GetSubGraphType()
    {
        return typeof(GraphAsset);
    }

    public override void CreateNodeUI(GraphEditorContext context)
    {
        var vbox = new VBoxContainer
        {
            Name = "SubGraphContent",
            CustomMinimumSize = new Vector2(160, 0)
        };
        vbox.AddThemeConstantOverride("separation", 3);

        vbox.AddChild(new Label
        {
            Text = "SubGraph",
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var pathLabel = new Label
        {
            Name = "PathLabel",
            Text = GetCompactSubGraphLabel(),
            ClipText = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = string.IsNullOrEmpty(SubGraphPath) ? "Unbound SubGraph" : SubGraphPath
        };
        vbox.AddChild(pathLabel);

        context.GraphNode.AddChild(vbox);
    }

    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(260f, 0f) };
        root.AddThemeConstantOverride("separation", 6);
        root.AddChild(new Label { Text = "SubGraph" });
        root.AddChild(CreateInspectorInfoRow("Path", string.IsNullOrWhiteSpace(SubGraphPath) ? "Unbound" : SubGraphPath));
        return root;
    }

    public override void CreateUI(GraphEditorContext context)
    {
        CreateNodeUI(context);
    }

    public string GetCompactSubGraphLabel()
    {
        if (string.IsNullOrWhiteSpace(SubGraphPath))
            return "Unbound";

        string fileName = SubGraphPath.GetFile().GetBaseName();
        return string.IsNullOrWhiteSpace(fileName) ? "Bound" : fileName;
    }
}
