using System.Collections.Generic;
using System.Linq;
using Framework;
using Godot;

namespace GameLogic
{
    /// <summary>
    /// MissionGraph 的运行时总控。
    /// 
    /// 这个类刻意把“图推进”和“任务系统部署”分开：
    /// MissionGraphRuntime 只负责在 FlowGraph 中排队 Mission 部署请求，
    /// 真正调用 MissionManager.StartMission 的动作统一在这里 drain。
    /// 这样可以避免节点 Enter 过程中直接改 MissionManager 状态，也方便保存恢复和 debug。
    /// </summary>
    public class MissionChainManager : IMissionSystemComponent<object>
    {
        /// <summary>
        /// 现有任务系统，负责真正创建、保存、移除 Mission 实例。
        /// </summary>
        private readonly MissionManager<object> missionManager;

        /// <summary>
        /// 当前所有存活的 MissionGraphRuntime。
        /// 
        /// key 是 graphPath，不是 graphName，也不是 MissionId。
        /// 根图通常使用 graph.graphName，子图使用 parentGraphPath + "/" + subGraph.graphName。
        /// MissionRuntimeId 会把 graphPath 和 nodeId 拼成 graphPath.nodeId，所以 graphPath 不能包含 '.'。
        /// </summary>
        private readonly Dictionary<string, MissionGraphRuntime> runtimes = new();

        public MissionChainManager(MissionManager<object> missionManager)
        {
            this.missionManager = missionManager;
        }

        /// <summary>
        /// 启动一个根 MissionGraph。默认 graphPath 使用图资源自身的 graphName。
        /// </summary>
        public MissionGraphRuntime StartChain(MissionGraph chain)
        {
            return StartChain(chain, chain?.graphName, null, null);
        }

        /// <summary>
        /// 使用指定 graphPath 启动一个根 MissionGraph。
        /// 外部如果需要同时运行多个同名图，可以传入不同 graphPath。
        /// </summary>
        public MissionGraphRuntime StartChain(MissionGraph chain, string graphPath)
        {
            return StartChain(chain, graphPath, null, null);
        }

        /// <summary>
        /// 由父 MissionGraphRuntime 启动子图。
        /// 子图复用父图 Blackboard 的 fork，业务上仍然登记到同一个 Manager。
        /// </summary>
        internal MissionGraphRuntime StartSubChain(
            MissionGraph chain,
            string graphPath,
            GraphBlackboardRuntime parentBlackboard,
            MissionGraphRuntime parentRuntime)
        {
            return StartChain(chain, graphPath, parentBlackboard, parentRuntime);
        }

        /// <summary>
        /// 创建根运行时状态。子图状态会挂在各自父运行时的 ChildStates 里。
        /// </summary>
        public List<MissionGraphRuntimeState> CreateRuntimeStates()
        {
            return runtimes.Values
                .Where(runtime => runtime.ParentRuntime == null)
                .Select(runtime => runtime.CreateState())
                .ToList();
        }

        /// <summary>
        /// 从存档恢复所有 MissionGraphRuntime。
        /// 
        /// 恢复时不会重新 Start 图，也不会重新执行节点 Enter。
        /// 每个 runtime 只恢复 active Flow 节点和 Mission/SubGraph 等待关系，
        /// 之后 MissionChainSaver 会再把 MissionManager 里的 Mission 实例加载回来。
        /// </summary>
        public void LoadChains(IEnumerable<MissionGraphRuntimeState> states)
        {
            foreach (MissionGraphRuntime runtime in runtimes.Values.ToList())
                runtime.Stop();

            runtimes.Clear();
            if (states == null)
                return;

            foreach (MissionGraphRuntimeState state in states)
                LoadRuntimeState(state, null);
        }

        /// <summary>
        /// 根据 graphPath 找到运行中的 MissionGraph。
        /// MissionChainSaver 通过它把存档里的 MissionId 还原成对应 MissionNode 原型。
        /// </summary>
        public bool TryGetGraph(string graphPath, out MissionGraph graph)
        {
            graph = null;
            if (string.IsNullOrWhiteSpace(graphPath) ||
                !runtimes.TryGetValue(graphPath, out MissionGraphRuntime runtime))
            {
                return false;
            }

            graph = runtime.Graph;
            return graph != null;
        }

        public void OnMissionStarted(Mission<object> mission)
        {
            Debugger.Info("[MissionChainManager]Create Mission:" + mission.id);
        }

        /// <summary>
        /// MissionManager 移除任务时回调到图。
        /// 
        /// isFinished=true 表示任务自然完成，MissionNode 会输出 Completed 并推进 Sequence 连接。
        /// isFinished=false 表示取消/移除，MissionNode 会完成但不输出，从而不推进 Sequence。
        /// </summary>
        public void OnMissionRemoved(Mission<object> mission, bool isFinished)
        {
            if (mission == null ||
                !MissionRuntimeId.TryParse(mission.id, out string graphPath, out _) ||
                !runtimes.TryGetValue(graphPath, out MissionGraphRuntime runtime))
            {
                return;
            }

            if (!runtime.OnMissionCompleted(mission.id, isFinished))
                return;

            AdvanceRuntime(runtime);
            CompleteIfNeeded(runtime);
        }

