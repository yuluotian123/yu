using Godot;

public enum GraphConditionUseMode
{
    And,
    Or
}

public abstract class GraphConditionBase
{
    public virtual string Description => GetType().Name;

    public abstract bool IsMet(GraphExecutionContext context);

    public virtual Control CreateEditUI(GraphEditorContext context)
    {
        return new Label { Text = Description };
    }
}
