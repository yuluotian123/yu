#if TOOLS
using System;
using Godot;

/// <summary>
/// 节点创建搜索服务。
/// </summary>
/// <remarks>
/// 窗口只把 GraphEdit 的右键位置转交给本服务。服务负责读取当前图允许的节点类型、
/// 创建分类搜索弹窗，并在用户选择后回调节点类型名。
/// </remarks>
public static class GraphNodeSearchService
{
    /// <summary>在指定画布位置打开节点搜索弹窗。</summary>
    public static void Show(GraphAsset graph, GraphEdit graphEdit, Vector2 position, Action<string> onSelected)
    {
        if (graph == null || graphEdit == null)
            return;

        var allowedNodes = graph.GetAllowedNodeTypes();
        if (allowedNodes.Count == 0)
            return;

        var popup = new SearchablePopup<string>(
            allowedNodes,
            nodeType => GraphTypeRegistry.TryGetNodeDefinition(nodeType, out GraphNodeDefinition definition)
                ? GetMenuName(definition)
                : nodeType,
            nodeType => GraphTypeRegistry.TryGetNodeDefinition(nodeType, out GraphNodeDefinition definition)
                ? definition.Category
                : "General",
            nodeType => GraphTypeRegistry.TryGetNodeDefinition(nodeType, out GraphNodeDefinition definition)
                ? $"{definition.NodeType} {definition.DisplayName} {string.Join(" ", definition.SearchKeywords)}"
                : nodeType);

        popup.OnItemSelected += nodeType => onSelected?.Invoke(nodeType);

        var anchor = new Control { Position = position };
        graphEdit.AddChild(anchor);
        popup.ShowBelow(anchor);
        anchor.QueueFree();
    }

    private static string GetMenuName(GraphNodeDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(definition?.MenuName))
            return definition.MenuName;

        if (!string.IsNullOrWhiteSpace(definition?.DisplayName))
            return definition.DisplayName;

        return definition?.NodeType ?? string.Empty;
    }
}
#endif
