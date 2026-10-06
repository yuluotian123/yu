using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Godot;
using GameLogic;

public sealed class GraphComponentArgument
{
    public string Name { get; set; } = string.Empty;
    public GraphBlackboardValue Value { get; set; } = new GraphStringBlackboardValue();
}

[GraphCallable("Call Component Method", "组件访问", GraphCallableUsage.Flow | GraphCallableUsage.BehaviorTree, ChineseName = "调用组件方法")]
public sealed class GraphComponentCallAction : GraphActionBase, IBehaviorTreeAction
{
    public GraphActionComponentReference Component { get; set; } = new();
    public string ComponentTypeName { get; set; } = string.Empty;
    public string ActionId { get; set; } = string.Empty;
    public List<GraphComponentArgument> ParameterValues { get; set; } = new();
    [JsonIgnore]
    public List<GraphComponentArgument> Arguments
    {
        get => ParameterValues;
        set => ParameterValues = value ?? new();
    }
    public string ExecutionMode { get; set; } = "OnEnter";
    public GraphActionStatus LastStatus { get; private set; } = GraphActionStatus.Failure;

    public override string Description
    {
        get
        {
            if (GraphComponentRegistry.TryGet(ComponentTypeName, out GraphComponentTypeDescriptor type))
            {
                GraphComponentActionDescriptor action = type.Actions.FirstOrDefault(value => value.MemberId == ActionId);
                if (action != null)
                    return $"{type.DisplayName}.{action.DisplayName}";
            }
            return string.IsNullOrWhiteSpace(ActionId) ? "Component Call" : ActionId;
        }
    }

    public override void Execute(GraphExecutionContext context)
    {
        LastStatus = Invoke(context);
    }

    public BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        LastStatus = Invoke(context);
        return LastStatus switch
        {
            GraphActionStatus.Success => BehaviorTreeStatus.Success,
            GraphActionStatus.Running => BehaviorTreeStatus.Running,
            _ => BehaviorTreeStatus.Failure
        };
    }

    public void Abort(BehaviorTreeRuntime runtime, GraphExecutionContext context)
    {
        LastStatus = GraphActionStatus.Failure;
    }

    public void InitializeArguments()
    {
        GraphComponentActionDescriptor action = ResolveAction();
        if (action == null)
            return;
        while (Arguments.Count < action.Parameters.Count)
            Arguments.Add(new GraphComponentArgument
            {
                Name = action.Parameters[Arguments.Count].Name,
                Value = CreateDefaultValue(action.Parameters[Arguments.Count].ValueType)
            });
        while (Arguments.Count > action.Parameters.Count)
            Arguments.RemoveAt(Arguments.Count - 1);
        for (int i = 0; i < Arguments.Count; i++)
        {
            Arguments[i] ??= new GraphComponentArgument();
            Arguments[i].Name = action.Parameters[i].Name;
            Arguments[i].Value ??= CreateDefaultValue(action.Parameters[i].ValueType);
        }
    }

