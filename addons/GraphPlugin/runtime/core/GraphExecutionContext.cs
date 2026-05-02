using System.Collections.Generic;

/// <summary>
/// 图运行时执行上下文。
/// </summary>
/// <remarks>
/// Flow、State、HFSM、Mission 等运行时都会把当前图、黑板栈和业务对象放进这个上下文。
/// 节点执行时不应直接依赖全局单例；优先从上下文读取当前图、黑板和 UserData。
/// </remarks>
public sealed class GraphExecutionContext
{
    /// <summary>创建运行时上下文。</summary>
    public GraphExecutionContext(GraphAsset graph, GraphBlackboardRuntime blackboard)
    {
        Graph = graph;
        Blackboard = blackboard;
    }

    /// <summary>当前执行的图资源。</summary>
    public GraphAsset Graph { get; }

    /// <summary>当前运行时黑板。内部包含本地图、父图和全局黑板作用域。</summary>
    public GraphBlackboardRuntime Blackboard { get; }

    /// <summary>业务层可挂入的运行时对象，例如角色、组件、技能实例或任务管理器。</summary>
    public List<object> UserData { get; } = new();

    /// <summary>获取第一个匹配类型的业务对象。</summary>
    public T GetUserData<T>() where T : class
    {
        for (int i = 0; i < UserData.Count; i++)
        {
            if (UserData[i] is T typed)
                return typed;
        }

        return null;
    }

    /// <summary>获取所有匹配类型的业务对象。</summary>
    public IReadOnlyList<T> GetUserDataAll<T>() where T : class
    {
        var result = new List<T>();
        for (int i = 0; i < UserData.Count; i++)
        {
            if (UserData[i] is T typed)
                result.Add(typed);
        }

        return result;
    }
}
