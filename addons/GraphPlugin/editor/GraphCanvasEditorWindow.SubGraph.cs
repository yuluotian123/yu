#if TOOLS
using Godot;
using System.Collections.Generic;

/// <summary>
/// 子图导航相关方法
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
            var warn = new AcceptDialog { Title = "无法进入子图", DialogText = "请先使用[绑定子图资源]按钮绑定一个 GraphAsset 文件。" };
            AddChild(warn);
            warn.PopupCentered();
            return;
        }
        var subGraph = subData.GetSubGraph();
        if (subGraph == null)
        {
            var warn = new AcceptDialog { Title = "加载失败", DialogText = $"无法加载子图资源：\n{subData.SubGraphPath}" };
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
        if (_graphStack.Count == 0) return;
        OnSave();
        var (parentGraph, _) = _graphStack.Pop();
        LoadGraph(parentGraph);
        RebuildBreadcrumb();
    }

    private void NavigateTo(int depth)
    {
        if (depth < 0) return;
        int pops = _graphStack.Count - depth;
        if (pops <= 0) return;
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
        if (!isSubGraph) return;
        var stackList = new List<(GraphAsset graph, string label)>(_graphStack);
        stackList.Reverse();
        var backBtn = new Button { Text = "← 返回" };
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
            Title = "选择或新建子图资源 (.tres)",
            FileMode = EditorFileDialog.FileModeEnum.SaveFile,
            Access = EditorFileDialog.AccessEnum.Resources,
        };
        dialog.AddFilter("*.tres", "Godot 资源文件");
        dialog.FileSelected += (path) =>
        {
            if (!path.EndsWith(".tres")) path += ".tres";
            if (ResourceLoader.Exists(path))
            {
                var res = ResourceLoader.Load(path);
                if (res is not GraphAsset graphAsset || !subData.AcceptsSubGraph(graphAsset))
                {
                    var warn = new AcceptDialog { Title = "类型错误", DialogText = $"所选文件不是 {subData.GetSubGraphTypeName()} 资源：\n{path}" };
                    AddChild(warn);
                    warn.PopupCentered();
                    return;
                }
            }
            else
            {
                var newAsset = subData.CreateSubGraphAsset();
                ResourceSaver.Save(newAsset, path);
                GD.Print($"[SubGraph] 已创建新子图资源: {path}");
            }
            subData.SubGraphPath = path;
            subData.InvalidateCache();
            _currentGraph.SaveToJson();
            RefreshSubGraphNodeUi(node, subData);
            GD.Print($"[SubGraph] 已绑定子图: {path}");
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(900, 600));
    }
    private void RefreshSubGraphNodeUi(GraphNode node, SubGraphNodeData subData)
    {
        node.Title = subData.GetDisplayName();
        var content = node.GetNodeOrNull<VBoxContainer>("SubGraphContent");
        if (content == null) return;
        var pathLabel = content.GetNodeOrNull<Label>("PathLabel");
        if (pathLabel != null)
            pathLabel.Text = $"📄 {subData.GetDisplayName()}";
        var enterBtn = content.GetNodeOrNull<Button>("EnterBtn");
        if (enterBtn != null)
        {
            enterBtn.Disabled = false;
            enterBtn.TooltipText = $"进入: {subData.GetDisplayName()}";
        }
        var bindBtn = content.GetNodeOrNull<Button>("BindBtn");
        if (bindBtn != null)
            bindBtn.Text = "更换子图资源...";
    }
}
#endif
