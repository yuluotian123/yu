namespace GameLogic
{
    /// <summary>
    /// MissionGraph 中真实 MissionId 的统一生成/解析工具。
    /// 
    /// MissionId 采用 graphPath.nodeId：
    /// graphPath 用来定位运行中的 MissionGraphRuntime，nodeId 用来定位图里的 MissionNode。
    /// MissionChainManager 会禁止 graphPath 包含 '.'，这样日志、存档和解析都保持稳定。
    /// </summary>
    public static class MissionRuntimeId
    {
        /// <summary>
        /// 根据 runtime 路径和节点 Id 生成 MissionManager 使用的 MissionId。
        /// </summary>
        public static string Create(string graphPath, string nodeId)
        {
            return $"{graphPath}.{nodeId}";
        }

        /// <summary>
        /// 把 MissionId 拆回 graphPath 和 nodeId。
        /// graphPath 禁止包含 '.'，因此使用第一个 '.' 作为分隔点。
        /// </summary>
        public static bool TryParse(string missionId, out string graphPath, out string nodeId)
        {
            graphPath = string.Empty;
            nodeId = string.Empty;

            if (string.IsNullOrWhiteSpace(missionId))
                return false;

            int dot = missionId.IndexOf('.');
            if (dot <= 0 || dot >= missionId.Length - 1)
                return false;

            graphPath = missionId.Substring(0, dot);
            nodeId = missionId.Substring(dot + 1);
            return !string.IsNullOrWhiteSpace(graphPath) && !string.IsNullOrWhiteSpace(nodeId);
        }
    }
}
