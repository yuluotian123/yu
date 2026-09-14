#if TOOLS
using System;
using Godot;

[Tool]
public partial class GraphPlugin : EditorPlugin
{
    public static GraphPlugin Instance { get; private set; }

    private GraphCanvasEditorWindow _editorWindow;
    private GraphCanvasInspectorPlugin _inspectorPlugin;

    public override void _EnterTree()
    {
        Instance = this;
        _inspectorPlugin = new GraphCanvasInspectorPlugin { Plugin = this };
        AddInspectorPlugin(_inspectorPlugin);

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
        RemoveInspectorPlugin(_inspectorPlugin);
        if (_editorWindow != null)
        {
            GraphEditorSignalCleanup.DisconnectSubtree(_editorWindow);
            _editorWindow.QueueFree();
            _editorWindow = null;
        }

        GD.Print("GraphCanvas plugin unloaded");
        if (ReferenceEquals(Instance, this))
            Instance = null;
    }

    public override void _Notification(int what)
    {
        if (what != NotificationExtensionReloaded)
            return;

        GD.Print("[GraphPlugin] C# extension reloaded.");
    }

    public void OpenGraphEditor(GraphAsset graph, GodotObject source = null, string selectNodeId = null)
    {
        if (graph == null || !GodotObject.IsInstanceValid(graph))
        {
            GD.PushWarning("[GraphPlugin] The selected graph resource is no longer valid.");
            return;
        }

        if (source != null && !GodotObject.IsInstanceValid(source))
            source = null;

        // Godot changes the Inspector edited object to the nested resource
        // when the graph field itself is opened. Recover the owning animation
        // component from the currently edited scene so the editor keeps its
        // host, variables, and save context.
        source = ResolveAnimationGraphSource(graph, source);

        if (source is GameLogic.CharacterGraphComponent2D characterComponent &&
            graph is GameLogic.CharacterGraphAsset)
        {
            GraphAsset preparedGraph = characterComponent.PrepareGraphForEditor();
            if (preparedGraph != null && !ReferenceEquals(preparedGraph, graph))
            {
                graph = preparedGraph;
                EditorInterface.Singleton.MarkSceneAsUnsaved();
            }
        }

        if (source is GameLogic.CharacterAnimationComponent2D animationComponent &&
            graph is GameLogic.HfsmGraphAsset)
        {
            GraphAsset preparedGraph = animationComponent.PrepareLocomotionGraphForEditor();
            if (preparedGraph != null && !ReferenceEquals(preparedGraph, graph))
            {
                graph = preparedGraph;
                EditorInterface.Singleton.MarkSceneAsUnsaved();
            }
            else if (preparedGraph != null)
            {
                // Provider migration and editor-local flags can dirty an
                // already embedded resource without replacing its reference.
                EditorInterface.Singleton.MarkSceneAsUnsaved();
            }
        }

        EnsureEditorWindow();
        if (_editorWindow == null)
            return;

        _editorWindow.Hide();
        _editorWindow.ResetNavigation();
        _editorWindow.LoadGraph(graph, source);
        if (!string.IsNullOrWhiteSpace(selectNodeId))
            _editorWindow.CallDeferred(nameof(GraphCanvasEditorWindow.SelectNode), selectNodeId);
        _editorWindow.CallDeferred(Window.MethodName.PopupCentered, new Vector2I(1200, 800));
    }

    private static GodotObject ResolveAnimationGraphSource(GraphAsset graph, GodotObject source)
    {
        GameLogic.HfsmGraphAsset hfsmGraph = graph as GameLogic.HfsmGraphAsset;
        if (source is GameLogic.CharacterAnimationComponent2D || hfsmGraph == null)
            return source;

        Node sceneRoot;
        try
        {
            sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();
        }
        catch
        {
            return source;
        }

        if (sceneRoot == null || !GodotObject.IsInstanceValid(sceneRoot))
            return source;

        return FindAnimationComponent(sceneRoot, hfsmGraph) ?? source;
    }

    private static GameLogic.CharacterAnimationComponent2D FindAnimationComponent(
        Node node,
        GameLogic.HfsmGraphAsset graph)
    {
        if (node == null || !GodotObject.IsInstanceValid(node))
            return null;

        if (node is GameLogic.GameObject2D gameObject)
        {
            // Check both serialized components and initialized runtime
            // components. Tool scenes are not guaranteed to have run _Ready.
            if (gameObject.Components != null)
            {
                foreach (GameLogic.Component2D component in gameObject.Components)
                {
                    if (component is GameLogic.CharacterAnimationComponent2D animation &&
                        SameGraph(animation.LocomotionGraph, graph))
                        return animation;
                }
            }

            foreach (GameLogic.Component2D component in gameObject.GetAllComponents())
            {
                if (component is GameLogic.CharacterAnimationComponent2D animation &&
                    SameGraph(animation.LocomotionGraph, graph))
                    return animation;
            }
        }

        foreach (Node child in node.GetChildren())
        {
            GameLogic.CharacterAnimationComponent2D found = FindAnimationComponent(child, graph);
            if (found != null)
                return found;
        }

        return null;
    }

    private static bool SameGraph(Resource left, Resource right)
    {
        if (left == null || right == null ||
            !GodotObject.IsInstanceValid(left) || !GodotObject.IsInstanceValid(right))
            return false;
        if (ReferenceEquals(left, right))
            return true;

        string leftPath = left.ResourcePath;
        string rightPath = right.ResourcePath;
        return !string.IsNullOrWhiteSpace(leftPath) &&
               string.Equals(leftPath, rightPath, StringComparison.Ordinal);
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
