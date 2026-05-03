using System.Collections.Generic;

namespace GameLogic
{
    /// <summary>
    /// MissionGraphRuntime 的可保存状态。
    /// 
    /// 这里只保存“图 runtime 正在等待什么”，不保存 Mission 需求进度本身。
    /// 真实 Mission 的 handle 状态由 MissionChainSaver.Missions 保存。
    /// </summary>
    public class MissionGraphRuntimeState
    {
        /// <summary>
        /// runtime 在整棵 MissionGraph 树中的路径。
        /// 根图通常是 graphName，子图是 parentGraphPath + "/" + subGraphName。
        /// </summary>
        public string GraphPath { get; set; } = string.Empty;

        /// <summary>
        /// MissionGraph 资源路径。
        /// 读取存档时只用这个路径加载图资源，不再从固定目录推导。
        /// </summary>
        public string GraphResourcePath { get; set; } = string.Empty;

        /// <summary>
        /// 正在等待 MissionManager 回调的 MissionId 列表。
        /// 恢复时会还原为 active MissionNode，但不会再次调用 MissionNode.Enter。
        /// </summary>
        public List<string> ActiveMissionIds { get; set; } = new();

        /// <summary>
        /// 正在等待完成的子图路径。
        /// 恢复时会根据路径反查对应 MissionSubGraphNodeData。
        /// </summary>
        public List<string> PendingSubGraphPaths { get; set; } = new();

        /// <summary>
        /// 子 MissionGraphRuntime 状态树。
        /// </summary>
        public List<MissionGraphRuntimeState> ChildStates { get; set; } = new();
    }
}
