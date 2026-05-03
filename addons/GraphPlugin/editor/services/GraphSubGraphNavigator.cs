#if TOOLS
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// 子图导航服务。
/// </summary>
/// <remarks>
/// 服务拥有父图栈、面包屑、进入/返回、绑定/创建子图资源和子图节点按钮注入逻辑。
/// </remarks>
public sealed class GraphSubGraphNavigator
{
    private readonly Window _owner;
    private readonly HBoxContainer _breadcrumbBar;
    private readonly Func<GraphAsset> _getCurrentGraph;
    private readonly Action<GraphAsset> _loadGraph;
    private readonly Action _saveCurrentGraph;
    private readonly Stack<(GraphAsset graph, string label)> _graphStack = new();

    /// <summary>创建子图导航服务。</summary>
    public GraphSubGraphNavigator(
        Window owner,
        HBoxContainer breadcrumbBar,
        Func<GraphAsset> getCurrentGraph,
        Action<GraphAsset> loadGraph,
        Action saveCurrentGraph)
    {
        _owner = owner;
        _breadcrumbBar = breadcrumbBar;
        _getCurrentGraph = getCurrentGraph;
        _loadGraph = loadGraph;
        _saveCurrentGraph = saveCurrentGraph;
    }

    /// <summary>重置导航栈和面包屑。</summary>
    public void Reset()
    {
        _graphStack.Clear();
        if (_breadcrumbBar == null)
            return;

        foreach (Node child in _breadcrumbBar.GetChildren())
            child.QueueFree();
        _breadcrumbBar.Visible = false;
    }

    /// <summary>获取父图列表，直接父图排在前面。</summary>
    public List<GraphAsset> GetParentGraphs()
    {
        var parents = new List<GraphAsset>();
        foreach (var item in _graphStack)
            parents.Add(item.graph);
        return parents;
    }

    /// <summary>获取当前导航链上的根图。</summary>
    public GraphAsset GetRootGraph(GraphAsset fallback)
    {
        GraphAsset root = fallback;
        foreach (var item in _graphStack)
            root = item.graph;
        return root;
    }

    /// <summary>Creates the inspector controls for binding and entering a subgraph.</summary>
    public Control CreateSubGraphInspectorControls(GraphNode node, SubGraphNodeData subData)
    {
        if (subData == null)
            return null;

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 6);
        root.AddChild(new Label { Text = "SubGraph" });

        Button enterBtn = null;
        GraphResourcePathField resourceField = null;

        resourceField = new GraphResourcePathField(
            subData.GetSubGraphType(),
            subData.SubGraphPath,
            path =>
            {
                BindSubGraphPath(subData, node, path);
                resourceField.Path = subData.SubGraphPath;
                UpdateEnterButton(enterBtn, subData);
            })
        {
            Name = "SubGraphResourceField"
        };
        root.AddChild(resourceField);

        enterBtn = new Button
        {
            Text = "Enter SubGraph >",
            Name = "EnterBtn"
        };
        UpdateEnterButton(enterBtn, subData);
        enterBtn.Pressed += () => TryEnterSubGraph(subData);
        root.AddChild(enterBtn);

        var createBtn = new Button
        {
            Text = "Create SubGraph Resource...",
            TooltipText = "Create a new subgraph resource file",
            Name = "CreateBtn"
        };
        createBtn.Pressed += () => ShowCreateSubGraphDialog(subData, node);
        root.AddChild(createBtn);

