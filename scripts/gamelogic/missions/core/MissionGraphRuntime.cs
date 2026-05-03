using System;
using System.Collections.Generic;
using System.Linq;
using Framework;
using Godot;

namespace GameLogic
{
    /// <summary>
    /// MissionGraph 的业务运行时。
    /// 
    /// 它直接继承 FlowGraphRuntime，复用 FlowGraph 的节点进入、节点完成、Sequence/Parallel 连线推进。
    /// Mission 层只补充三类业务状态：
    /// 1. MissionNode 对应的真实 Mission 部署和完成结果。
    /// 2. MissionSubGraphNodeData 对应的子 MissionGraphRuntime。
    /// 3. 保存恢复所需的 Mission metadata。
    /// </summary>
    public sealed class MissionGraphRuntime : FlowGraphRuntime, IDisposable
    {
        /// <summary>
        /// MissionNode.Enter 只把部署请求排进这里。
        /// 真正创建 Mission 的动作由 MissionChainManager.DrainDeployments 统一执行。
        /// </summary>
        private readonly Queue<MissionDeploymentRequest> _pendingDeployments = new();

        /// <summary>
        /// 正在等待 MissionManager 回调的 Mission。
        /// key 是 MissionId(graphPath.nodeId)，value 是对应 MissionNode.Id。
        /// </summary>
        private readonly Dictionary<string, string> _activeMissionNodes = new();

        /// <summary>
        /// 正在等待子图完成的 SubGraphNode。
        /// key 是 childGraphPath，value 是对应 MissionSubGraphNodeData.Id。
        /// </summary>
        private readonly Dictionary<string, string> _pendingSubGraphNodes = new();

        /// <summary>
        /// 运行中的子 MissionGraphRuntime。
        /// ChildScopes 会把它们暴露给统一 runtime scope 遍历。
        /// </summary>
        private readonly Dictionary<string, MissionGraphRuntime> _childRuntimes = new();

        /// <summary>
        /// Mission/SubGraph 的完成结果暂存区。
        /// IFlowNode.TryGetCompletion 会消费这里的结果，再交给 FlowGraphRuntime 推进连线。
        /// </summary>
        private readonly Dictionary<string, NodeCompletion> _nodeCompletions = new();

        private bool _disposed;

        /// <summary>
        /// 创建 MissionGraphRuntime。
        /// parentBlackboard 存在时会 fork 一份给子图，避免子图直接污染父图当前 local scope。
        /// </summary>
        public MissionGraphRuntime(
            MissionGraph graph,
            MissionChainManager manager,
            string graphPath,
            GraphBlackboardRuntime parentBlackboard = null,
            MissionGraphRuntime parentRuntime = null)
            : base(graph, new GraphExecutionContext(
                graph,
                parentBlackboard?.Fork() ?? new GraphBlackboardRuntime()))
        {
            Manager = manager;
            GraphPath = graphPath ?? string.Empty;
            ParentRuntime = parentRuntime;
            Context.UserData.Add(this);
            if (manager != null)
                Context.UserData.Add(manager);
        }

        /// <summary>
        /// 业务侧强类型图资源。
        /// </summary>
        public new MissionGraph Graph => base.Graph as MissionGraph;

        /// <summary>
        /// 所属 Manager，负责真正部署 Mission 和维护 runtime 字典。
        /// </summary>
        public MissionChainManager Manager { get; }

        /// <summary>
        /// 当前 runtime 在整棵 MissionGraph 树里的稳定路径。
        /// MissionRuntimeId 会使用 GraphPath + "." + nodeId 生成真实 MissionId。
        /// </summary>
        public string GraphPath { get; }

        /// <summary>
        /// 父 MissionGraphRuntime。根图为 null。
        /// </summary>
        public MissionGraphRuntime ParentRuntime { get; }

        /// <summary>
        /// 暴露子图 runtime，供统一 runtime scope 遍历。
        /// </summary>
        public override IEnumerable<IGraphRuntimeScope> ChildScopes => _childRuntimes.Values;

        /// <summary>
        /// MissionGraph 完成条件比 FlowGraph 更严格：
        /// Flow active 节点清空只是基础条件，还必须没有待部署 Mission、活跃 Mission、等待中的子图。
        /// </summary>
        public override bool IsCompleted =>
            base.IsCompleted &&
            _pendingDeployments.Count == 0 &&
            _activeMissionNodes.Count == 0 &&
            _pendingSubGraphNodes.Count == 0;

        /// <summary>
        /// 启动 FlowGraph。
        /// </summary>
        public override bool Start()
        {
            if (GraphPath.Contains('.'))
            {
                Debugger.Warn($"[MissionGraphRuntime] GraphPath can not contain '.': {GraphPath}");
                return false;
            }

            bool started = base.Start();
            if (!started)
                return false;

            return true;
        }

