using System.Collections.Generic;
using Godot;

/// <summary>
/// 图运行时作用域接口，用来把父图、子图以及更深层运行时组织成一棵可遍历的树。
/// </summary>
/// <remarks>
/// 这个接口不是“图节点”，而是运行时黑板作用域。业务代码通常不需要直接调用它；
/// 运行时只要实现该接口，GraphPlugin 就能统一完成跨子图的黑板写入、调试和后续运行时检查。
/// </remarks>
public interface IGraphRuntimeScope
{
    /// <summary>
    /// 当前运行时的执行上下文。上下文中包含当前图资源、黑板运行时和业务对象。
    /// </summary>
    GraphExecutionContext Context { get; }

    /// <summary>
    /// 当前运行时正在管理的子运行时作用域，例如 StateGraph 的 Composite State 子图。
    /// </summary>
    IEnumerable<IGraphRuntimeScope> ChildScopes { get; }
}

/// <summary>
/// 图运行时黑板写入工具，提供跨父子图作用域的通用写入规则。
/// </summary>
/// <remarks>
/// 写入顺序是“先找声明者，再兜底写当前图”：如果某个 key 已经声明在当前图、子图或全局黑板中，
/// 就更新声明它的黑板；如果整棵运行时树都没有声明该 key，才交给根作用域的
/// <see cref="GraphBlackboardRuntime.SetValue{T}(string, T)"/> 创建或写入。
/// 这样外部系统可以简单调用 SetValue，而不需要知道变量实际属于哪个子图。
/// </remarks>
public static class GraphRuntimeBlackboardWriter
{
    /// <summary>
    /// 从根运行时开始递归写入黑板值。
    /// </summary>
    /// <typeparam name="T">写入值的类型。</typeparam>
    /// <param name="root">根运行时作用域。</param>
    /// <param name="key">黑板 key。</param>
    /// <param name="value">要写入的值。</param>
    /// <returns>写入成功返回 true；key 无效或没有可写黑板时返回 false。</returns>
    public static bool SetValueRecursive<T>(IGraphRuntimeScope root, string key, T value)
    {
        if (root == null || string.IsNullOrWhiteSpace(key))
            return false;

        var visited = new HashSet<IGraphRuntimeScope>();
        if (TrySetDeclaredLocalValueRecursive(root, key, value, visited))
            return true;

        GraphBlackboardRuntime blackboard = root.Context?.Blackboard;
        if (blackboard == null)
        {
            GD.PushWarning($"[GraphRuntimeBlackboardWriter] Can not set '{key}' because runtime scope has no blackboard.");
            return false;
        }

        return blackboard.SetValue(key, value);
    }

    /// <summary>
    /// 只更新已经声明过的黑板 key，不创建新条目。
    /// </summary>
    /// <typeparam name="T">写入值的类型。</typeparam>
    /// <param name="root">根运行时作用域。</param>
    /// <param name="key">黑板 key。</param>
    /// <param name="value">要写入的值。</param>
    /// <returns>找到声明者并写入成功返回 true。</returns>
    public static bool TrySetDeclaredValueRecursive<T>(IGraphRuntimeScope root, string key, T value)
    {
        if (root == null || string.IsNullOrWhiteSpace(key))
            return false;

        var visited = new HashSet<IGraphRuntimeScope>();
        if (TrySetDeclaredLocalValueRecursive(root, key, value, visited))
            return true;

        return root.Context?.Blackboard?.TrySetDeclaredValue(key, value) == true;
    }

    private static bool TrySetDeclaredLocalValueRecursive<T>(
        IGraphRuntimeScope scope,
        string key,
        T value,
        HashSet<IGraphRuntimeScope> visited)
    {
        if (scope == null || !visited.Add(scope))
            return false;

        if (scope.Context?.Blackboard?.TrySetDeclaredLocalValue(key, value) == true)
            return true;

        IEnumerable<IGraphRuntimeScope> children = scope.ChildScopes;
        if (children == null)
            return false;

        foreach (IGraphRuntimeScope child in children)
        {
            if (TrySetDeclaredLocalValueRecursive(child, key, value, visited))
                return true;
        }

        return false;
    }
}
