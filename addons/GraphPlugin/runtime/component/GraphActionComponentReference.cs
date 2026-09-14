using System;
using System.Linq;
using Godot;
using GameLogic;

/// <summary>
/// Serializable reference to a component owned by the graph execution target.
/// The component instance itself is never stored in a graph resource.
/// </summary>
public sealed partial class GraphActionComponentReference
{
    public string ComponentTypeName { get; set; } = string.Empty;
    public int ComponentSlot { get; set; }

    public bool IsAssigned => !string.IsNullOrWhiteSpace(ComponentTypeName);

    public bool TryResolve(GraphExecutionContext context, out Component2D component, out string error)
    {
        component = null;
        error = string.Empty;
        if (!IsAssigned)
        {
            error = "No component has been assigned.";
            return false;
        }

        GameObject2D owner = context?.GetUserData<GameObject2D>();
        if (owner == null)
        {
            error = $"Component '{ComponentTypeName}' has no graph owner.";
            return false;
        }

        if (!GraphComponentRegistry.TryGet(ComponentTypeName, out GraphComponentTypeDescriptor descriptor))
        {
            error = $"Unknown component type '{ComponentTypeName}'.";
            return false;
        }

        var matches = owner.GetAllComponents()
            .Where(value => value != null && descriptor.ComponentType.IsInstanceOfType(value))
            .ToList();
        if (ComponentSlot < 0 || ComponentSlot >= matches.Count)
        {
            error = $"Component '{ComponentTypeName}' slot {ComponentSlot} is unavailable.";
            return false;
        }

        component = matches[ComponentSlot];
        return true;
    }

    public bool TryResolve<T>(GraphExecutionContext context, out T component, out string error)
        where T : Component2D
    {
        if (TryResolve(context, out Component2D resolved, out error) && resolved is T typed)
        {
            component = typed;
            return true;
        }

        component = null;
        if (string.IsNullOrEmpty(error))
            error = $"Component '{ComponentTypeName}' is not compatible with {typeof(T).Name}.";
        return false;
    }

#if TOOLS
    public Control CreateEditUI(string label, GraphEditorContext context, Action changed)
    {
        var root = new VBoxContainer();
        var caption = new Label { Text = label };
        root.AddChild(caption);

        if (context?.CurrentGraph?.ActionDependencyMode == GraphActionDependencyMode.Reusable)
        {
            root.AddChild(new Label
            {
                Text = "Reusable: resolved automatically from the current host",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
            return root;
        }

        var drop = new GraphComponentDropField(this, changed)
        {
            Text = IsAssigned ? $"{ComponentTypeName}  [{ComponentSlot}]" : "Drop component from Components panel",
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(0, 30),
            MouseDefaultCursorShape = Control.CursorShape.Drag
        };
        root.AddChild(drop);

        var clear = new Button { Text = "Clear" };
        clear.Disabled = !IsAssigned;
        clear.Pressed += () =>
        {
            ComponentTypeName = string.Empty;
            ComponentSlot = 0;
            drop.Text = "";
            clear.Disabled = true;
            changed?.Invoke();
        };
        root.AddChild(clear);
        return root;
    }

    private sealed partial class GraphComponentDropField : Button
    {
        private readonly GraphActionComponentReference _reference;
        private readonly Action _changed;

        public GraphComponentDropField(GraphActionComponentReference reference, Action changed)
        {
            _reference = reference;
            _changed = changed;
            FocusMode = FocusModeEnum.None;
            TooltipText = "Drag a component from the Components panel here";
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            return TryRead(data, out _, out _);
        }

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            if (!TryRead(data, out string typeName, out int slot))
                return;
            _reference.ComponentTypeName = typeName;
            _reference.ComponentSlot = slot;
            Text = $"{typeName}  [{slot}]";
            _changed?.Invoke();
        }

        private static bool TryRead(Variant data, out string typeName, out int slot)
        {
            typeName = string.Empty;
            slot = 0;
            if (data.VariantType != Variant.Type.Dictionary)
                return false;
            Godot.Collections.Dictionary dictionary = data.AsGodotDictionary();
            if (!dictionary.ContainsKey("kind") || dictionary["kind"].AsString() != "graph_component")
                return false;
            typeName = dictionary.ContainsKey("component_type") ? dictionary["component_type"].AsString() : string.Empty;
            slot = dictionary.ContainsKey("component_slot") ? dictionary["component_slot"].AsInt32() : 0;
            return !string.IsNullOrWhiteSpace(typeName);
        }
    }
#endif
}

/// <summary>Central component dependency policy used by all graph actions.</summary>
public static class GraphActionComponentResolver
{
    public static bool TryResolve<T>(
        GraphExecutionContext context,
        GraphActionComponentReference explicitReference,
        string actionName,
        out T component,
        out string error)
        where T : Component2D
    {
        component = null;
        error = string.Empty;
        if (context?.ActionDependencyMode == GraphActionDependencyMode.Reusable)
        {
            GameObject2D owner = context.GameObject;
            component = owner?.GetAllComponents()?.FirstOrDefault(value => value is T) as T;
            if (component != null)
                return true;

            error = $"[{actionName}] Reusable action could not find component '{typeof(T).Name}' on the current host.";
            return false;
        }

        if (explicitReference == null || !explicitReference.TryResolve(context, out Component2D resolved, out error))
        {
            error = $"[{actionName}] HostBound action requires an explicit '{typeof(T).Name}' component reference. {error}";
            return false;
        }

        if (resolved is T typed)
        {
            component = typed;
            return true;
        }

        error = $"[{actionName}] Component '{explicitReference.ComponentTypeName}' is not compatible with '{typeof(T).Name}'.";
        return false;
    }

