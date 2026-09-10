using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Godot;

public static class GraphComponentPortTypes
{
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

public abstract class GraphComponentNodeData : GraphNodeData
{
    public string ComponentTypeName { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;

    protected GraphComponentTypeDescriptor ComponentType =>
        GraphComponentRegistry.TryGet(ComponentTypeName, out GraphComponentTypeDescriptor descriptor) ? descriptor : null;
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
        if (!GraphComponentInvoker.TryInvoke(context?.GetUserData<GameLogic.GameObject2D>(), ComponentTypeName, MemberId, values, out GraphActionStatus status, out _, out string error))
        {
            GD.PushError($"[GraphComponentCallNode] {error}");
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
    public string OutputKey { get; set; } = string.Empty;
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
        AddField(root, "Component type", ComponentTypeName, value => ComponentTypeName = value);
        AddField(root, "Member ID", MemberId, value => MemberId = value);
        AddField(root, "Output blackboard key", OutputKey, value => OutputKey = value);
        return root;
    }

    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        _lastSucceeded = Read(context);
        runtime.SetNodeData(Id, _lastSucceeded);
    }

    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }
    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        completion = new NodeCompletion(_lastSucceeded ? 0 : 1);
        return true;
    }
    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) { }

    public override void Execute(GraphExecutionContext context) => Read(context);

    private bool Read(GraphExecutionContext context)
    {
        if (!GraphComponentInvoker.TryRead(context?.GetUserData<GameLogic.GameObject2D>(), ComponentTypeName, MemberId, out object value, out string error))
        {
            GD.PushError($"[GraphComponentGetNode] {error}");
            return false;
        }
        return !string.IsNullOrWhiteSpace(OutputKey) && context.Blackboard.SetValue(OutputKey, value);
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
    public string InputKey { get; set; } = string.Empty;
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
        AddField(root, "Component type", ComponentTypeName, value => ComponentTypeName = value);
        AddField(root, "Member ID", MemberId, value => MemberId = value);
        AddField(root, "Input blackboard key", InputKey, value => InputKey = value);
        return root;
    }

    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        _lastSucceeded = Write(context);
        runtime.SetNodeData(Id, _lastSucceeded);
    }
    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }
    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        completion = new NodeCompletion(_lastSucceeded ? 0 : 1);
        return true;
    }
    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) { }

    public override void Execute(GraphExecutionContext context) => Write(context);

    private bool Write(GraphExecutionContext context)
    {
        string error = string.Empty;
        if (!context.Blackboard.TryGetValue(InputKey, out object value))
        {
            GD.PushError($"[GraphComponentSetNode] Blackboard key '{InputKey}' is missing.");
            return false;
        }
        if (!GraphComponentInvoker.TryWrite(context?.GetUserData<GameLogic.GameObject2D>(), ComponentTypeName, MemberId, value, out error))
        {
            GD.PushError($"[GraphComponentSetNode] {error}");
            return false;
        }
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
}

public sealed class BehaviorTreeComponentGetNodeData : BehaviorTreeNodeData
{
    public string ComponentTypeName { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string OutputKey { get; set; } = string.Empty;

    public override string GetDisplayName() => "Component Get";
    public override string GetCategory() => "Component/Value";
    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        AddField(root, "Component type", ComponentTypeName, value => ComponentTypeName = value);
        AddField(root, "Member ID", MemberId, value => MemberId = value);
        AddField(root, "Output blackboard key", OutputKey, value => OutputKey = value);
        return root;
    }
    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        if (!GraphComponentInvoker.TryRead(context?.GetUserData<GameLogic.GameObject2D>(), ComponentTypeName, MemberId, out object value, out string error))
        {
            Godot.GD.PushError($"[BehaviorTreeComponentGetNode] {error}");
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
    public string ComponentTypeName { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string InputKey { get; set; } = string.Empty;

    public override string GetDisplayName() => "Component Set";
    public override string GetCategory() => "Component/Value";
    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        AddField(root, "Component type", ComponentTypeName, value => ComponentTypeName = value);
        AddField(root, "Member ID", MemberId, value => MemberId = value);
        AddField(root, "Input blackboard key", InputKey, value => InputKey = value);
        return root;
    }
    public override BehaviorTreeStatus Tick(BehaviorTreeRuntime runtime, GraphExecutionContext context, double delta)
    {
        if (string.IsNullOrWhiteSpace(InputKey) || !context.Blackboard.TryGetValue(InputKey, out object value))
            return BehaviorTreeStatus.Failure;
        if (!GraphComponentInvoker.TryWrite(context?.GetUserData<GameLogic.GameObject2D>(), ComponentTypeName, MemberId, value, out string error))
        {
            Godot.GD.PushError($"[BehaviorTreeComponentSetNode] {error}");
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
