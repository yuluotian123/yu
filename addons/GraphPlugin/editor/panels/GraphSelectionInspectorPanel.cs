#if TOOLS
using System;
using Godot;

public sealed class GraphSelectionInspectorPanel
{
    private readonly Func<GraphAsset> _getCurrentGraph;
    private readonly Func<GraphEditorContext> _createContext;
    private readonly Func<GraphNode, GraphNodeData, Control> _buildExtraNodeInspector;
    private readonly MarginContainer _root;
    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly VBoxContainer _content;

    public GraphSelectionInspectorPanel(
        Func<GraphAsset> getCurrentGraph,
        Func<GraphEditorContext> createContext,
        Func<GraphNode, GraphNodeData, Control> buildExtraNodeInspector = null)
    {
        _getCurrentGraph = getCurrentGraph;
        _createContext = createContext;
        _buildExtraNodeInspector = buildExtraNodeInspector;

        _root = new MarginContainer
        {
            CustomMinimumSize = new Vector2(320f, 0f),
            SizeFlagsHorizontal = Control.SizeFlags.Fill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _root.AddThemeConstantOverride("margin_left", 8);
        _root.AddThemeConstantOverride("margin_top", 8);
        _root.AddThemeConstantOverride("margin_right", 8);
        _root.AddThemeConstantOverride("margin_bottom", 8);

        var layout = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        layout.AddThemeConstantOverride("separation", 6);
        _root.AddChild(layout);

        _titleLabel = new Label
        {
            Text = "Inspector",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        layout.AddChild(_titleLabel);

        _subtitleLabel = new Label
        {
            Text = "No selection",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _subtitleLabel.AddThemeColorOverride("font_color", new Color(0.68f, 0.68f, 0.68f));
        layout.AddChild(_subtitleLabel);
        layout.AddChild(new HSeparator());

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        layout.AddChild(scroll);

        _content = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _content.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_content);

        Clear();
    }

    public Control Root => _root;

    public void Clear()
    {
        _titleLabel.Text = "Inspector";
        _subtitleLabel.Text = "No selection";
        ClearContent();

        var hint = new Label
        {
            Text = "Select a node or connection to edit its details.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        hint.AddThemeColorOverride("font_color", new Color(0.62f, 0.62f, 0.62f));
        _content.AddChild(hint);
    }

    public void ShowNode(GraphNode graphNode)
    {
        GraphAsset graph = _getCurrentGraph();
        if (graph == null || graphNode == null)
        {
            Clear();
            return;
        }

        string nodeId = graphNode.Name.ToString();
        GraphNodeData nodeData = graph.FindNodeById(nodeId);
        if (nodeData == null)
        {
            Clear();
            return;
        }

        _titleLabel.Text = nodeData.GetDisplayName();
        _subtitleLabel.Text = nodeData.NodeType;
        ClearContent();

        Control ui = nodeData.CreateInspectorUI(_createContext().WithGraphNode(nodeData, graphNode));
        if (ui == null)
        {
            Clear();
            return;
        }

        ui.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _content.AddChild(ui);

        Control extraUi = _buildExtraNodeInspector?.Invoke(graphNode, nodeData);
        if (extraUi == null)
            return;

        extraUi.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _content.AddChild(new HSeparator());
        _content.AddChild(extraUi);
    }

    public void ShowConnection(GraphConnection connection)
    {
        if (connection == null)
        {
            Clear();
            return;
        }

        _titleLabel.Text = connection.GetDisplayName();
        _subtitleLabel.Text = $"{connection.FromNode}:{connection.FromPort} -> {connection.ToNode}:{connection.ToPort}";
        ClearContent();

        Control ui = connection.CreateInspectorUI(_createContext().WithConnection(connection));
        if (ui == null)
        {
            Clear();
            return;
        }

        ui.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _content.AddChild(ui);
    }

    private void ClearContent()
    {
        foreach (Node child in _content.GetChildren())
        {
            GraphEditorSignalCleanup.DisconnectSubtree(child);
            _content.RemoveChild(child);
            child.QueueFree();
        }
    }
}
#endif
