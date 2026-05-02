#if TOOLS
using System;
using System.Linq;
using Godot;

/// <summary>
/// 图浏览器面板。
/// </summary>
/// <remarks>
/// 面板提供节点树、验证结果和定位能力。它只读取编辑器当前状态，不拥有图数据，
/// 因此窗口切换图后可以安全刷新。
/// </remarks>
public sealed class GraphExplorerPanel
{
    private readonly Window _owner;
    private readonly Func<GraphAsset> _getCurrentGraph;
    private readonly Func<GraphEdit> _getGraphEdit;
    private Window _window;
    private VBoxContainer _content;

    /// <summary>创建图浏览器面板。</summary>
    public GraphExplorerPanel(Window owner, Func<GraphAsset> getCurrentGraph, Func<GraphEdit> getGraphEdit)
    {
        _owner = owner;
        _getCurrentGraph = getCurrentGraph;
        _getGraphEdit = getGraphEdit;
    }

    /// <summary>打开或刷新浏览器窗口。</summary>
    public void Open()
    {
        if (_window == null || !GodotObject.IsInstanceValid(_window))
            CreateWindow();

        Refresh();
        _window.PopupCentered();
    }

    /// <summary>如果窗口已打开则刷新内容。</summary>
    public void RefreshIfOpen()
    {
        if (_window != null && GodotObject.IsInstanceValid(_window) && _window.Visible)
            Refresh();
    }

    /// <summary>关闭并释放浏览器窗口。</summary>
    public void Close()
    {
        if (_window == null || !GodotObject.IsInstanceValid(_window))
        {
            _window = null;
            return;
        }

        _window.QueueFree();
        _window = null;
    }

    private void CreateWindow()
    {
        _window = new Window
        {
            Title = "Graph Explorer",
            Size = new Vector2I(420, 640)
        };
        _window.CloseRequested += () => _window.Hide();
        _owner.AddChild(_window);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        _window.AddChild(margin);

        _content = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(_content);
    }

    private void Refresh()
    {
        foreach (Node child in _content.GetChildren())
        {
            _content.RemoveChild(child);
            child.QueueFree();
        }

        GraphAsset graph = _getCurrentGraph();
        if (graph == null)
        {
            _content.AddChild(new Label { Text = "No graph loaded." });
            return;
        }

        var header = new HBoxContainer();
        header.AddChild(new Label
        {
            Text = graph.GetEditorTitle(),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        });

        var refreshButton = new Button { Text = "Refresh" };
        refreshButton.Pressed += Refresh;
        header.AddChild(refreshButton);
        _content.AddChild(header);

        _content.AddChild(BuildNodeTree(graph));
        _content.AddChild(BuildValidationList(graph));
    }

    private Control BuildNodeTree(GraphAsset graph)
    {
        var tree = new Tree
        {
            HideRoot = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 280)
        };
        tree.ItemActivated += () => LocateSelectedNode(tree);

        TreeItem root = tree.CreateItem();
        foreach (var group in graph.Nodes
                     .Where(node => node != null)
                     .GroupBy(node => node.GetCategory())
                     .OrderBy(group => group.Key))
        {
            TreeItem groupItem = tree.CreateItem(root);
            groupItem.SetText(0, string.IsNullOrWhiteSpace(group.Key) ? "General" : group.Key);
            groupItem.SetSelectable(0, false);

            foreach (GraphNodeData node in group.OrderBy(node => node.GetDisplayName()))
            {
                TreeItem nodeItem = tree.CreateItem(groupItem);
                nodeItem.SetText(0, $"{node.GetDisplayName()}  [{node.Id}]");
                nodeItem.SetMetadata(0, node.Id);
            }
        }

        return tree;
    }

    private Control BuildValidationList(GraphAsset graph)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(new Label { Text = "Validation" });

        GraphValidationResult result = GraphValidationService.Validate(graph);
        if (result.Issues.Count == 0)
        {
            box.AddChild(new Label { Text = "图验证通过。" });
            return box;
        }

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 180)
        };
        var issues = new VBoxContainer();
        scroll.AddChild(issues);

        foreach (GraphValidationIssue issue in result.Issues)
        {
            var label = new Label
            {
                Text = $"{(issue.Severity == GraphValidationSeverity.Error ? "错误" : "警告")}: {issue.Message}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            if (issue.Severity == GraphValidationSeverity.Error)
                label.AddThemeColorOverride("font_color", new Color(1f, 0.35f, 0.35f));
            issues.AddChild(label);
        }

        box.AddChild(scroll);
        return box;
    }

    private void LocateSelectedNode(Tree tree)
    {
        TreeItem selected = tree.GetSelected();
        string nodeId = selected?.GetMetadata(0).AsString();
        if (string.IsNullOrWhiteSpace(nodeId))
            return;

        GraphEdit graphEdit = _getGraphEdit();
        GraphNode target = graphEdit?.GetNodeOrNull<GraphNode>(new NodePath(nodeId));
        if (target == null)
            return;

        foreach (Node child in graphEdit.GetChildren())
        {
            if (child is GraphNode graphNode)
                graphNode.Selected = graphNode == target;
        }

        graphEdit.ScrollOffset = target.PositionOffset - new Vector2(220f, 160f);
    }
}
#endif
