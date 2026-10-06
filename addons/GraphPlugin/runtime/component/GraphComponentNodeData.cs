using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Godot;
using GameLogic;

public static class GraphComponentPortTypes
{
    public const int Component = 9;
    public static int FromType(Type type)
    {
        // Component nodes are registered from empty templates before a member is selected.
        if (type == null) return 8;
        if (type == typeof(bool)) return 1;
        if (type == typeof(int) || type.IsEnum) return 2;
        if (type == typeof(float) || type == typeof(double)) return 3;
        if (type == typeof(string)) return 4;
        if (type == typeof(Vector2)) return 5;
        if (type == typeof(Vector3)) return 6;
        if (type == typeof(Color)) return 7;
        return 8;
    }
}

/// <summary>Exposes one graph host component as a typed flow value.</summary>
public sealed class GraphComponentReferenceNodeData : GraphNodeData, IFlowNode
{
    public string ComponentTypeName { get; set; } = string.Empty;
    public int ComponentSlot { get; set; }

    public override List<string> GetGraphTypes() => new() { FlowGraphAsset.GraphTypeName, GameLogic.CharacterGraphAsset.CharacterGraphTypeName };
    public override string GetDisplayName() => "Component Reference";
    public override string GetMenuName() => "Component Reference";
    public override string GetCategory() => "Component";
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;
    public override string GetInputPortName(int port) => "Execute";
    public override string GetOutputPortName(int port) => "Component";
    public override int GetOutputPortType(int port) => GraphComponentPortTypes.Component;
    public override Color GetNodeColor() => new(0.36f, 0.62f, 0.86f);

    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        if (!TryResolve(context, out IComponent component, out string error))
        {
            GD.PushError($"[GraphComponentReferenceNode] {error}");
            runtime.SetNodeOutput(Id, null);
            return;
        }
        runtime.SetNodeOutput(Id, component);
    }

    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }
    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        completion = NodeCompletion.Next();
        return true;
    }
    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) { }

    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        root.AddChild(new Label { Text = "Component type" });
        var typeEdit = new LineEdit { Text = ComponentTypeName };
        typeEdit.TextChanged += value => ComponentTypeName = value;
        root.AddChild(typeEdit);
        root.AddChild(new Label { Text = "Component slot" });
        var slot = new SpinBox { MinValue = 0, MaxValue = 32, Step = 1, Value = ComponentSlot };
        slot.ValueChanged += value => ComponentSlot = (int)value;
        root.AddChild(slot);
        return root;
    }

    private bool TryResolve(GraphExecutionContext context, out IComponent component, out string error)
    {
        var reference = new GraphActionComponentReference
        {
            ComponentTypeName = ComponentTypeName,
            ComponentSlot = ComponentSlot
        };
        if (!GraphComponentRegistry.TryGet(ComponentTypeName, out GraphComponentTypeDescriptor descriptor))
        {
            component = null;
            error = $"Unknown component type '{ComponentTypeName}'.";
            return false;
        }
        return GraphActionComponentResolver.TryResolve(context, reference, descriptor.ComponentType, GetDisplayName(), out component, out error);
    }
}

public sealed class GraphActionNodeData : GraphNodeData, IFlowNode
{
    public GraphActionBase Action { get; set; }

    public override List<string> GetGraphTypes() => new() { FlowGraphAsset.GraphTypeName, GameLogic.CharacterGraphAsset.CharacterGraphTypeName };
    public override string GetDisplayName()
    {
#if TOOLS
        return Action == null ? "Action" : GraphCallableCatalog.ItemLabel(Action, Action.Description);
#else
        return Action?.Description ?? "Action";
#endif
    }
    public override string GetMenuName() => "Action";
    public override string GetCategory() => "Action";
    public override int GetInputCount() => 2;
    public override int GetOutputCount() => 2;
    public override string GetInputPortName(int port) => port == 0 ? "Execute" : "Component";
    public override string GetOutputPortName(int port) => port == 0 ? "Success" : "Failure";
    public override int GetInputPortType(int port) => port == 0 ? 0 : GraphComponentPortTypes.Component;
    public override Color GetNodeColor() => new(0.55f, 0.42f, 0.82f);

