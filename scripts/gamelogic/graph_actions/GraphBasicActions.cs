using System;
using Godot;

namespace GameLogic;

[GraphCallable("Set Blackboard", "黑板与数据", GraphCallableUsage.All, ChineseName = "设置黑板")]
public sealed class SetBlackboardAction : GraphInstantAction
{
    public GraphBlackboardKeyReference Key { get; set; } = new();
    public GraphActionValue Value { get; set; } = GraphActionValue.Number(0);
    public override string Description => "Set Blackboard";
    protected override bool Run(GraphActionInvocation call) => Value.TryRead(call.Execution, out object value)
        && call.Execution?.Blackboard?.SetValue(Key.Key, value) == true;
}

[GraphCallable("Add Blackboard Number", "黑板与数据", GraphCallableUsage.All, ChineseName = "累加黑板数值")]
public sealed class AddBlackboardNumberAction : GraphInstantAction
{
    public GraphBlackboardKeyReference Key { get; set; } = new();
    public GraphActionValue Amount { get; set; } = GraphActionValue.Number(1);
    public override string Description => "Add Blackboard Number";
    protected override bool Run(GraphActionInvocation call)
    {
        var current = GraphActionValue.Key(Key.Key);
        return current.TryNumber(call.Execution, out float value) && Amount.TryNumber(call.Execution, out float amount)
            && float.IsFinite(value + amount) && call.Execution.Blackboard.SetValue(Key.Key, value + amount);
    }
}

[GraphCallable("Wait Seconds", "流程", GraphCallableUsage.Flow | GraphCallableUsage.BehaviorTree, ChineseName = "等待秒数")]
public sealed class WaitSecondsAction : GraphSharedAction
{
    public GraphActionValue Seconds { get; set; } = GraphActionValue.Number(1);
    public override string Description => "Wait Seconds";
    public override GraphActionTask CreateTask(GraphActionInvocation call)
    {
        return new GraphDelayTask(Seconds.TryNumber(call.Execution, out float seconds) ? seconds : float.NaN);
    }
}

public enum GraphValueComparison { Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual }

public abstract class GraphSharedCondition : BehaviorTreeConditionBase
{
#if TOOLS
    public override Control CreateEditUI(GraphEditorContext context) => GraphCommonActionEditor.Build(this, context);
#endif
}

[GraphCallable("Compare Blackboard", "黑板与数据", GraphCallableUsage.Flow | GraphCallableUsage.BehaviorTree, ChineseName = "比较黑板")]
public sealed class BlackboardCompareCondition : GraphSharedCondition
{
    public GraphActionValue Left { get; set; } = GraphActionValue.Key("Value");
    public GraphValueComparison Comparison { get; set; }
    public GraphActionValue Right { get; set; } = GraphActionValue.Number(0);
    public override string Description => "Compare Blackboard";
    public override bool IsMet(GraphExecutionContext context)
    {
        if (!Left.TryRead(context, out object left) || !Right.TryRead(context, out object right)) return false;
        if (Left.TryNumber(context, out float a) && Right.TryNumber(context, out float b))
            return Comparison switch
            {
                GraphValueComparison.Equal => Mathf.IsEqualApprox(a, b),
                GraphValueComparison.NotEqual => !Mathf.IsEqualApprox(a, b),
                GraphValueComparison.Less => a < b, GraphValueComparison.LessOrEqual => a <= b,
                GraphValueComparison.Greater => a > b, GraphValueComparison.GreaterOrEqual => a >= b, _ => false
            };
        return Comparison switch
        {
            GraphValueComparison.Equal => Equals(left, right),
            GraphValueComparison.NotEqual => !Equals(left, right), _ => false
        };
    }
}
