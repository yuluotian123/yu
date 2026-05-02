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

        GraphClipboardPasteData paste = _clipboard.CreatePasteData(new Vector2(50, 50));
        string pasteNodesJson = GraphJsonHelper.SerializeList(paste.Nodes);
        string pasteConnectionsJson = GraphJsonHelper.SerializeList(paste.Connections);

        if (_undoRedo != null)
        {
            string snapshotNodesJson = GraphSnapshotService.CaptureNodes(_currentGraph);
            string snapshotConnsJson = GraphSnapshotService.CaptureConnections(_currentGraph);

            _undoRedo.CreateAction("Paste Graph Nodes");
            _undoRedo.AddDoMethod(this, MethodName.DoPasteSnapshot, pasteNodesJson, pasteConnectionsJson);
            _undoRedo.AddUndoMethod(this, MethodName.DoRestoreSnapshot, snapshotNodesJson, snapshotConnsJson);
            _undoRedo.CommitAction();
        }
        else
        {
            DoPasteSnapshot(pasteNodesJson, pasteConnectionsJson);
        }
    }

    private void DoPasteSnapshot(string nodesJson, string connectionsJson)
    {
        GraphSnapshotService.AddSerialized(_currentGraph, _graphEdit, CreateNodeFromData, nodesJson, connectionsJson);
    }
}
#endif