        /// <summary>
        /// 停止当前 runtime 以及所有子 runtime，并清空 Mission 层等待状态。
        /// </summary>
        public override void Stop()
        {
            foreach (MissionGraphRuntime child in _childRuntimes.Values.ToList())
                child.Stop();

            _childRuntimes.Clear();
            _pendingDeployments.Clear();
            _activeMissionNodes.Clear();
            _pendingSubGraphNodes.Clear();
            _nodeCompletions.Clear();

            base.Stop();
        }

        /// <summary>
        /// 释放 runtime。Dispose 会走 Stop，确保 debug handle 和子 runtime 都被清理。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Stop();
        }

        /// <summary>
        /// MissionNode 进入时调用。
        /// 
        /// 这里不直接调用 MissionManager.StartMission，而是创建部署请求。
        /// Manager drain 成功后会进入 _activeMissionNodes；失败则写入 NoOutput completion。
        /// </summary>
        public void QueueMission(MissionNode node)
        {
            if (node == null)
                return;

            string missionId = MissionRuntimeId.Create(GraphPath, node.Id);
            if (_activeMissionNodes.ContainsKey(missionId) ||
                _pendingDeployments.Any(request => request.MissionId == missionId))
            {
                return;
            }

            _pendingDeployments.Enqueue(new MissionDeploymentRequest
            {
                MissionId = missionId,
                NodeId = node.Id,
                Prototype = node.CreateMissionProto(GraphPath)
            });
        }

        /// <summary>
        /// 由 MissionChainManager 调用，取出一个待部署 Mission。
        /// </summary>
        public bool TryDequeueDeployment(out MissionDeploymentRequest request)
        {
            if (_pendingDeployments.Count > 0)
            {
                request = _pendingDeployments.Dequeue();
                return true;
            }

            request = null;
            return false;
        }

