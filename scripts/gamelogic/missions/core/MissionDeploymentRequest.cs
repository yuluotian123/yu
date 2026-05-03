namespace GameLogic
{
    /// <summary>
    /// MissionGraphRuntime 向 MissionChainManager 提交的部署请求。
    /// 
    /// Runtime 在节点 Enter 阶段只创建这个请求；
    /// Manager drain 后才会真正调用 MissionManager.StartMission。
    /// </summary>
    public sealed class MissionDeploymentRequest
    {
        /// <summary>
        /// graphPath.nodeId 格式的真实 MissionId。
        /// </summary>
        public string MissionId { get; set; } = string.Empty;

        /// <summary>
        /// 发起请求的 MissionNode.Id，用于部署成功后建立等待关系，失败后写回节点完成状态。
        /// </summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>
        /// MissionManager 创建任务所需的原型。
        /// </summary>
        public MissionPrototype<object> Prototype { get; set; }
    }
}
