#if TOOLS
using Godot;
using System.Collections.Generic;

/// <summary>
/// Subgraph navigation helpers.
/// </summary>
public partial class GraphCanvasEditorWindow
{
    public void ResetNavigation()
    {
        _graphStack.Clear();
        if (_breadcrumbBar != null)
        {
            foreach (var child in _breadcrumbBar.GetChildren())
                child.QueueFree();
            _breadcrumbBar.Visible = false;
        }
    }

    private void TryEnterSubGraph(SubGraphNodeData subData)
    {
        if (string.IsNullOrEmpty(subData.SubGraphPath))
        {
            var warn = new AcceptDialog
            {
                Title = "Can Not Enter SubGraph",
                DialogText = "Bind a GraphAsset file before entering this subgraph."
            };
            AddChild(warn);
            warn.PopupCentered();
            return;
        }

        var subGraph = subData.GetSubGraph();
        if (subGraph == null)
        {
            var warn = new AcceptDialog
            {
                Title = "Load Failed",
                DialogText = $"Can not load subgraph resource:\n{subData.SubGraphPath}"
            };
            AddChild(warn);
            warn.PopupCentered();
            return;
        }

        PushSubGraph(subGraph, subData.GetDisplayName());
    }

    private void PushSubGraph(GraphAsset subGraph, string label)
    {
        OnSave();
        _graphStack.Push((_currentGraph, _currentGraph.GetEditorTitle()));
        LoadGraph(subGraph);
        RebuildBreadcrumb();
    }

    private void PopSubGraph()
    {
        if (_graphStack.Count == 0)
            return;

        OnSave();
        var (parentGraph, _) = _graphStack.Pop();
        LoadGraph(parentGraph);
        RebuildBreadcrumb();
    }

    private void NavigateTo(int depth)
    {
        if (depth < 0)
            return;

        int pops = _graphStack.Count - depth;
        if (pops <= 0)
            return;

        OnSave();
        GraphAsset targetGraph = null;
        for (int i = 0; i < pops; i++)
        {
            var (g, _) = _graphStack.Pop();
            targetGraph = g;
        }

        if (targetGraph != null)
        {
            LoadGraph(targetGraph);
            RebuildBreadcrumb();
        }
    }

    private void RebuildBreadcrumb()
    {
        foreach (var child in _breadcrumbBar.GetChildren())
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

        _breadcrumbBar.AddChild(new Label { Text = _currentGraph.GetEditorTitle() });
    }

    private void ShowBindSubGraphDialog(SubGraphNodeData subData, GraphNode node)
    {
        var dialog = new EditorFileDialog
        {
            Title = "Select Or Create SubGraph Resource (.tres)",
            FileMode = EditorFileDialog.FileModeEnum.SaveFile,
            Access = EditorFileDialog.AccessEnum.Resources,
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
                    var warn = new AcceptDialog
                    {
                        Title = "Type Error",
                        DialogText = $"Selected file is not a {subData.GetSubGraphTypeName()} resource:\n{path}"
                    };
                    AddChild(warn);
                    warn.PopupCentered();
                    return;
                }
            }
            else
            {
                var newAsset = subData.CreateSubGraphAsset();
                ResourceSaver.Save(newAsset, path);
                GD.Print($"[SubGraph] Created new subgraph resource: {path}");
            }

            subData.SubGraphPath = path;
            subData.InvalidateCache();
            _currentGraph.SaveToJson();
            RefreshSubGraphNodeUi(node, subData);
            GD.Print($"[SubGraph] Bound subgraph: {path}");
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(900, 600));
    }

    private void RefreshSubGraphNodeUi(GraphNode node, SubGraphNodeData subData)
    {
        node.Title = subData.GetDisplayName();
        var content = node.GetNodeOrNull<VBoxContainer>("SubGraphContent");
        if (content == null)
            return;

        var pathLabel = content.GetNodeOrNull<Label>("PathLabel");
        if (pathLabel != null)
            pathLabel.Text = subData.GetDisplayName();

        var enterBtn = content.GetNodeOrNull<Button>("EnterBtn");
        if (enterBtn != null)
        {
            enterBtn.Disabled = false;
            enterBtn.TooltipText = $"Enter: {subData.GetDisplayName()}";
        }

        var bindBtn = content.GetNodeOrNull<Button>("BindBtn");
        if (bindBtn != null)
            bindBtn.Text = "Replace SubGraph Resource...";
    }
}
#endif
