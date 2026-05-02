#if TOOLS
using Godot;

[Tool]
public partial class GraphPlugin : EditorPlugin
{
    private GraphCanvasEditorWindow _editorWindow;
    private GraphCanvasInspectorPlugin _inspectorPlugin;
    private GraphRuntimeDebugEditorDebuggerPlugin _runtimeDebugDebuggerPlugin;

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

        _runtimeDebugDebuggerPlugin = new GraphRuntimeDebugEditorDebuggerPlugin();
        AddDebuggerPlugin(_runtimeDebugDebuggerPlugin);

        GD.Print("GraphCanvas plugin loaded");
    }

    public override void _ExitTree()
    {
        if (_runtimeDebugDebuggerPlugin != null)
            RemoveDebuggerPlugin(_runtimeDebugDebuggerPlugin);

        RemoveInspectorPlugin(_inspectorPlugin);
        if (_editorWindow != null)
            _editorWindow.QueueFree();

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
            CreateConnection = () => new GraphConnection()
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
