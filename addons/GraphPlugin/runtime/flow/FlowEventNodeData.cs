using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public abstract class FlowEventNodeData : GraphNodeData, IFlowNode
{
    public string EventName { get; set; } = string.Empty;
    public override List<string> GetGraphTypes() => new() { FlowGraphAsset.GraphTypeName, GameLogic.CharacterGraphAsset.CharacterGraphTypeName };
    public override string GetCategory() => "流程 / 事件";
    public override Color GetNodeColor() => new(0.52f, 0.62f, 0.9f);
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;
    public override bool CanBePrime() => false;
    public override void Validate(GraphAsset graph, GraphValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(EventName)) result.AddError("事件名称不能为空。", Id);
    }
    public abstract void Enter(FlowGraphRuntime runtime, GraphExecutionContext context);
    public virtual void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }
    public abstract bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion);
    public virtual void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) => runtime.ClearNodeData(Id);
    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        var edit = new LineEdit { Text = EventName, PlaceholderText = "事件名称 / Event name" }; root.AddChild(edit);
        edit.TextChanged += text => { EventName = text.Trim(); context?.CurrentGraph?.MarkDirty(); };
        var known = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var node in context.CurrentGraph.Nodes.OfType<FlowEventNodeData>())
            if (!string.IsNullOrWhiteSpace(node.EventName)) known.Add(node.EventName);
        if (context.CurrentGraph is GameLogic.CharacterGraphAsset)
        {
            foreach (var value in Enum.GetNames<GameLogic.CharacterLifecycleEvent>()) known.Add("Lifecycle." + value);
            foreach (var input in context.CurrentGraph.Nodes.OfType<GameLogic.CharacterInputActionNodeData>()) known.Add("Input." + input.Id);
            foreach (var ability in context.CurrentGraph.Nodes.OfType<GameLogic.CharacterAbilityNodeData>())
                if (!string.IsNullOrWhiteSpace(ability.AbilityId))
                {
                    known.Add($"Ability.{ability.AbilityId}.Completed");
                    foreach (var value in Enum.GetNames<GameLogic.AbilityActivationResult>()) known.Add($"Ability.{ability.AbilityId}.{value}");
                }
        }
        var options = new OptionButton(); options.AddItem("选择已有事件…");
        foreach (var name in known) options.AddItem(name);
        options.ItemSelected += index =>
        {
            if (index == 0) return;
            EventName = options.GetItemText((int)index); edit.Text = EventName; context?.CurrentGraph?.MarkDirty();
        };
        root.AddChild(options); return root;
    }
}

public sealed class FlowPublishEventNodeData : FlowEventNodeData
{
    public override string GetMenuName() => "发送事件（Publish Event）";
    public override string GetDisplayName() => GetMenuName();
    public override void Enter(FlowGraphRuntime runtime, GraphExecutionContext context) => context.Events.Publish(EventName);
    public override bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    { completion = NodeCompletion.Completed(); return true; }
}

public sealed class FlowWaitEventNodeData : FlowEventNodeData
{
    public float Timeout { get; set; } = 5f;
    public override string GetMenuName() => "等待事件（Wait Event）";
    public override string GetDisplayName() => GetMenuName();
    public override int GetOutputCount() => 2;
    public override string GetOutputPortName(int port) => port == 0 ? "Received" : "Timeout";
    public override void Validate(GraphAsset graph, GraphValidationResult result)
    {
        base.Validate(graph, result);
        if (!float.IsFinite(Timeout) || Timeout <= 0) result.AddError("事件等待超时必须大于 0。", Id);
    }
    public override void Enter(FlowGraphRuntime runtime, GraphExecutionContext context) =>
        runtime.SetNodeData(Id, new WaitData { Version = context.Events.GetVersion(EventName) });
    public override void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) =>
        runtime.GetNodeData<WaitData>(Id).Elapsed += Math.Max(0, delta);
    public override bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        var data = runtime.GetNodeData<WaitData>(Id);
        bool received = context.Events.GetVersion(EventName) > data.Version;
        completion = new NodeCompletion(received ? 0 : 1);
        return received || data.Elapsed >= Timeout;
    }
    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = (VBoxContainer)base.CreateInspectorUI(context);
        root.AddChild(new Label { Text = "超时秒数（Timeout）" });
        var timeout = new SpinBox { MinValue = 0.01, MaxValue = 999999, Step = 0.05, Value = Timeout };
        timeout.ValueChanged += value => { Timeout = (float)value; context?.CurrentGraph?.MarkDirty(); };
        root.AddChild(timeout); return root;
    }
    private sealed class WaitData { public ulong Version; public double Elapsed; }
}
