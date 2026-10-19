#if TOOLS
using Godot;

/// <summary>
/// 剪贴板操作。
/// </summary>
public partial class GraphCanvasEditorWindow
{
    private void OnCopyNodes()
    {
        _clipboard.Copy(_currentGraph, _graphEdit);
    }

    private void OnPasteNodes()
    {
        if (_clipboard.IsEmpty)
            return;

        GraphClipboardPasteData paste = _clipboard.CreatePasteData(_currentGraph, new Vector2(50, 50));
        string pasteNodesJson = GraphJsonHelper.SerializeList(paste.Nodes);
        string pasteConnectionsJson = GraphJsonHelper.SerializeList(paste.Connections);

        if (_undoRedo != null)
        {
            string snapshotNodesJson = GraphSnapshotService.CaptureNodes(_currentGraph);
            string snapshotConnsJson = GraphSnapshotService.CaptureConnections(_currentGraph);

            _undoRedo.CreateAction("Paste Graph Nodes");
            _undoRedo.AddDoMethod(this, MethodName.DoPasteSnapshot, pasteNodesJson, pasteConnectionsJson, paste.Resources);
            _undoRedo.AddUndoMethod(this, MethodName.UndoPasteSnapshot, snapshotNodesJson, snapshotConnsJson, paste.Resources);
            _undoRedo.CommitAction();
        }
        else
        {
            DoPasteSnapshot(pasteNodesJson, pasteConnectionsJson, paste.Resources);
        }
    }

    private void DoPasteSnapshot(string nodesJson, string connectionsJson, Godot.Collections.Array<Resource> resources)
    {
        _currentGraph.AddNodeResources(resources);
        GraphSnapshotService.AddSerialized(_currentGraph, _graphEdit, CreateNodeFromData, nodesJson, connectionsJson);
    }

    private void UndoPasteSnapshot(string nodesJson, string connectionsJson, Godot.Collections.Array<Resource> resources)
    {
        _currentGraph.RemoveNodeResources(resources);
        DoRestoreSnapshot(nodesJson, connectionsJson);
    }
}
#endif
