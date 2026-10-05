#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

public static class GraphCallableCatalog
{
    private static GraphCallableAttribute Metadata(Type type) => type.GetCustomAttribute<GraphCallableAttribute>();
    public static string Name(Type type)
    {
        var metadata = Metadata(type);
        if (metadata != null) return string.IsNullOrEmpty(metadata.ChineseName) ? metadata.Name : $"{metadata.ChineseName}（{metadata.Name}）";
        return IsCallable(type) ? Regex.Replace(type.Name, "([a-z])([A-Z])", "$1 $2") : type.Name;
    }
    public static string ItemLabel(object item, string description)
    {
        if (item == null) return description;
        var metadata = Metadata(item.GetType());
        if (metadata == null) return description;
        string name = Name(item.GetType());
        return string.IsNullOrEmpty(description) || description == metadata.Name ? name : $"{name} · {description}";
    }
    public static string Category(Type type)
    {
        if (Metadata(type) is { } metadata) return metadata.Category;
        if (typeof(GameLogic.HfsmConditionBase).IsAssignableFrom(type)) return "状态 / HFSM";
        if (typeof(StateConditionBase).IsAssignableFrom(type)) return "状态";
        if (typeof(ConditionBase).IsAssignableFrom(type)) return "任务";
        if (typeof(GraphConditionBase).IsAssignableFrom(type)) return "条件";
        if (typeof(GraphActionBase).IsAssignableFrom(type)) return "其他动作";
        return type.Namespace ?? "General";
    }
    public static string SearchText(Type type) => $"{Name(type)} {Category(type)} {type.Name} {type.FullName} {Metadata(type)?.Keywords}";
    private static bool IsCallable(Type type) => typeof(GraphActionBase).IsAssignableFrom(type) || typeof(GraphConditionBase).IsAssignableFrom(type);
    public static bool Supports(Type type, GraphCallableUsage usage)
    {
        if (Metadata(type) is { } metadata) return (metadata.Usage & usage) != 0;
        if (typeof(StateConditionBase).IsAssignableFrom(type)) return false;
        if (typeof(GraphInstantAction).IsAssignableFrom(type)) return true;
        if (typeof(GraphSharedAction).IsAssignableFrom(type) || typeof(BehaviorTreeConditionBase).IsAssignableFrom(type))
            return usage != GraphCallableUsage.Timeline;
        if (typeof(BehaviorTreeActionBase).IsAssignableFrom(type)) return usage == GraphCallableUsage.BehaviorTree;
        return usage == GraphCallableUsage.Flow;
    }
    public static IReadOnlyList<Type> Actions(GraphCallableUsage usage) => Sort(SubTypeCache.GetSubTypes<GraphActionBase>().Where(type => Supports(type, usage)));
    public static IReadOnlyList<Type> TimelineActions(GraphTimelineActionKind kind) => Actions(GraphCallableUsage.Timeline)
        .Where(type => GraphTimelineActionRules.Supports(type, kind)).ToList();
    public static IReadOnlyList<Type> ActionsForGraph(GraphAsset graph) => Actions(GraphCallableUsage.Flow)
        .Where(type => graph is not GameLogic.CharacterGraphAsset ||
            (type != typeof(GraphComponentCallAction) && type != typeof(GraphComponentGetAction) &&
             type != typeof(GraphComponentSetAction) && type != typeof(GameLogic.UseAbilityAction))).ToList();
    public static IReadOnlyList<Type> Conditions(GraphCallableUsage usage) => Sort(SubTypeCache.GetSubTypes<GraphConditionBase>().Where(type => Supports(type, usage)));
    private static IReadOnlyList<Type> Sort(IEnumerable<Type> types) => types.OrderBy(Category, StringComparer.Ordinal).ThenBy(Name, StringComparer.Ordinal).ToList();
}
#endif
