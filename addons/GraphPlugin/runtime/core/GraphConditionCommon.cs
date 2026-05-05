using Godot;

public enum GraphConditionUseMode
{
    And,
    Or
}

public abstract class GraphConditionEditorBase
{
    public virtual string Description => GetType().Name;

    public virtual Control CreateEditUI(GraphEditorContext context)
    {
        return new Label { Text = Description };
    }
}

public static class GraphConditionEvaluator
{
    public static bool IsMet(
        System.Collections.Generic.IList<GraphConditionBase> conditions,
        GraphConditionUseMode useMode,
        GraphExecutionContext context)
    {
        if (conditions == null || conditions.Count == 0)
            return true;

        if (useMode == GraphConditionUseMode.Or)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                if (conditions[i]?.IsMet(context) == true)
                    return true;
            }

            return false;
        }

        for (int i = 0; i < conditions.Count; i++)
        {
            if (conditions[i]?.IsMet(context) != true)
                return false;
        }

        return true;
    }
}
