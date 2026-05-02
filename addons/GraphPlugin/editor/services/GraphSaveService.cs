#if TOOLS
using System.Collections.Generic;
using Godot;

/// <summary>
/// 图保存服务。
/// </summary>
public static class GraphSaveService
{
    /// <summary>
    /// 同步节点位置、执行验证并保存资源。
    /// </summary>
    public static bool Save(Window owner, GraphAsset graph, GraphEdit graphEdit, bool showDialog = true)
    {
        if (graph == null)
            return false;

        SyncNodePositions(graph, graphEdit);
        SyncEditorState(graph, graphEdit);

        if (!graph.Validate(out GraphValidationResult validation))
        {
            if (showDialog)
                ShowDialog(owner, "Graph Validation Error", validation.ToDisplayText());
            return false;
        }

        graph.SaveJsonFields();
        Error error = ResourceSaver.Save(graph, graph.ResourcePath);
        if (error != Error.Ok)
        {
            if (showDialog)
                ShowDialog(owner, "Graph Save Failed", $"保存失败：{error}");
            return false;
        }

        GD.Print($"[GraphSaveService] 图已保存：{graph.ResourcePath}");
        return true;
    }

    private static void SyncNodePositions(GraphAsset graph, GraphEdit graphEdit)
    {
        if (graphEdit == null)
            return;

        var nodeDict = new Dictionary<string, GraphNodeData>();
        foreach (GraphNodeData nodeData in graph.Nodes)
            nodeDict[nodeData.Id] = nodeData;

        foreach (Node child in graphEdit.GetChildren())
        {
            if (child is GraphNode graphNode &&
                nodeDict.TryGetValue(graphNode.Name, out GraphNodeData nodeData))
            {
                nodeData.Position = graphNode.PositionOffset;
            }
        }

        graph.MarkDirty();
    }

    private static void SyncEditorState(GraphAsset graph, GraphEdit graphEdit)
    {
        if (graph == null || graphEdit == null)
            return;

        graph.Document.EditorState.Zoom = graphEdit.Zoom;
        graph.Document.EditorState.ScrollOffset = graphEdit.ScrollOffset;
        graph.MarkDirty();
    }

    private static void ShowDialog(Window owner, string title, string message)
    {
        if (owner == null)
            return;

        var dialog = new AcceptDialog
        {
            Title = title,
            DialogText = message
        };
        owner.AddChild(dialog);
        dialog.PopupCentered();
    }
}
#endif
