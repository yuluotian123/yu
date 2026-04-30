using Godot;

public abstract class StateConditionBase
{
    public virtual string Description => GetType().Name;

    public abstract bool IsMet(StateGraphRuntime runtime);

    public virtual Control CreateEditUI(GraphEditorContext context)
    {
        return new Label { Text = Description };
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
