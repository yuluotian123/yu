#if TOOLS
using Godot;

[Tool]
public partial class GraphPlugin : EditorPlugin
{
    private GraphCanvasEditorWindow _editorWindow;
    private GraphCanvasInspectorPlugin _inspectorPlugin;

    public override void _EnterTree()
    {
        RegisterBuiltInGraphTypes();
        GraphTypeRegistry.AutoRegisterAll();
        var nodeCount = GraphTypeRegistry.GetAllRegisteredTypes().Count;
        GD.Print($"GraphCanvas: registered {nodeCount} node types");

        _editorWindow = new GraphCanvasEditorWindow();
        EditorInterface.Singleton.GetBaseControl().AddChild(_editorWindow);
        _editorWindow._undoRedo = GetUndoRedo();
        _editorWindow.Hide();

        _inspectorPlugin = new GraphCanvasInspectorPlugin { Plugin = this };
        AddInspectorPlugin(_inspectorPlugin);

        GD.Print("GraphCanvas plugin loaded");
    }

    public override void _ExitTree()
    {
        RemoveInspectorPlugin(_inspectorPlugin);
        if (_editorWindow != null)
        {
            GraphEditorSignalCleanup.DisconnectSubtree(_editorWindow);
            _editorWindow.QueueFree();
            _editorWindow = null;
        }

        GD.Print("GraphCanvas plugin unloaded");
    }

    public void OpenGraphEditor(GraphAsset graph)
    {
        if (graph != null && _editorWindow != null)
        {
            _editorWindow.Hide();
            _editorWindow.ResetNavigation();
            _editorWindow.LoadGraph(graph);
            _editorWindow.CallDeferred(Window.MethodName.PopupCentered, new Vector2I(1200, 800));
        }
    }

    private static void RegisterBuiltInGraphTypes()
    {
        GraphTypeRegistry.RegisterGraphType(new GraphTypeDefinition
        {
            GraphType = FlowGraphAsset.GraphTypeName,
            DisplayName = "FlowGraph",
            CreateConnection = () => new FlowConnection()
        });

        GraphTypeRegistry.RegisterGraphType(new GraphTypeDefinition
        {
            GraphType = StateGraphAsset.GraphTypeName,
            DisplayName = "StateGraph",
            CreateConnection = () => new StateTransitionConnection()
        });
    }
}
#endif