    public override void Validate(GraphAsset graph, GraphValidationResult result)
    {
        if (Action is GraphSharedAction shared) shared.Validate(graph, Id, result);
    }

    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        var inputs = new System.Collections.Generic.Dictionary<string, object>(StringComparer.Ordinal);
        foreach (GraphConnection connection in runtime.Graph.GetIncomingConnections(Id, 1))
        {
            if (!runtime.TryGetNodeOutput(connection.FromNode, out object value))
                continue;
            inputs["Component"] = value;
            break;
        }
        var run = new GraphActionSequenceRun { Invocation = new GraphActionInvocation(context, inputs) };
        runtime.SetNodeData(Id, run);
        run.Tick(new[] { Action }, run.Invocation, 0);
    }

    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta)
    {
        var run = runtime.GetNodeData<GraphActionSequenceRun>(Id);
        run.Tick(new[] { Action }, run.Invocation ?? new GraphActionInvocation(context), delta);
    }
    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        var status = runtime.GetNodeData<GraphActionSequenceRun>(Id).Status;
        completion = status == BehaviorTreeStatus.Failure ? NodeCompletion.False("Failure") : NodeCompletion.Next("Success");
        return status != BehaviorTreeStatus.Running;
    }
    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        if (runtime.TryGetNodeData(Id, out GraphActionSequenceRun run)) run.Cancel(context);
        runtime.SetNodeData(Id, null);
    }

    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
#if TOOLS
        var selector = new Button { Text = Action == null ? "选择动作…" : GraphCallableCatalog.Name(Action.GetType()) };
        var parameters = new VBoxContainer();
        selector.Pressed += () =>
        {
            var popup = new SearchablePopup<Type>(GraphCallableCatalog.ActionsForGraph(context.CurrentGraph),
                GraphCallableCatalog.Name, GraphCallableCatalog.Category, GraphCallableCatalog.SearchText);
            popup.OnItemSelected += type =>
            {
                Action = (GraphActionBase)Activator.CreateInstance(type);
                selector.Text = GraphCallableCatalog.Name(type);
                context.CurrentGraph?.MarkDirty();
                foreach (Node child in parameters.GetChildren())
                {
                    GraphEditorSignalCleanup.DisconnectSubtree(child);
                    parameters.RemoveChild(child); child.QueueFree();
                }
                parameters.AddChild(Action.CreateEditUI(context));
            };
            popup.ShowBelow(selector);
        };
        root.AddChild(selector);
        root.AddChild(parameters);
        if (Action != null) parameters.AddChild(Action.CreateEditUI(context));
#endif
        return root;
    }
}

public abstract class GraphComponentNodeData : GraphNodeData
{
    public GraphActionComponentReference Component { get; set; } = new();
    public string ComponentTypeName { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;

    protected GraphComponentTypeDescriptor ComponentType =>
        GraphComponentRegistry.TryGet(ComponentTypeName, out GraphComponentTypeDescriptor descriptor) ? descriptor : null;

    public override void Validate(GraphAsset graph, GraphValidationResult result)
    {
        if (graph?.ActionDependencyMode == GraphActionDependencyMode.HostBound && Component?.IsAssigned != true)
            result.AddError($"HostBound component node '{GetDisplayName()}' requires an explicit component reference.", Id);
    }

    protected bool TryResolveComponent(GraphExecutionContext context, out IComponent component, out string error)
    {
        component = null;
        error = string.Empty;
        if (context?.ActionDependencyMode == GraphActionDependencyMode.HostBound &&
            Component?.IsAssigned != true &&
            !string.IsNullOrWhiteSpace(ComponentTypeName))
        {
            Component ??= new GraphActionComponentReference();
            Component.ComponentTypeName = ComponentTypeName;
            Component.ComponentSlot = 0;
        }
        if (!GraphComponentRegistry.TryGet(ComponentTypeName, out GraphComponentTypeDescriptor descriptor))
        {
            error = $"Unknown component type '{ComponentTypeName}'.";
            return false;
        }
        return GraphActionComponentResolver.TryResolve(context, Component, descriptor.ComponentType, GetDisplayName(), out component, out error);
    }
}

public class GraphComponentCallNodeData : GraphComponentNodeData, IFlowNode
{
    public List<GraphComponentArgument> ParameterValues { get; set; } = new();
    [JsonIgnore]
    public List<GraphComponentArgument> Arguments
    {
        get => ParameterValues;
        set => ParameterValues = value ?? new();
    }
    public string ExecutionMode { get; set; } = "OnEnter";
    public GraphActionStatus LastStatus { get; private set; } = GraphActionStatus.Failure;

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

