using Godot;

public abstract class StateConditionBase : GraphConditionBase
{
    public abstract bool IsMet(StateGraphRuntime runtime);

    public override bool IsMet(GraphExecutionContext context)
    {
        return IsMet(context?.GetUserData<StateGraphRuntime>());
    }
}

public enum StateFloatComparison
{
    Less,
    LessOrEqual,
    Equal,
    GreaterOrEqual,
    Greater
}

public static class StateFloatComparisonUtility
{
    public static bool Evaluate(
        StateFloatComparison comparison,
        float actual,
        float expected,
        float tolerance = 0.0001f)
    {
        return comparison switch
        {
            StateFloatComparison.Less => actual < expected,
            StateFloatComparison.LessOrEqual => actual <= expected,
            StateFloatComparison.Equal => Mathf.Abs(actual - expected) <= tolerance,
            StateFloatComparison.GreaterOrEqual => actual >= expected,
            StateFloatComparison.Greater => actual > expected,
            _ => false
        };
    }

    public static string ToOperatorText(StateFloatComparison comparison)
    {
        return comparison switch
        {
            StateFloatComparison.Less => "<",
            StateFloatComparison.LessOrEqual => "<=",
            StateFloatComparison.Equal => "==",
            StateFloatComparison.GreaterOrEqual => ">=",
            StateFloatComparison.Greater => ">",
            _ => "?"
        };
    }
}