#if TOOLS
    public override Control CreateEditUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 4);
        root.AddChild(Component.CreateEditUI("Component", context, () => { }));
        GraphComponentRegistry.EnsureScanned();

        var component = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var action = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        IReadOnlyList<GraphComponentTypeDescriptor> availableTypes = context?.AvailableComponentTypes;
        // An empty list is meaningful: the current graph has no matching host,
        // so do not expose unrelated component types from the global registry.
        var componentTypes = (availableTypes ?? Array.Empty<GraphComponentTypeDescriptor>())
            .Where(value => value.Actions.Count > 0)
            .ToList();
        for (int i = 0; i < componentTypes.Count; i++)
            component.AddItem(componentTypes[i].DisplayName, i);
        int componentIndex = componentTypes.FindIndex(value => value.TypeName == ComponentTypeName || value.ComponentType.Name == ComponentTypeName);
        if (componentIndex >= 0)
            component.Select(componentIndex);
        component.ItemSelected += index =>
        {
            ComponentTypeName = componentTypes[(int)index].TypeName;
            ActionId = string.Empty;
            Arguments.Clear();
            PopulateActions(action, componentTypes[(int)index]);
            RebuildArguments(root, componentTypes[(int)index].Actions.FirstOrDefault());
        };
        root.AddChild(component);

        root.AddChild(action);
        GraphComponentTypeDescriptor selectedType = componentTypes.FirstOrDefault(value => value.TypeName == ComponentTypeName || value.ComponentType.Name == ComponentTypeName);
        PopulateActions(action, selectedType);
        action.ItemSelected += index =>
        {
            GraphComponentTypeDescriptor type = componentTypes.FirstOrDefault(value =>
                value.TypeName == ComponentTypeName || value.ComponentType.Name == ComponentTypeName);
            if (type == null || index >= type.Actions.Count)
                return;
            ActionId = type.Actions[(int)index].MemberId;
            RebuildArguments(root, type.Actions[(int)index]);
        };
        RebuildArguments(root, selectedType?.Actions.FirstOrDefault(value => value.MemberId == ActionId));
        return root;
    }

    private void PopulateActions(OptionButton option, GraphComponentTypeDescriptor type)
    {
        option.Clear();
        if (type == null)
            return;
        for (int i = 0; i < type.Actions.Count; i++)
            option.AddItem(type.Actions[i].DisplayName, i);
        int index = type.Actions.FindIndex(value => value.MemberId == ActionId);
        if (index >= 0)
            option.Select(index);
    }

    private void RebuildArguments(VBoxContainer root, GraphComponentActionDescriptor descriptor)
    {
        while (root.GetChildCount() > 2)
        {
            Node child = root.GetChild(2);
            root.RemoveChild(child);
            child.QueueFree();
        }
        if (descriptor == null)
            return;
        while (Arguments.Count < descriptor.Parameters.Count)
            Arguments.Add(new GraphComponentArgument { Name = descriptor.Parameters[Arguments.Count].Name, Value = CreateDefaultValue(descriptor.Parameters[Arguments.Count].ValueType) });
        while (Arguments.Count > descriptor.Parameters.Count)
            Arguments.RemoveAt(Arguments.Count - 1);
        for (int i = 0; i < descriptor.Parameters.Count; i++)
        {
            Arguments[i].Name = descriptor.Parameters[i].Name;
            Arguments[i].Value ??= CreateDefaultValue(descriptor.Parameters[i].ValueType);
            var label = new Label { Text = descriptor.Parameters[i].Name };
            root.AddChild(label);
            Control editor = Arguments[i].Value.CreateEditUI(null);
            root.AddChild(editor);
        }
    }

#endif

    private GraphActionStatus Invoke(GraphExecutionContext context)
    {
        InitializeArguments();
        IComponent component = null;
        string resolveError = string.Empty;
        if (!GraphComponentRegistry.TryGet(ComponentTypeName, out GraphComponentTypeDescriptor componentType) ||
            !GraphActionComponentResolver.TryResolve(
                context,
                Component,
                componentType.ComponentType,
                nameof(GraphComponentCallAction),
                out component,
                out resolveError))
        {
            GD.PushError($"[GraphComponentCallAction] {resolveError}");
            return GraphActionStatus.Failure;
        }
        object[] arguments = Arguments?.Select(value => value?.Value?.GetObjectValue()).ToArray() ?? Array.Empty<object>();
        if (!GraphComponentInvoker.TryInvokeComponent(component, ComponentTypeName, ActionId, arguments, out GraphActionStatus status, out _, out string error))
        {
            GD.PushError($"[GraphComponentCallAction] {error}");
            return GraphActionStatus.Failure;
        }
        return status;
    }

    private GraphComponentActionDescriptor ResolveAction()
    {
        return GraphComponentRegistry.TryGet(ComponentTypeName, out GraphComponentTypeDescriptor type)
            ? type.Actions.FirstOrDefault(value => value.MemberId == ActionId)
            : null;
    }

    private static GraphBlackboardValue CreateDefaultValue(Type type)
    {
        if (type == typeof(bool)) return new GraphBoolBlackboardValue();
        if (type == typeof(int) || type?.IsEnum == true) return new GraphIntBlackboardValue();
        if (type == typeof(float) || type == typeof(double)) return new GraphFloatBlackboardValue();
        return new GraphStringBlackboardValue();
    }
}

