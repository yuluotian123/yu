using System;
using System.Collections.Generic;
using Framework;
using GameLogic;
using Godot;

/// <summary>
/// MissionGraph 的子图节点。
/// 
/// 它复用 FlowGraph 的 IFlowNode 生命周期：
/// Enter 时启动子 MissionGraphRuntime，之后保持 active，
/// 直到子 runtime 完成并由父 runtime 写回 completion。
/// </summary>
public class MissionSubGraphNodeData : SubGraphNodeData, IFlowNode
{
    private MissionGraph _cachedMissionSubGraph;

    public override List<string> GetGraphTypes() => new() { MissionGraph.GraphTypeName };

    public override string GetDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(SubGraphPath))
            return $"SubGraph: {SubGraphPath.GetFile().GetBaseName()}";

        return "Mission SubGraph";
    }

    public override string GetMenuName() => "Mission SubGraph";

    /// <summary>
    /// 编辑器创建新子图资源时使用 MissionGraph。
    /// </summary>
    public override GraphAsset CreateSubGraphAsset()
    {
        return new MissionGraph();
    }

    /// <summary>
    /// 限制子图类型只能是 MissionGraph。
    /// </summary>
    public override Type GetSubGraphType()
    {
        return typeof(MissionGraph);
    }

    public override MissionGraph GetSubGraph()
    {
        if (_cachedMissionSubGraph != null)
            return _cachedMissionSubGraph;

        if (string.IsNullOrWhiteSpace(SubGraphPath))
            return null;

        _cachedMissionSubGraph = ModuleSystem
            .GetModule<IResourceModule>()
            .LoadAssetOnce<MissionGraph>(SubGraphPath);
        return _cachedMissionSubGraph;
    }

    public override void InvalidateCache()
    {
        _cachedMissionSubGraph = null;
        base.InvalidateCache();
    }

    /// <summary>
    /// 进入节点时启动子图。
    /// 子图是否能继续推进父图，由子 runtime 完成后回调决定。
    /// </summary>
    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        if (runtime is MissionGraphRuntime missionRuntime)
        {
            missionRuntime.StartSubGraph(this);
            return;
        }

        GD.PushWarning($"[MissionSubGraph] Mission subgraph can only run inside MissionGraphRuntime: {Id}");
    }

    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }

    /// <summary>
    /// 查询子图是否已经完成。
    /// completion 由 MissionGraphRuntime.OnSubGraphCompleted 写入，只消费一次。
    /// </summary>
    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        if (runtime is MissionGraphRuntime missionRuntime)
            return missionRuntime.TryConsumeNodeCompletion(Id, out completion);

        completion = NodeCompletion.NoOutput("MissingMissionRuntime");
        return true;
    }

    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) { }
}