        return root;
    }

    /// <summary>Adds a compact enter button to a subgraph node on the canvas.</summary>
    public void InjectSubGraphEnterButton(GraphNode node, SubGraphNodeData subData)
    {
        if (node == null || subData == null)
            return;

        var content = node.GetNodeOrNull<VBoxContainer>("SubGraphContent");
        if (content == null || content.GetNodeOrNull<Button>("EnterBtn") != null)
            return;

        var enterBtn = new Button
        {
            Text = ">",
            Name = "EnterBtn",
            CustomMinimumSize = new Vector2(28f, 0f),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
        };
        UpdateEnterButton(enterBtn, subData);
        enterBtn.Pressed += () => TryEnterSubGraph(subData);
        content.AddChild(enterBtn);
    }

    private void TryEnterSubGraph(SubGraphNodeData subData)
    {
        if (string.IsNullOrEmpty(subData.SubGraphPath))
        {
            ShowWarning("Can Not Enter SubGraph", "Bind a GraphAsset file before entering this subgraph.");
            return;
        }

        GraphAsset subGraph = subData.GetSubGraph();
        if (subGraph == null)
        {
            ShowWarning("Load Failed", $"Can not load subgraph resource:\n{subData.SubGraphPath}");
            return;
        }

        PushSubGraph(subGraph);
    }

    private void PushSubGraph(GraphAsset subGraph)
    {
        GraphAsset current = _getCurrentGraph();
        if (current == null || subGraph == null)
            return;

        _saveCurrentGraph?.Invoke();
        _graphStack.Push((current, current.GetEditorTitle()));
        _loadGraph(subGraph);
        RebuildBreadcrumb();
    }

    private void PopSubGraph()
    {
        if (_graphStack.Count == 0)
            return;

        _saveCurrentGraph?.Invoke();
        var (parentGraph, _) = _graphStack.Pop();
        _loadGraph(parentGraph);
        RebuildBreadcrumb();
    }

    private void NavigateTo(int depth)
    {
        if (depth < 0)
            return;

        int pops = _graphStack.Count - depth;
        if (pops <= 0)
            return;

        _saveCurrentGraph?.Invoke();
        GraphAsset targetGraph = null;
        for (int i = 0; i < pops; i++)
        {
            var (graph, _) = _graphStack.Pop();
            targetGraph = graph;
        }

        if (targetGraph != null)
        {
            _loadGraph(targetGraph);
            RebuildBreadcrumb();
        }
    }

    private void RebuildBreadcrumb()
    {
        foreach (Node child in _breadcrumbBar.GetChildren())
            child.QueueFree();

        bool isSubGraph = _graphStack.Count > 0;
        _breadcrumbBar.Visible = isSubGraph;
        if (!isSubGraph)
            return;

        var stackList = new List<(GraphAsset graph, string label)>(_graphStack);
        stackList.Reverse();

        var backBtn = new Button { Text = "< Back" };
        backBtn.Pressed += PopSubGraph;
        _breadcrumbBar.AddChild(backBtn);
        _breadcrumbBar.AddChild(new VSeparator());

        for (int i = 0; i < stackList.Count; i++)
        {
            int depth = i;
            var crumbBtn = new Button { Text = stackList[i].label };
            crumbBtn.Pressed += () => NavigateTo(depth);
            _breadcrumbBar.AddChild(crumbBtn);
            _breadcrumbBar.AddChild(new Label { Text = " > " });
        }

        GraphAsset current = _getCurrentGraph();
        _breadcrumbBar.AddChild(new Label { Text = current?.GetEditorTitle() ?? "Graph" });
    }

    private void BindSubGraphPath(SubGraphNodeData subData, GraphNode node, string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            if (!ResourceLoader.Exists(path))
            {
                ShowWarning("Resource Not Found", $"SubGraph resource does not exist:\n{path}");
                return;
            }

            var resource = ResourceLoader.Load(path);
            if (resource is not GraphAsset graphAsset || !subData.AcceptsSubGraph(graphAsset))
            {
                ShowWarning("Type Error", $"Selected file is not a {subData.GetSubGraphType().Name} resource:\n{path}");
                return;
            }
        }

        GraphAsset current = _getCurrentGraph();
        subData.SubGraphPath = path ?? string.Empty;
        subData.InvalidateCache();
        current?.MarkDirty();
        current?.SaveJsonFields();
        RefreshSubGraphNodeUi(node, subData);
    }

    private void ShowCreateSubGraphDialog(SubGraphNodeData subData, GraphNode node)
    {
        var dialog = new EditorFileDialog
        {
            Title = "Create SubGraph Resource (.tres)",
            FileMode = EditorFileDialog.FileModeEnum.SaveFile,
            Access = EditorFileDialog.AccessEnum.Resources
        };
        dialog.AddFilter("*.tres", "Godot resource file");
        dialog.FileSelected += path =>
        {
            if (!path.EndsWith(".tres"))
                path += ".tres";

            if (ResourceLoader.Exists(path))
            {
                var res = ResourceLoader.Load(path);
                if (res is not GraphAsset graphAsset || !subData.AcceptsSubGraph(graphAsset))
                {
                    ShowWarning("Type Error", $"Selected file is not a {subData.GetSubGraphType().Name} resource:\n{path}");
                    return;
                }
            }
            else
            {
                GraphAsset newAsset = subData.CreateSubGraphAsset();
                ResourceSaver.Save(newAsset, path);
                GD.Print($"[SubGraph] Created new subgraph resource: {path}");
            }

            BindSubGraphPath(subData, node, path);
            GD.Print($"[SubGraph] Bound subgraph: {path}");
        };
        _owner.AddChild(dialog);
        dialog.PopupCentered(new Vector2I(900, 600));
    }

    private static void RefreshSubGraphNodeUi(GraphNode node, SubGraphNodeData subData)
    {
        node.Title = subData.GetDisplayName();
        var content = node.GetNodeOrNull<VBoxContainer>("SubGraphContent");
        if (content == null)
            return;

        var pathLabel = content.GetNodeOrNull<Label>("PathLabel");
        if (pathLabel != null)
        {
            pathLabel.Text = subData.GetCompactSubGraphLabel();
            pathLabel.TooltipText = string.IsNullOrEmpty(subData.SubGraphPath) ? "Unbound SubGraph" : subData.SubGraphPath;
        }

        var resourceField = content.GetNodeOrNull<GraphResourcePathField>("SubGraphResourceField");
        if (resourceField != null)
            resourceField.Path = subData.SubGraphPath;

        var enterBtn = content.GetNodeOrNull<Button>("EnterBtn");
        if (enterBtn != null)
        {
            enterBtn.Disabled = string.IsNullOrEmpty(subData.SubGraphPath);
            enterBtn.TooltipText = string.IsNullOrEmpty(subData.SubGraphPath)
                ? "Bind a subgraph resource first"
                : $"Enter: {subData.GetDisplayName()}";
        }

        node.CallDeferred("reset_size");
    }

    private static void UpdateEnterButton(Button enterBtn, SubGraphNodeData subData)
    {
        if (enterBtn == null || subData == null)
            return;

        enterBtn.Disabled = string.IsNullOrEmpty(subData.SubGraphPath);
        enterBtn.TooltipText = string.IsNullOrEmpty(subData.SubGraphPath)
            ? "Bind a subgraph resource first"
            : $"Enter: {subData.GetDisplayName()}";
    }

    private void ShowWarning(string title, string text)
    {
        var dialog = new AcceptDialog
        {
            Title = title,
            DialogText = text
        };
        _owner.AddChild(dialog);
        dialog.PopupCentered();
    }
}
#endif