    public override List<string> GetGraphTypes() => new() { FlowGraphAsset.GraphTypeName, GameLogic.CharacterGraphAsset.CharacterGraphTypeName };
    public override string GetDisplayName() => "Component Call";
    public override string GetMenuName() => "Component Call";
    public override string GetCategory() => "Component / Action";
    public override Color GetNodeColor() => new(0.72f, 0.48f, 0.88f);
    public override int GetInputCount() => 1 + (ComponentType?.Actions.FirstOrDefault(value => value.MemberId == MemberId)?.Parameters.Count ?? 0);
    public override int GetOutputCount() => 2;
    public override string GetInputPortName(int port) => port == 0 ? "Execute" : ResolveAction()?.Parameters[port - 1].Name ?? $"Arg {port - 1}";
    public override string GetOutputPortName(int port) => port == 0 ? "Success" : "Failure";
    public override int GetInputPortType(int port) => port == 0 ? 0 : GraphComponentPortTypes.FromType(ResolveAction()?.Parameters[port - 1].ValueType);
    public override int GetOutputPortType(int port) => 0;
    public override bool CanBePrime() => false;

    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        LastStatus = Invoke(context);
    }

    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta)
    {
        if (LastStatus == GraphActionStatus.Running)
            LastStatus = Invoke(context);
    }

    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        if (LastStatus == GraphActionStatus.Running)
        {
            completion = default;
            return false;
        }
        completion = new NodeCompletion(LastStatus == GraphActionStatus.Success ? 0 : 1);
        return true;
    }

    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) { }

    public override void CreateNodeUI(GraphEditorContext context)
    {
        context.GraphNode.AddChild(new Label { Text = ResolveAction()?.DisplayName ?? "Component Call", HorizontalAlignment = HorizontalAlignment.Center });
    }

    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        root.AddChild(Component.CreateEditUI("Component", context, () => { }));
        AddField(root, "Component type", ComponentTypeName, value => ComponentTypeName = value);
        AddField(root, "Action ID", MemberId, value => MemberId = value);
        InitializeArguments();
        GraphComponentActionDescriptor action = ResolveAction();
        if (action != null)
        {
            root.AddChild(new HSeparator());
            root.AddChild(new Label { Text = "Arguments" });
            for (int i = 0; i < action.Parameters.Count; i++)
            {
                root.AddChild(new Label { Text = action.Parameters[i].Name });
                root.AddChild(Arguments[i].Value.CreateEditUI(context));
            }
        }
        return root;
    }

    private static void AddField(VBoxContainer root, string label, string value, Action<string> setter)
    {
        root.AddChild(new Label { Text = label });
        var edit = new LineEdit { Text = value ?? string.Empty };
        edit.TextChanged += text => setter(text);
        root.AddChild(edit);
    }

    private GraphComponentActionDescriptor ResolveAction() => ComponentType?.Actions.FirstOrDefault(value => value.MemberId == MemberId);

    private GraphActionStatus Invoke(GraphExecutionContext context)
    {
        InitializeArguments();
        object[] values = Arguments?.Select(value => value?.Value?.GetObjectValue()).ToArray() ?? Array.Empty<object>();
        GraphComponentActionDescriptor action = ResolveAction();
        if (action?.UseInputEventValue == true && values.Length > 0)
        {
            GameLogic.CharacterInputEventContext input = context?.GetUserData<GameLogic.CharacterInputEventContext>();
            if (input != null)
                values[0] = input.Value;
        }
        IComponent component = null;
        string resolveError = string.Empty;
        string invokeError = string.Empty;
        GraphActionStatus status;
        if (!TryResolveComponent(context, out component, out resolveError) ||
            !GraphComponentInvoker.TryInvokeComponent(component, ComponentTypeName, MemberId, values, out status, out _, out invokeError))
        {
            string message = string.IsNullOrWhiteSpace(resolveError) ? invokeError : resolveError;
            GD.PushError($"[GraphComponentCallNode] {message}");
            return GraphActionStatus.Failure;
        }
        return status;
    }

    private static GraphBlackboardValue CreateDefaultValue(Type type)
    {
        if (type == typeof(bool)) return new GraphBoolBlackboardValue();
        if (type == typeof(int) || type.IsEnum) return new GraphIntBlackboardValue();
        if (type == typeof(float) || type == typeof(double)) return new GraphFloatBlackboardValue();
        return new GraphStringBlackboardValue();
    }
}

