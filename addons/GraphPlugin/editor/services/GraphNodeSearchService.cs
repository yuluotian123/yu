#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
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
        Show(graph, graphEdit, position, null, onSelected, null);
    }

    public static void Show(
        GraphAsset graph,
        GraphEdit graphEdit,
        Vector2 position,
        IReadOnlyList<GraphNodeSearchEntry> dynamicEntries,
        Action<string> onSelected,
        Action<GraphNodeSearchEntry> onDynamicSelected)
    {
        if (graph == null || graphEdit == null)
            return;

        var allowedNodes = graph.GetAllowedNodeTypes()
            .Where(nodeType => !IsGenericComponentNode(nodeType))
            .ToList();
        if (allowedNodes.Count == 0 && (dynamicEntries == null || dynamicEntries.Count == 0))
            return;

        var entries = allowedNodes.Select(nodeType => new GraphNodeSearchEntry
        {
            NodeType = nodeType,
            Label = GraphTypeRegistry.TryGetNodeDefinition(nodeType, out GraphNodeDefinition definition)
                ? GetMenuName(definition)
                : nodeType,
            Group = GraphTypeRegistry.TryGetNodeDefinition(nodeType, out definition)
                ? definition.Category
                : "General",
            SearchText = GraphTypeRegistry.TryGetNodeDefinition(nodeType, out definition)
                ? $"{definition.NodeType} {definition.DisplayName} {string.Join(" ", definition.SearchKeywords)}"
                : nodeType
        }).ToList();
        if (dynamicEntries != null)
            entries.AddRange(dynamicEntries);

        var popup = new SearchablePopup<GraphNodeSearchEntry>(
            entries,
            entry => entry.Label,
            entry => entry.Group,
            entry => entry.SearchText);

        popup.OnItemSelected += entry =>
        {
            if (entry.DynamicAction != null)
            {
                entry.DynamicAction();
                onDynamicSelected?.Invoke(entry);
            }
            else
                onSelected?.Invoke(entry.NodeType);
        };

        var anchor = new Control { Position = position };
        graphEdit.AddChild(anchor);
        popup.ShowBelow(anchor);
        anchor.QueueFree();
    }

    private static bool IsGenericComponentNode(string nodeType) => nodeType == nameof(GraphComponentCallNodeData) ||
        nodeType == nameof(GraphComponentGetNodeData) ||
        nodeType == nameof(GraphComponentSetNodeData) ||
        nodeType == nameof(BehaviorTreeComponentCallNodeData) ||
        nodeType == nameof(BehaviorTreeComponentGetNodeData) ||
        nodeType == nameof(BehaviorTreeComponentSetNodeData) ||
        nodeType == nameof(GameLogic.HfsmComponentActionStateNodeData);

    public sealed class GraphNodeSearchEntry
    {
        public string NodeType { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string Group { get; init; } = string.Empty;
        public string SearchText { get; init; } = string.Empty;
        public Action DynamicAction { get; init; }
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
