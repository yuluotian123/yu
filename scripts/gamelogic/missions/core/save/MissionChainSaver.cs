using System.Collections.Generic;
using System.Linq;
using Framework;

namespace GameLogic
{
    /// <summary>
    /// 单个 MissionManager 任务的存档记录。
    /// MissionId 能反查 graphPath 和 MissionNode.Id，HandleStatuses 保存具体需求进度。
    /// </summary>
    public class MissionRecord
    {
        public string MissionId { get; set; }
        public List<string> HandleStatuses { get; set; } = new();
    }

    /// <summary>
    /// Mission 系统存档入口。
    /// 
    /// 存档拆成两部分：
    /// 1. Missions：MissionManager 中真实任务的需求进度。
    /// 2. Chains：MissionGraphRuntime 树的 active 节点和子图等待关系。
    /// 加载时必须先恢复 Chains，再恢复 Missions，因为 MissionId 需要通过 graphPath 找回 MissionGraph。
    /// </summary>
    public class MissionChainSaver : IMissionSystemComponent<object>, ISaveable
    {
        private readonly MissionManager<object> _missionManager;
        private readonly MissionChainManager _chainManager;

        /// <summary>
        /// LoadMission 会触发 OnMissionStarted。
        /// 加载阶段不应该把存档中的记录重复追加一次，所以用该标记屏蔽回调写入。
        /// </summary>
        private bool _loading;

        public MissionChainSaver(MissionManager<object> missionManager)
        {
            _missionManager = missionManager;
            _chainManager = missionManager.GetMissionSystemComponent<MissionChainManager>();
        }

        public string SaveKey => "mission_system";

        /// <summary>
        /// MissionManager 侧的真实任务状态。
        /// </summary>
        public List<MissionRecord> Missions { get; set; } = new();

        /// <summary>
        /// MissionGraphRuntime 侧的图运行时状态。
        /// </summary>
        public List<MissionGraphRuntimeState> Chains { get; set; } = new();

        /// <summary>
        /// 保存图 runtime 树。
        /// Mission 列表通过 MissionManager 回调持续维护，这里只刷新 Chains。
        /// </summary>
        public void Save()
        {
            Chains = _chainManager?.CreateRuntimeStates() ?? new List<MissionGraphRuntimeState>();
        }

        /// <summary>
        /// 恢复 Mission 系统。
        /// 先恢复 runtime，之后再用 MissionRecord 重建 MissionManager 中的任务实例。
        /// </summary>
        public void Load()
        {
            Debugger.Info("Load MissionSystem. ChainsCount:" + Chains.Count + " MissionsCount:" + Missions.Count);

            // 读档是一次状态替换。先静默清空旧任务，避免旧任务继续存在，
            // 也避免 RemoveMission 回调把即将恢复的 runtime 当成“任务取消”处理。
            _missionManager.ClearMissions();
            _chainManager?.LoadChains(Chains);

            _loading = true;
            try
            {
                foreach (MissionRecord record in Missions.ToList())
                {
                    if (!MissionRuntimeId.TryParse(record.MissionId, out string graphPath, out string nodeId))
                        continue;

                    if (_chainManager?.TryGetGraph(graphPath, out MissionGraph graph) != true)
                        continue;

                    if (graph.FindNodeById(nodeId) is not MissionNode missionNode)
                        continue;

                    // Mission 原型来自当前图资源中的 MissionNode，需求进度来自存档里的 HandleStatuses。
                    MissionPrototype<object> proto = missionNode.CreateMissionProto(graphPath);
                    if (!_missionManager.LoadMission(proto, record.HandleStatuses.ToArray()))
                        Debugger.Warn($"[MissionChainSaver] Failed to load mission: {record.MissionId}");
                }
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>
        /// MissionManager 创建任务后，写入或刷新 MissionRecord。
        /// 加载阶段由 _loading 屏蔽，避免重复写入同一条存档记录。
        /// </summary>
        public void OnMissionStarted(Mission<object> mission)
        {
            if (_loading || mission == null)
                return;

            Missions.RemoveAll(record => record.MissionId == mission.id);
            Missions.Add(new MissionRecord
            {
                MissionId = mission.id,
                HandleStatuses = mission.HandleStatus.ToList()
            });
        }

        /// <summary>
        /// 任务被移除后，从存档记录里删除。
        /// 图是否继续推进由 MissionChainManager.OnMissionRemoved 处理。
        /// </summary>
        public void OnMissionRemoved(Mission<object> mission, bool isFinished)
        {
            if (mission != null)
                Missions.RemoveAll(record => record.MissionId == mission.id);
        }

        /// <summary>
        /// 任务需求状态变化时，刷新已保存的 handle 状态。
        /// </summary>
        public void OnMissionStatusChanged(Mission<object> mission, bool isFinished)
        {
            if (mission == null)
                return;

            MissionRecord record = Missions.Find(value => value.MissionId == mission.id);
            if (record != null)
                record.HandleStatuses = mission.HandleStatus.ToList();
        }
    }
}