public class GraphComponentGetNodeData : GraphComponentNodeData, IFlowNode
{
    private bool _lastSucceeded;

    public override List<string> GetGraphTypes() => new() { FlowGraphAsset.GraphTypeName, GameLogic.CharacterGraphAsset.CharacterGraphTypeName };
    public override string GetDisplayName() => "Component Get";
    public override string GetMenuName() => "Component Get";
    public override string GetCategory() => "Component / Value";
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;
    public override string GetInputPortName(int port) => "Execute";
    public override string GetOutputPortName(int port) => ResolveValue()?.DisplayName ?? "Value";
    public override int GetOutputPortType(int port) => GraphComponentPortTypes.FromType(ResolveValue()?.ValueType);
    public override bool CanBePrime() => false;

    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        root.AddChild(Component.CreateEditUI("Component", context, () => { }));
        AddField(root, "Component type", ComponentTypeName, value => ComponentTypeName = value);
        AddField(root, "Member ID", MemberId, value => MemberId = value);
        return root;
    }

    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        _lastSucceeded = Read(context, runtime);
        runtime.SetNodeData(Id, _lastSucceeded);
    }

    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }
    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        completion = new NodeCompletion(_lastSucceeded ? 0 : 1);
        return true;
    }
    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) { }

    public override void Execute(GraphExecutionContext context) => Read(context, null);

    private bool Read(GraphExecutionContext context, FlowGraphRuntime runtime)
    {
        IComponent component = null;
        string resolveError = string.Empty;
        string error = string.Empty;
        object value;
        if (!TryResolveComponent(context, out component, out resolveError) ||
            !GraphComponentInvoker.TryReadComponent(component, ComponentTypeName, MemberId, out value, out error))
        {
            GD.PushError($"[GraphComponentGetNode] {(string.IsNullOrWhiteSpace(resolveError) ? error : resolveError)}");
            return false;
        }
        runtime?.SetNodeOutput(Id, value);
        return true;
    }

    private GraphComponentValueDescriptor ResolveValue() => ComponentType?.Values.FirstOrDefault(value => value.MemberId == MemberId);

    private static void AddField(VBoxContainer root, string label, string value, Action<string> setter)
    {
        root.AddChild(new Label { Text = label });
        var edit = new LineEdit { Text = value ?? string.Empty };
        edit.TextChanged += text => setter(text);
        root.AddChild(edit);
    }
}

public class GraphComponentSetNodeData : GraphComponentNodeData, IFlowNode
{
    public GraphBlackboardValue Value { get; set; } = new GraphStringBlackboardValue();
    private bool _lastSucceeded;

