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