        /// <summary>
        /// MissionManager.StartMission 成功后调用。
        /// 从这一刻起，MissionNode 会等待 OnMissionCompleted 写入 completion。
        /// </summary>
        public void MarkMissionDeployed(MissionDeploymentRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.MissionId))
                return;

            _activeMissionNodes[request.MissionId] = request.NodeId;
        }

        /// <summary>
        /// MissionManager.StartMission 失败后调用。
        /// 
        /// 使用 NoOutput completion 是有意的：节点会结束 active 状态，但不会推进 Sequence 连线。
        /// </summary>
        public void MarkMissionDeploymentFailed(MissionDeploymentRequest request)
        {
            if (request == null)
                return;

            CompleteNode(request.NodeId, NodeCompletion.NoOutput("MissionStartFailed"));
        }

        /// <summary>
        /// MissionManager 移除 Mission 时调用。
        /// continues=true 代表正常完成并推进 Sequence；
        /// continues=false 代表取消/移除，只结束节点，不继续下游。
        /// </summary>
        public bool OnMissionCompleted(string missionId, bool continues)
        {
            if (!MissionRuntimeId.TryParse(missionId, out string graphPath, out string nodeId) ||
                graphPath != GraphPath ||
                !_activeMissionNodes.Remove(missionId))
            {
                return false;
            }

            CompleteNode(nodeId, continues
                ? NodeCompletion.Completed("MissionCompleted")
                : NodeCompletion.NoOutput("MissionRemoved"));

            return true;
        }

        /// <summary>
        /// MissionSubGraphNodeData 进入时调用，启动一个子 MissionGraphRuntime。
        /// 子图完成后会通过 MissionChainManager.CompleteIfNeeded 回调 OnSubGraphCompleted。
        /// </summary>
        public void StartSubGraph(MissionSubGraphNodeData node)
        {
            if (node == null)
                return;

            if (node.GetSubGraph() is not MissionGraph subGraph)
            {
                CompleteNode(node.Id, NodeCompletion.NoOutput("MissingSubGraph"));
                Debugger.Warn($"[MissionGraphRuntime] Missing MissionGraph subgraph: {node.SubGraphPath}");
                return;
            }

            string childGraphPath = GraphPath + "/" + subGraph.graphName;

            // 同一个子图路径已经在等待时，不重复启动，避免重复创建子 runtime。
            if (_pendingSubGraphNodes.ContainsKey(childGraphPath))
                return;

            // 先登记 pending，再启动子图。
            // 子图可能在 StartSubChain 内同步完成；提前登记才能接住完成回调。
            _pendingSubGraphNodes[childGraphPath] = node.Id;
            MissionGraphRuntime childRuntime = Manager?.StartSubChain(subGraph, childGraphPath, Context.Blackboard, this);
            if (childRuntime == null)
            {
                _pendingSubGraphNodes.Remove(childGraphPath);
                CompleteNode(node.Id, NodeCompletion.NoOutput("SubGraphStartFailed"));
                return;
            }

        }

        /// <summary>
        /// 子 MissionGraphRuntime 完成时调用。
        /// 会把对应 SubGraphNode 写成 Completed，让 FlowGraphRuntime 在下一次 Update 推进 Sequence。
        /// </summary>
        public bool OnSubGraphCompleted(string childGraphPath)
        {
            if (!_pendingSubGraphNodes.Remove(childGraphPath, out string nodeId))
                return false;

            _childRuntimes.Remove(childGraphPath);
            CompleteNode(nodeId, NodeCompletion.Completed("SubGraphCompleted"));
            return true;
        }

        /// <summary>
        /// MissionNode / MissionSubGraphNodeData 的 TryGetCompletion 会调用这里。
        /// 返回 true 时 completion 会被消费一次，随后由 FlowGraphRuntime 处理退出和连线推进。
        /// </summary>
        public bool TryConsumeNodeCompletion(string nodeId, out NodeCompletion completion)
        {
            if (!string.IsNullOrWhiteSpace(nodeId) &&
                _nodeCompletions.TryGetValue(nodeId, out completion))
            {
                _nodeCompletions.Remove(nodeId);
                return true;
            }

            completion = default;
            return false;
        }

        /// <summary>
        /// 由 Manager 在 runtime 注册到字典后调用。
        /// </summary>
        public void AttachChildRuntime(MissionGraphRuntime childRuntime)
        {
            if (childRuntime == null || string.IsNullOrWhiteSpace(childRuntime.GraphPath))
                return;

            _childRuntimes[childRuntime.GraphPath] = childRuntime;
        }

        /// <summary>
        /// 子 runtime 完成或启动失败时，从 ChildScopes 中移除。
        /// </summary>
        public void DetachChildRuntime(string childGraphPath)
        {
            if (!string.IsNullOrWhiteSpace(childGraphPath))
                _childRuntimes.Remove(childGraphPath);
        }

        /// <summary>
        /// 创建可保存状态。
        /// 注意：Mission 本身的 handle 状态由 MissionChainSaver.Missions 保存；
        /// 这里保存的是图 runtime 等待关系和子图树。
        /// </summary>
        public MissionGraphRuntimeState CreateState()
        {
            var state = new MissionGraphRuntimeState
            {
                GraphPath = GraphPath,
                GraphResourcePath = Graph?.ResourcePath ?? string.Empty,
                ActiveMissionIds = _activeMissionNodes.Keys.ToList(),
                PendingSubGraphPaths = _pendingSubGraphNodes.Keys.ToList()
            };

            foreach (MissionGraphRuntime child in _childRuntimes.Values)
                state.ChildStates.Add(child.CreateState());

            return state;
        }

        /// <summary>
        /// 恢复 runtime 等待关系。
        /// 
        /// 这里不会调用 Start，也不会调用 MissionNode.Enter。
        /// RestoreActiveNodes 只是把实现 IFlowNode 的节点放回 active 列表，
        /// 等 MissionManager 或子图之后发出完成事件时，图才能继续推进。
        /// </summary>
        public void LoadState(MissionGraphRuntimeState state)
        {
            if (state == null)
                return;

            _activeMissionNodes.Clear();
            _pendingSubGraphNodes.Clear();
            _nodeCompletions.Clear();

            var activeNodeIds = new List<string>();
            for (int i = 0; i < state.ActiveMissionIds.Count; i++)
            {
                string missionId = state.ActiveMissionIds[i];
                if (!MissionRuntimeId.TryParse(missionId, out string graphPath, out string nodeId) ||
                    graphPath != GraphPath)
                {
                    continue;
                }

                _activeMissionNodes[missionId] = nodeId;
                activeNodeIds.Add(nodeId);
            }

            for (int i = 0; i < state.PendingSubGraphPaths.Count; i++)
            {
                string subGraphPath = state.PendingSubGraphPaths[i];
                string nodeId = FindSubGraphNodeId(subGraphPath);
                if (string.IsNullOrWhiteSpace(nodeId))
                    continue;

                _pendingSubGraphNodes[subGraphPath] = nodeId;
                activeNodeIds.Add(nodeId);
            }

            RestoreActiveNodes(activeNodeIds);
        }

        /// <summary>
        /// 写入一个节点完成结果，等待对应 IFlowNode.TryGetCompletion 消费。
        /// </summary>
        private void CompleteNode(string nodeId, NodeCompletion completion)
        {
            if (!string.IsNullOrWhiteSpace(nodeId))
                _nodeCompletions[nodeId] = completion;
        }

        /// <summary>
        /// 根据 childGraphPath 反查当前图中的 SubGraphNode。
        /// 保存恢复时只存 childGraphPath，加载资源后需要重新找到等待它的节点。
        /// </summary>
        private string FindSubGraphNodeId(string childGraphPath)
        {
            if (Graph == null || string.IsNullOrWhiteSpace(childGraphPath))
                return string.Empty;

            for (int i = 0; i < Graph.Nodes.Count; i++)
            {
                if (Graph.Nodes[i] is not MissionSubGraphNodeData subGraphNode)
                    continue;

                if (subGraphNode.GetSubGraph() is MissionGraph subGraph &&
                    GraphPath + "/" + subGraph.graphName == childGraphPath)
                {
                    return subGraphNode.Id;
                }
            }

            return string.Empty;
        }

    }
}
