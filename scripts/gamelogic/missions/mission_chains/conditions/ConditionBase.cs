using Godot;

public enum ConditionExecuteMode
{
    Sequence,
    Parallel
}

public enum ConditionUseMode
{
    And,
    Or
}

/// <summary>
/// 条件基类，纯 C# 类。
/// 序列化由 GraphJsonHelper 统一完成（多态 $type 方案）。
/// 子类只需定义公开属性即可自动序列化/反序列化。
/// </summary>
public abstract class ConditionBase : GraphConditionBase
{
    public abstract bool IsConditionMet { get; }

    public override bool IsMet(GraphExecutionContext context)
    {
        return IsConditionMet;
    }
}
