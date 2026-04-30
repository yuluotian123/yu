using Godot;

public abstract class GraphActionBase
{
    public virtual string Description => GetType().Name;

    public abstract void Execute(GraphExecutionContext context);

    public virtual Control CreateEditUI(GraphEditorContext context)
    {
        return new Label { Text = Description };
    }
}
