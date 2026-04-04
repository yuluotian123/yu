#if TOOLS
using Godot;
using Godot.Collections;
using System.Collections.Generic;

/// <summary>
/// 剪贴板操作
/// </summary>
public partial class GraphCanvasEditorWindow
{
    private void OnCopyNodes()
    {
        _clipboard.Clear();
        var nodeDict = new System.Collections.Generic.Dictionary<string, GraphNodeData>();
        foreach (var nodeData in _currentGraph.Nodes)
            nodeDict[nodeData.Id] = nodeData;
        foreach (var child in _graphEdit.GetChildren())
        {
            if (child is GraphNode gn && gn.Selected)
            {
                if (nodeDict.TryGetValue(gn.Name, out var nodeData))
                {
                    var entry = new Dictionary
                    {
                        ["type"] = nodeData.NodeType,
                        ["position"] = gn.PositionOffset,
                        ["nodeJson"] = GraphJsonHelper.Serialize(nodeData)
                    };
                    _clipboard.Add(entry);
                }
            }
        }
    }

    private void OnPasteNodes()
    {
        if (_clipboard.Count == 0) return;
        var offset = new Vector2(50, 50);
        
        if (_undoRedo != null)
        {
            _undoRedo.CreateAction("粘贴节点");
            foreach (var nodeInfo in _clipboard)
            {
                var nodeType = nodeInfo["type"].AsString();
                var newPosition = nodeInfo["position"].AsVector2() + offset;
                var tempData = GraphNodeFactory.CreateNodeData(_currentGraph.graphName,nodeType);
                _undoRedo.AddDoMethod(this, MethodName.DoAddNode, nodeType, tempData.Id, newPosition);
                _undoRedo.AddUndoMethod(this, MethodName.DoRemoveNode, new StringName(tempData.Id));
            }
            _undoRedo.CommitAction();
        }
        else
        {
            foreach (var nodeInfo in _clipboard)
            {
                var nodeType = nodeInfo["type"].AsString();
                var newPosition = nodeInfo["position"].AsVector2() + offset;
                var tempData = GraphNodeFactory.CreateNodeData(_currentGraph.graphName,nodeType);
                DoAddNode(nodeType, tempData.Id, newPosition);
            }
        }
    }
}
#endif