[GraphCallable("Get Component Property", "组件访问", GraphCallableUsage.All, ChineseName = "读取组件属性")]
public sealed class GraphComponentGetAction : GraphActionBase, IBehaviorTreeAction
{
    public GraphActionComponentReference Component { get; set; } = new();
    public string ComponentTypeName { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string OutputKey { get; set; } = string.Empty;

    public override string Description => $"Get {ComponentTypeName}.{MemberId}";

#if TOOLS
    public override Control CreateEditUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        root.AddChild(new Label { Text = "Get component value" });
        root.AddChild(Component.CreateEditUI("Component", context, () => { }));
        AddField(root, "Component type", ComponentTypeName, value => ComponentTypeName = value);
        AddField(root, "Member ID", MemberId, value => MemberId = value);
        AddField(root, "Output blackboard key", OutputKey, value => OutputKey = value);
        return root;
    }
#endif

    public override void Execute(GraphExecutionContext context) => Read(context);

    public BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        return Read(context) ? BehaviorTreeStatus.Success : BehaviorTreeStatus.Failure;
    }

    public void Abort(BehaviorTreeRuntime runtime, GraphExecutionContext context) { }

    private bool Read(GraphExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(OutputKey))
            return false;
        string error = string.Empty;
        object value = null;
        IComponent component = null;
        string resolveError = string.Empty;
        if (!GraphComponentRegistry.TryGet(ComponentTypeName, out GraphComponentTypeDescriptor componentType) ||
            !GraphActionComponentResolver.TryResolve(
                context,
                Component,
                componentType.ComponentType,
                nameof(GraphComponentGetAction),
                out component,
                out resolveError) ||
            !GraphComponentInvoker.TryReadComponent(component, ComponentTypeName, MemberId, out value, out error))
        {
            GD.PushError($"[GraphComponentGetAction] {(string.IsNullOrWhiteSpace(resolveError) ? error : resolveError)}");
            return false;
        }
        return context.Blackboard.SetValue(OutputKey, value);
    }

#if TOOLS
    internal static void AddField(VBoxContainer root, string label, string value, Action<string> setter)
    {
        root.AddChild(new Label { Text = label });
        var edit = new LineEdit { Text = value ?? string.Empty };
        edit.TextChanged += text => setter(text);
        root.AddChild(edit);
    }
#endif
}

[GraphCallable("Set Component Property", "组件访问", GraphCallableUsage.All, ChineseName = "写入组件属性")]
public sealed class GraphComponentSetAction : GraphActionBase, IBehaviorTreeAction
{
    public GraphActionComponentReference Component { get; set; } = new();
    public string ComponentTypeName { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string InputKey { get; set; } = string.Empty;

    public override string Description => $"Set {ComponentTypeName}.{MemberId}";

#if TOOLS
    public override Control CreateEditUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        root.AddChild(new Label { Text = "Set component value" });
        root.AddChild(Component.CreateEditUI("Component", context, () => { }));
        GraphComponentGetAction.AddField(root, "Component type", ComponentTypeName, value => ComponentTypeName = value);
        GraphComponentGetAction.AddField(root, "Member ID", MemberId, value => MemberId = value);
        GraphComponentGetAction.AddField(root, "Input blackboard key", InputKey, value => InputKey = value);
        return root;
    }
#endif

    public override void Execute(GraphExecutionContext context) => Write(context);

    public BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        return Write(context) ? BehaviorTreeStatus.Success : BehaviorTreeStatus.Failure;
    }

    public void Abort(BehaviorTreeRuntime runtime, GraphExecutionContext context) { }

    private bool Write(GraphExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(InputKey) || !context.Blackboard.TryGetValue(InputKey, out object value))
            return false;
        string error = string.Empty;
        IComponent component = null;
        string resolveError = string.Empty;
        if (!GraphComponentRegistry.TryGet(ComponentTypeName, out GraphComponentTypeDescriptor componentType) ||
            !GraphActionComponentResolver.TryResolve(
                context,
                Component,
                componentType.ComponentType,
                nameof(GraphComponentSetAction),
                out component,
                out resolveError) ||
            !GraphComponentInvoker.TryWriteComponent(component, ComponentTypeName, MemberId, value, out error))
        {
            GD.PushError($"[GraphComponentSetAction] {(string.IsNullOrWhiteSpace(resolveError) ? error : resolveError)}");
            return false;
        }
        return true;
    }
}