    public override List<string> GetGraphTypes() => new() { FlowGraphAsset.GraphTypeName, GameLogic.CharacterGraphAsset.CharacterGraphTypeName };
    public override string GetDisplayName() => "Component Set";
    public override string GetMenuName() => "Component Set";
    public override string GetCategory() => "Component / Value";
    public override int GetInputCount() => 2;
    public override int GetOutputCount() => 1;
    public override string GetInputPortName(int port) => port == 0 ? "Execute" : ResolveValue()?.DisplayName ?? "Value";
    public override int GetInputPortType(int port) => port == 0 ? 0 : GraphComponentPortTypes.FromType(ResolveValue()?.ValueType);
    public override string GetOutputPortName(int port) => "Completed";
    public override bool CanBePrime() => false;

    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        root.AddChild(Component.CreateEditUI("Component", context, () => { }));
        AddField(root, "Component type", ComponentTypeName, value => ComponentTypeName = value);
        AddField(root, "Member ID", MemberId, value => MemberId = value);
        GraphComponentValueDescriptor descriptor = ResolveValue();
        if (descriptor != null)
        {
            Value = EnsureDefaultValue(Value, descriptor.ValueType);
            root.AddChild(new Label { Text = "Value (used when unconnected)" });
            root.AddChild(Value.CreateEditUI(context));
        }
        return root;
    }

    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        _lastSucceeded = Write(context, runtime);
        runtime.SetNodeData(Id, _lastSucceeded);
    }
    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }
    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        completion = new NodeCompletion(_lastSucceeded ? 0 : 1);
        return true;
    }
    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) { }

    public override void Execute(GraphExecutionContext context) => Write(context, null);

    private bool Write(GraphExecutionContext context, FlowGraphRuntime runtime)
    {
        string error = string.Empty;
        if (!TryGetConnectedValue(runtime, out object value))
        {
            value = Value?.GetObjectValue();
        }
        IComponent component = null;
        string resolveError = string.Empty;
        if (!TryResolveComponent(context, out component, out resolveError) ||
            !GraphComponentInvoker.TryWriteComponent(component, ComponentTypeName, MemberId, value, out error))
        {
            GD.PushError($"[GraphComponentSetNode] {(string.IsNullOrWhiteSpace(resolveError) ? error : resolveError)}");
            return false;
        }
        return true;
    }

    private bool TryGetConnectedValue(FlowGraphRuntime runtime, out object value)
    {
        value = null;
        if (runtime == null || runtime.Graph == null)
            return false;
        foreach (GraphConnection connection in runtime.Graph.GetIncomingConnections(Id, 1))
        {
            if (runtime.TryGetNodeOutput(connection.FromNode, out value))
                return true;
        }
        return false;
    }

    private GraphComponentValueDescriptor ResolveValue() => ComponentType?.Values.FirstOrDefault(value => value.MemberId == MemberId);

    private static GraphBlackboardValue EnsureDefaultValue(GraphBlackboardValue value, Type type)
    {
        if (value != null && value.ValueType == type)
            return value;
        if (type == typeof(bool)) return new GraphBoolBlackboardValue();
        if (type == typeof(int) || type.IsEnum) return new GraphIntBlackboardValue();
        if (type == typeof(float) || type == typeof(double)) return new GraphFloatBlackboardValue();
        if (type == typeof(Vector2)) return new GraphVector2BlackboardValue();
        if (type == typeof(Vector3)) return new GraphVector3BlackboardValue();
        if (type == typeof(Color)) return new GraphColorBlackboardValue();
        return new GraphStringBlackboardValue();
    }

    private static void AddField(VBoxContainer root, string label, string value, Action<string> setter)
    {
        root.AddChild(new Label { Text = label });
        var edit = new LineEdit { Text = value ?? string.Empty };
        edit.TextChanged += text => setter(text);
        root.AddChild(edit);
    }
}

public class BehaviorTreeComponentCallNodeData : BehaviorTreeNodeData
{
    public GraphComponentCallAction Call { get; set; } = new();
    public override string GetDisplayName() => "Component Call";
    public override string GetCategory() => "Component/Action";
    public override Color GetNodeColor() => new(0.72f, 0.48f, 0.88f);
    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta) => Call?.Tick(runtime, context, delta) ?? BehaviorTreeStatus.Failure;
    public override void Abort(BehaviorTreeRuntime runtime, GraphExecutionContext context) => Call?.Abort(runtime, context);
    public override void CreateNodeUI(GraphEditorContext context)
    {
        context.GraphNode.AddChild(new Label { Text = Call?.Description ?? "Component Call", HorizontalAlignment = HorizontalAlignment.Center });
    }
    public override Control CreateInspectorUI(GraphEditorContext context) => Call?.CreateEditUI(context);
    public override void Validate(GraphAsset graph, GraphValidationResult result) => Call?.Validate(graph, Id, result);
}

public sealed class BehaviorTreeComponentGetNodeData : BehaviorTreeNodeData
{
    public GraphActionComponentReference Component { get; set; } = new();
    public string ComponentTypeName { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string OutputKey { get; set; } = string.Empty;

    public override string GetDisplayName() => "Component Get";
    public override string GetCategory() => "Component/Value";
    public override void Validate(GraphAsset graph, GraphValidationResult result)
    {
        if (graph?.ActionDependencyMode == GraphActionDependencyMode.HostBound && Component?.IsAssigned != true)
            result.AddError("HostBound component get node requires an explicit component reference.", Id);
    }
    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        root.AddChild(Component.CreateEditUI("Component", context, () => { }));
        AddField(root, "Component type", ComponentTypeName, value => ComponentTypeName = value);
        AddField(root, "Member ID", MemberId, value => MemberId = value);
        AddField(root, "Output blackboard key", OutputKey, value => OutputKey = value);
        return root;
    }
    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        GraphComponentTypeDescriptor descriptor = null;
        IComponent component = null;
        string resolveError = string.Empty;
        string error = string.Empty;
        object value;
        if (!GraphComponentRegistry.TryGet(ComponentTypeName, out descriptor) ||
            !GraphActionComponentResolver.TryResolve(context, Component, descriptor.ComponentType, GetDisplayName(), out component, out resolveError) ||
            !GraphComponentInvoker.TryReadComponent(component, ComponentTypeName, MemberId, out value, out error))
        {
            Godot.GD.PushError($"[BehaviorTreeComponentGetNode] {(string.IsNullOrWhiteSpace(resolveError) ? error : resolveError)}");
            return BehaviorTreeStatus.Failure;
        }
        return !string.IsNullOrWhiteSpace(OutputKey) && context.Blackboard.SetValue(OutputKey, value)
            ? BehaviorTreeStatus.Success
            : BehaviorTreeStatus.Failure;
    }

    private static void AddField(VBoxContainer root, string label, string value, Action<string> setter)
    {
        root.AddChild(new Label { Text = label });
        var edit = new LineEdit { Text = value ?? string.Empty };
        edit.TextChanged += text => setter(text);
        root.AddChild(edit);
    }
}