    public static bool TryResolve(
        GraphExecutionContext context,
        GraphActionComponentReference explicitReference,
        Type expectedType,
        string actionName,
        out Component2D component,
        out string error)
    {
        component = null;
        error = string.Empty;
        if (expectedType == null || !typeof(Component2D).IsAssignableFrom(expectedType))
        {
            error = $"[{actionName}] Invalid component type.";
            return false;
        }

        GameObject2D owner = context?.GameObject;
        if (context?.ActionDependencyMode == GraphActionDependencyMode.Reusable)
        {
            component = owner?.GetAllComponents()?.FirstOrDefault(value => value != null && expectedType.IsInstanceOfType(value));
            if (component != null)
                return true;
            error = $"[{actionName}] Reusable action could not find component '{expectedType.Name}' on the current host.";
            return false;
        }

        if (explicitReference == null || !explicitReference.TryResolve(context, out component, out error))
        {
            error = $"[{actionName}] HostBound action requires an explicit '{expectedType.Name}' component reference. {error}";
            return false;
        }
        if (!expectedType.IsInstanceOfType(component))
        {
            error = $"[{actionName}] Component '{explicitReference.ComponentTypeName}' is not compatible with '{expectedType.Name}'.";
            component = null;
            return false;
        }
        return true;
    }
}

public sealed class GraphActionInvocation
{
    public GraphActionInvocation(GraphExecutionContext execution, System.Collections.Generic.IReadOnlyDictionary<string, object> inputs = null)
    {
        Execution = execution;
        Inputs = inputs ?? new System.Collections.Generic.Dictionary<string, object>(StringComparer.Ordinal);
    }

    public GraphExecutionContext Execution { get; }
    public System.Collections.Generic.IReadOnlyDictionary<string, object> Inputs { get; }
    public GraphActionDependencyMode DependencyMode =>
        Execution?.ActionDependencyMode ?? GraphActionDependencyMode.HostBound;

    public bool TryGetComponent<T>(string inputName, GraphActionComponentReference explicitReference, string actionName, out T component, out string error)
        where T : Component2D
    {
        if (DependencyMode == GraphActionDependencyMode.HostBound && TryGetInput(inputName, out component))
        {
            error = string.Empty;
            return true;
        }
        return GraphActionComponentResolver.TryResolve(Execution, explicitReference, actionName, out component, out error);
    }

    public bool TryGetInput<T>(string name, out T value)
    {
        if (Inputs.TryGetValue(name ?? string.Empty, out object raw) && raw is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }
}
