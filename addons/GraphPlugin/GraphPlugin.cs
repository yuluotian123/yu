#if TOOLS
using System;
using Godot;

[Tool]
public partial class GraphPlugin : EditorPlugin
{
    private GraphCanvasEditorWindow _editorWindow;
    private GraphCanvasInspectorPlugin _inspectorPlugin;
    private Button _toolbarButton;

    public override void _EnterTree()
    {
        _inspectorPlugin = new GraphCanvasInspectorPlugin { Plugin = this };
        AddInspectorPlugin(_inspectorPlugin);
        CreateToolbarButton();

        try
        {
            RegisterBuiltInGraphTypes();
            GraphTypeRegistry.AutoRegisterAll();
            int nodeCount = GraphTypeRegistry.GetAllRegisteredTypes().Count;
            GD.Print($"GraphCanvas: registered {nodeCount} node types");
        }
        catch (Exception ex)
        {
            GD.PushError($"[GraphPlugin] Node registration failed: {ex}");
        }

        try
        {
            _editorWindow = new GraphCanvasEditorWindow();
            EditorInterface.Singleton.GetBaseControl().AddChild(_editorWindow);
            _editorWindow._undoRedo = GetUndoRedo();
            _editorWindow.Hide();
        }
        catch (Exception ex)
        {
            GD.PushError($"[GraphPlugin] Editor window initialization failed: {ex}");
            _editorWindow = null;
        }

        GD.Print("GraphCanvas plugin loaded");
    }

    public override void _ExitTree()
    {
        DestroyToolbarButton();
        RemoveInspectorPlugin(_inspectorPlugin);
        if (_editorWindow != null)
        {
            GraphEditorSignalCleanup.DisconnectSubtree(_editorWindow);
            _editorWindow.QueueFree();
            _editorWindow = null;
        }

        GD.Print("GraphCanvas plugin unloaded");
    }

    public override void _Notification(int what)
    {
        if (what != NotificationExtensionReloaded)
            return;

        DestroyToolbarButton();
        CreateToolbarButton();
        GD.Print("[GraphPlugin] C# extension reloaded; toolbar restored.");
    }

    public void OpenGraphEditor(GraphAsset graph, GodotObject source = null)
    {
        if (graph == null)
            return;

        EnsureEditorWindow();
        if (_editorWindow == null)
            return;

        _editorWindow.Hide();
        _editorWindow.ResetNavigation();
        _editorWindow.LoadGraph(graph, source);
        _editorWindow.CallDeferred(Window.MethodName.PopupCentered, new Vector2I(1200, 800));
    }

    private void EnsureEditorWindow()
    {
        if (_editorWindow != null && GodotObject.IsInstanceValid(_editorWindow))
            return;

        try
        {
            _editorWindow = new GraphCanvasEditorWindow();
            EditorInterface.Singleton.GetBaseControl().AddChild(_editorWindow);
            _editorWindow._undoRedo = GetUndoRedo();
            _editorWindow.Hide();
        }
        catch (Exception ex)
        {
            GD.PushError($"[GraphPlugin] Editor window initialization failed: {ex}");
            _editorWindow = null;
        }
    }

    private void CreateToolbarButton()
    {
        _toolbarButton = new Button
        {
            Text = "Open Graph",
            TooltipText = "Open the graph selected in the Inspector",
            FocusMode = Control.FocusModeEnum.None
        };
        _toolbarButton.Pressed += OpenSelectedGraph;
        AddControlToContainer(CustomControlContainer.Toolbar, _toolbarButton);
    }

    private void DestroyToolbarButton()
    {
        if (_toolbarButton == null || !GodotObject.IsInstanceValid(_toolbarButton))
            return;

        _toolbarButton.Pressed -= OpenSelectedGraph;
        RemoveControlFromContainer(CustomControlContainer.Toolbar, _toolbarButton);
        _toolbarButton.QueueFree();
        _toolbarButton = null;
    }

    private void OpenSelectedGraph()
    {
        GodotObject edited = EditorInterface.Singleton.GetInspector()?.GetEditedObject();
        if (edited is GraphAsset graph)
        {
            OpenGraphEditor(graph, edited);
            return;
        }

        foreach ((string _, GraphAsset value) in GraphCanvasInspectorPlugin.FindGraphProperties(edited))
        {
            OpenGraphEditor(value, edited);
            return;
        }

        GD.PushWarning("[GraphPlugin] Select a Graph resource or a component with a Graph property first.");
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

        GraphTypeRegistry.RegisterGraphType(new GraphTypeDefinition
        {
            GraphType = BehaviorTreeGraphAsset.GraphTypeName,
            DisplayName = "BehaviorTree",
            CreateConnection = () => new BehaviorTreeConnection()
        });
    }
}
#endif
