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
        return graph != null;
    }

    public virtual string GetSubGraphTypeName()
    {
        return nameof(GraphAsset);
    }

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
            Text = string.IsNullOrEmpty(SubGraphPath) ? "Unbound SubGraph" : GetDisplayName(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        vbox.AddChild(pathLabel);

        context.GraphNode.AddChild(vbox);
    }
}
