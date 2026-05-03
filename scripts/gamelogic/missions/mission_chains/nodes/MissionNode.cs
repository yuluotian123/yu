using System.Collections.Generic;
using System.Text.Json.Serialization;
using GameLogic;
using Godot;

/// <summary>
/// MissionGraph 中代表一个真实 Mission 的节点。
/// 
/// 节点进入时不会自己完成，而是把 Mission 部署请求交给 MissionGraphRuntime。
/// MissionManager 后续回调任务完成/移除时，runtime 才会把 completion 写回节点。
/// </summary>
public class MissionNode : GraphNodeData, IFlowNode
{
    /// <summary>
    /// 该 Mission 需要满足的需求模板。
    /// </summary>
    [JsonInclude]
    private readonly List<MissionRequireTemplate> _requires =
          new List<MissionRequireTemplate>();

    /// <summary>
    /// 多个需求的组合模式。
    /// </summary>
    [JsonInclude]
    private MissionRequireMode _mode;

    public override List<string> GetGraphTypes()
        => new List<string> { "MissionGraph" };
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;

    /// <summary>
    /// 根据当前节点配置创建 Mission 原型。
    /// GraphName 参数历史上叫 GraphName，新的调用语义实际传入的是 graphPath。
    /// 生成的 MissionId 为 graphPath.nodeId。
    /// </summary>
    public MissionPrototype<object> CreateMissionProto(string GraphName = "")
    {
        var proto = new MissionPrototype<object>(GraphName + "." + Id, _requires.ToArray(), _mode);
        return proto;
    }

    /// <summary>
    /// FlowGraphRuntime 进入节点时调用。
    /// MissionNode 只排队部署请求，等待 Manager drain 后创建真实 Mission。
    /// </summary>
    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        if (runtime is MissionGraphRuntime missionRuntime)
        {
            missionRuntime.QueueMission(this);
            return;
        }

        GD.PushWarning($"[MissionNode] MissionNode can only run inside MissionGraphRuntime: {Id}");
    }

    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }

    /// <summary>
    /// 查询 Mission 是否已经完成。
    /// completion 由 MissionGraphRuntime.OnMissionCompleted 或部署失败路径写入，只消费一次。
    /// </summary>
    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        if (runtime is MissionGraphRuntime missionRuntime)
            return missionRuntime.TryConsumeNodeCompletion(Id, out completion);

        completion = NodeCompletion.NoOutput("MissingMissionRuntime");
        return true;
    }

    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) { }

    /// <summary>
    /// 编辑器内的需求列表 UI。
    /// </summary>
    public override void CreateUI(GraphEditorContext context)
    {
        base.CreateUI(context);

        var listControl = new ReorderableListControl<MissionRequireTemplate>(
            items: _requires,
            buildItemUi: require => require.CreateEditUI(context),
            getItemLabel: require => require.GetType().Name,
            availableTypes: SubTypeCache.GetSubTypes<MissionRequireTemplate>(),
            factory: type => (MissionRequireTemplate)System.Activator.CreateInstance(type)
        );

        context.GraphNode.AddChild(listControl.Build());

    }

}
