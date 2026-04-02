#if TOOLS
using Godot;

[Tool]
public partial class GraphPlugin : EditorPlugin
{
    private GraphCanvasEditorWindow _editorWindow;
    private GraphCanvasInspectorPlugin _inspectorPlugin;

    public override void _EnterTree()
    {
        GraphNodeFactory.AutoRegisterAll();
        var nodeCount = GraphNodeFactory.GetAllRegisteredTypes().Count;
        GD.Print($"GraphCanvas: 已注册 {nodeCount} 个节点类型");

        _editorWindow = new GraphCanvasEditorWindow();
        EditorInterface.Singleton.GetBaseControl().AddChild(_editorWindow);
        _editorWindow._undoRedo = GetUndoRedo();
        _editorWindow.Hide();

        _inspectorPlugin = new GraphCanvasInspectorPlugin { Plugin = this };
        AddInspectorPlugin(_inspectorPlugin);

        GD.Print("GraphCanvas 插件已加载");
    }

    public override void _ExitTree()
    {
        RemoveInspectorPlugin(_inspectorPlugin);
        if (_editorWindow != null)
            _editorWindow.QueueFree();

        GD.Print("GraphCanvas 插件已卸载");
    }

    public void OpenGraphEditor(GraphAsset graph)
    {
        if (graph != null && _editorWindow != null)
        {
            _editorWindow.Hide();
            // 清空子图导航栈，确保每次从根图重新打开
            _editorWindow.ResetNavigation();
            _editorWindow.LoadGraph(graph);
            _editorWindow.CallDeferred(Window.MethodName.PopupCentered,new Vector2I(1200,800));
            //_editorWindow.PopupCentered(new Vector2I(1200, 800));
        }
    }
}
#endif