public sealed class BehaviorTreeComponentSetNodeData : BehaviorTreeNodeData
{
    public GraphActionComponentReference Component { get; set; } = new();
    public string ComponentTypeName { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string InputKey { get; set; } = string.Empty;

    public override string GetDisplayName() => "Component Set";
    public override string GetCategory() => "Component/Value";
    public override void Validate(GraphAsset graph, GraphValidationResult result)
    {
        if (graph?.ActionDependencyMode == GraphActionDependencyMode.HostBound && Component?.IsAssigned != true)
            result.AddError("HostBound component set node requires an explicit component reference.", Id);
    }
    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        root.AddChild(Component.CreateEditUI("Component", context, () => { }));
        AddField(root, "Component type", ComponentTypeName, value => ComponentTypeName = value);
        AddField(root, "Member ID", MemberId, value => MemberId = value);
        AddField(root, "Input blackboard key", InputKey, value => InputKey = value);
        return root;
    }
    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        if (string.IsNullOrWhiteSpace(InputKey) || !context.Blackboard.TryGetValue(InputKey, out object value))
            return BehaviorTreeStatus.Failure;
        GraphComponentTypeDescriptor descriptor = null;
        IComponent component = null;
        string resolveError = string.Empty;
        string error = string.Empty;
        if (!GraphComponentRegistry.TryGet(ComponentTypeName, out descriptor) ||
            !GraphActionComponentResolver.TryResolve(context, Component, descriptor.ComponentType, GetDisplayName(), out component, out resolveError) ||
            !GraphComponentInvoker.TryWriteComponent(component, ComponentTypeName, MemberId, value, out error))
        {
            Godot.GD.PushError($"[BehaviorTreeComponentSetNode] {(string.IsNullOrWhiteSpace(resolveError) ? error : resolveError)}");
            return BehaviorTreeStatus.Failure;
        }
        return BehaviorTreeStatus.Success;
    }

    private static void AddField(VBoxContainer root, string label, string value, Action<string> setter)
    {
        root.AddChild(new Label { Text = label });
        var edit = new LineEdit { Text = value ?? string.Empty };
        edit.TextChanged += text => setter(text);
        root.AddChild(edit);
    }
}

namespace GameLogic
{
    public class HfsmComponentActionStateNodeData : HfsmStateNodeData
    {
        public GraphComponentCallAction Call { get; set; } = new();
        public override string GetMenuName() => "Component Action State";
        public override string GetDisplayName() => string.IsNullOrWhiteSpace(StateName) ? "Component Action" : StateName;
        public override Color GetNodeColor() => new(0.72f, 0.48f, 0.88f);
        public override void OnEnter(HfsmRuntime runtime) => Call?.Execute(runtime.Context);
        public override void OnUpdate(HfsmRuntime runtime, double delta)
        {
            if (Call?.LastStatus == GraphActionStatus.Running)
                Call.Tick(null, runtime.Context, delta);
        }
        public override void OnExit(HfsmRuntime runtime)
        {
            Call?.Abort(null, runtime?.Context);
        }
        public override bool TryGetCompletion(HfsmRuntime runtime, out NodeCompletion completion)
        {
            if (Call?.LastStatus == GraphActionStatus.Running)
            {
                completion = default;
                return false;
            }
            completion = new NodeCompletion(Call?.LastStatus == GraphActionStatus.Success ? 0 : 1);
            return true;
        }
        public override Control CreateInspectorUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            root.AddChild(base.CreateInspectorUI(context));
            root.AddChild(Call?.CreateEditUI(context));
            return root;
        }
    }
}