        public void OnMissionStatusChanged(Mission<object> mission, bool isFinished) { }

        private MissionGraphRuntime StartChain(
            MissionGraph chain,
            string graphPath,
            GraphBlackboardRuntime parentBlackboard,
            MissionGraphRuntime parentRuntime)
        {
            if (chain == null || string.IsNullOrWhiteSpace(graphPath))
                return null;

            if (graphPath.Contains('.'))
            {
                Debugger.Warn($"[MissionChainManager] GraphPath can not contain '.': {graphPath}");
                return null;
            }

            if (runtimes.ContainsKey(graphPath))
                return runtimes[graphPath];

            var runtime = new MissionGraphRuntime(chain, this, graphPath, parentBlackboard, parentRuntime);

            // 先登记，再 Start。
            // 子图可能在 Start() 期间马上完成；提前登记可以保证完成回调能找到父子 runtime。
            runtimes[graphPath] = runtime;
            parentRuntime?.AttachChildRuntime(runtime);

            if (!runtime.Start())
            {
                runtimes.Remove(graphPath);
                parentRuntime?.DetachChildRuntime(graphPath);
                runtime.Dispose();
                return null;
            }

            // Start 只推进 Flow；Mission 部署请求由 runtime 排队，必须在 manager 侧 drain。
            AdvanceRuntime(runtime);
            CompleteIfNeeded(runtime);
            return runtime;
        }

        /// <summary>
        /// 从单个 runtime state 恢复 MissionGraphRuntime，并递归恢复子图。
        /// </summary>
        private MissionGraphRuntime LoadRuntimeState(
            MissionGraphRuntimeState state,
            MissionGraphRuntime parentRuntime)
        {
            if (state == null ||
                string.IsNullOrWhiteSpace(state.GraphPath) ||
                string.IsNullOrWhiteSpace(state.GraphResourcePath))
            {
                return null;
            }

            // 存档只信任 GraphResourcePath，不再根据固定目录和 graphName 推导资源路径。
            MissionGraph graph = ModuleSystem
                .GetModule<IResourceModule>()
                .LoadAssetOnce<MissionGraph>(state.GraphResourcePath);
            if (graph == null)
            {
                Debugger.Warn($"[MissionChainManager] Failed to load MissionGraph: {state.GraphResourcePath}");
                return null;
            }

            var runtime = new MissionGraphRuntime(
                graph,
                this,
                state.GraphPath,
                parentRuntime?.Context.Blackboard,
                parentRuntime);

            runtimes[state.GraphPath] = runtime;
            parentRuntime?.AttachChildRuntime(runtime);

            // LoadState 只恢复等待中的 active 节点，不调用 Start，也不触发节点 Enter。
            runtime.LoadState(state);

            for (int i = 0; i < state.ChildStates.Count; i++)
                LoadRuntimeState(state.ChildStates[i], runtime);

            return runtime;
        }

        /// <summary>
        /// 推动一个 runtime 前进，直到本轮没有新的 Mission 部署请求。
        /// 
        /// FlowGraphRuntime.Update 可能让节点完成并进入新节点，新节点又可能 QueueMission。
        /// 因此这里采用“Update -> DrainDeployments”的小循环，把同步可推进的部分尽量推进完。
        /// </summary>
        private void AdvanceRuntime(MissionGraphRuntime runtime)
        {
            if (runtime == null)
                return;

            for (int i = 0; i < 128; i++)
            {
                runtime.Update(0d);
                if (!DrainDeployments(runtime))
                    break;
            }
        }

        /// <summary>
        /// 把 MissionGraphRuntime 中排队的 MissionDeploymentRequest 交给 MissionManager 创建。
        /// 
        /// StartMission 失败时不能让节点一直 active，否则整张图会卡住；
        /// 这里会把节点标记为 NoOutput 完成，表示失败终止且不推进 Sequence。
        /// </summary>
        private bool DrainDeployments(MissionGraphRuntime runtime)
        {
            bool drained = false;
            while (runtime.TryDequeueDeployment(out MissionDeploymentRequest request))
            {
                drained = true;
                if (missionManager.StartMission(request.Prototype))
                {
                    runtime.MarkMissionDeployed(request);
                }
                else
                {
                    Debugger.Warn($"[MissionChainManager] Failed to start mission: {request.MissionId}");
                    runtime.MarkMissionDeploymentFailed(request);
                }
            }

            return drained;
        }

        /// <summary>
        /// 如果 runtime 已完成，则释放它，并在它是子图时通知父图对应的 SubGraphNode 完成。
        /// </summary>
        private void CompleteIfNeeded(MissionGraphRuntime runtime)
        {
            if (runtime == null || !runtime.IsCompleted)
                return;

            string graphPath = runtime.GraphPath;
            MissionGraphRuntime parent = runtime.ParentRuntime;
            runtimes.Remove(graphPath);
            parent?.DetachChildRuntime(graphPath);
            runtime.Dispose();
            Debugger.Info("[MissionChainManager]RuntimeComplete:" + graphPath);

            if (parent == null)
                return;

            if (parent.OnSubGraphCompleted(graphPath))
            {
                AdvanceRuntime(parent);
                CompleteIfNeeded(parent);
            }
        }
    }
}
