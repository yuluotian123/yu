using System;
using System.Reflection;
using Godot;

public abstract class GraphActionBase
{
    public virtual string Description => GetType().Name;

    public abstract void Execute(GraphExecutionContext context);

    public virtual void Execute(GraphActionInvocation invocation)
    {
        Execute(invocation?.Execution);
    }

    public virtual Control CreateEditUI(GraphEditorContext context)
    {
        return new Label { Text = Description };
    }

    public virtual void Validate(GraphAsset graph, string nodeId, GraphValidationResult result)
    {
        if (graph?.ActionDependencyMode != GraphActionDependencyMode.HostBound || result == null)
            return;

        foreach (PropertyInfo property in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.PropertyType != typeof(GraphActionComponentReference) ||
                property.GetValue(this) is not GraphActionComponentReference reference ||
                reference.IsAssigned)
                continue;

            result.AddError($"HostBound action '{Description}' requires an explicit component for '{property.Name}'.", nodeId);
        }
    }
}
