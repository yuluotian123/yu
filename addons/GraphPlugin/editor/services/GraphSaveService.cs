#if TOOLS
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Persists graph data to either its external resource or the current scene.
/// </summary>
public static class GraphSaveService
{
    public static bool Save(Window owner, GraphAsset graph, GraphEdit graphEdit, bool showDialog = true)
    {
        if (graph == null || !GodotObject.IsInstanceValid(graph))
            return false;

        if (!graph.TryLoadDocument(out string loadError))
        {
            if (showDialog)
                ShowDialog(owner, "Graph Load Failed", loadError);
            return false;
        }

        SyncNodePositions(graph, graphEdit);
        SyncEditorState(graph, graphEdit);

        if (!graph.Validate(out GraphValidationResult validation))
        {
            if (showDialog)
                ShowDialog(owner, "Graph Validation Error", validation.ToDisplayText());
            return false;
        }

        return SaveGraphResource(owner, graph, showDialog);
    }

    public static bool SaveGraphResource(Window owner, GraphAsset graph, bool showDialog = true)
    {
        if (graph == null || !GodotObject.IsInstanceValid(graph))
            return false;

        try
        {
            graph.SaveJsonFields();
        }
        catch (Exception exception)
        {
            if (showDialog)
                ShowDialog(owner, "Graph Save Failed", exception.Message);
            return false;
        }
        Error error = SaveResource(graph);
        if (error != Error.Ok)
        {
            // SaveJsonFields clears the in-memory dirty flag before Godot writes
            // the resource. Keep the edit pending when the scene/resource save
            // itself fails so a later retry still has an unsaved state to report.
            graph.MarkDirty();
            if (IsInlineResource(graph))
            {
                try { EditorInterface.Singleton.MarkSceneAsUnsaved(); }
                catch { }
            }
            if (showDialog)
                ShowDialog(owner, "Graph Save Failed",
                    $"Could not save graph to {GetSaveTarget(graph)}: {error}.\nChanges remain unsaved.");
            return false;
        }

        GD.Print($"[GraphSaveService] Graph saved: {GetSaveTarget(graph)}");
        return true;
    }

    private static Error SaveResource(GraphAsset graph)
    {
        if (!IsInlineResource(graph))
        {
            if (string.IsNullOrWhiteSpace(graph.ResourcePath))
                return Error.InvalidParameter;
            return ResourceSaver.Save(graph, graph.ResourcePath);
        }

        try
        {
            Node sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();
            if (sceneRoot == null || !GodotObject.IsInstanceValid(sceneRoot) ||
                string.IsNullOrWhiteSpace(sceneRoot.SceneFilePath))
                return Error.Failed;
            if (!IsOwnedByScene(graph, sceneRoot))
                return Error.Failed;

            EditorInterface.Singleton.MarkSceneAsUnsaved();
            return EditorInterface.Singleton.SaveScene();
        }
        catch (Exception exception)
        {
            GD.PushError($"[GraphSaveService] Inline graph scene save failed: {exception.Message}");
            return Error.Failed;
        }
    }

    private static bool IsInlineResource(GraphAsset graph) =>
        graph != null && GodotObject.IsInstanceValid(graph) &&
        (graph.IsBuiltIn() ||
         graph.ResourcePath?.Contains("::", StringComparison.Ordinal) == true ||
         (graph.ResourceLocalToScene && string.IsNullOrWhiteSpace(graph.ResourcePath)));

    private static bool IsOwnedByScene(GraphAsset graph, Node sceneRoot)
    {
        if (graph == null || sceneRoot == null)
            return false;

        string resourcePath = graph.ResourcePath;
        int separator = resourcePath?.IndexOf("::", StringComparison.Ordinal) ?? -1;
        if (separator < 0)
            return string.IsNullOrWhiteSpace(resourcePath) &&
                   GraphComponentPanel.SceneContainsGraphHost(sceneRoot, graph);

        string ownerScenePath = resourcePath.Substring(0, separator);
        return string.Equals(ownerScenePath, sceneRoot.SceneFilePath, StringComparison.Ordinal);
    }

    private static string GetSaveTarget(GraphAsset graph)
    {
        if (!IsInlineResource(graph))
            return graph.ResourcePath;
        try
        {
            return EditorInterface.Singleton.GetEditedSceneRoot()?.SceneFilePath ?? graph.ResourcePath;
        }
        catch
        {
            return graph.ResourcePath;
        }
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
