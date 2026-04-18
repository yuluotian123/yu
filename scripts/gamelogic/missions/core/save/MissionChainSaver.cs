using System.Collections.Generic;
using System.Linq;
using Framework;

namespace GameLogic
{
    public class MissionRecord
    {
        public string MissionId { get; set; }
        public List<string> HandleStatuses { get; set; } = new();
    }

    public class ChainRecord
    {
        public string GraphPath { get; set; }
        public List<string> ActiveNodeIds { get; set; } = new();
        public List<string> PendingSubGraphPaths { get; set; } = new();
    }

    public class MissionChainSaver : IMissionSystemComponent<object>, ISaveable
    {
        public string SaveKey => "mission_system";

        public string MissionPathRoot = "res://assets/config/graphs/mission_graphs/";

        // ── 可序列化属性（SaveModule 自动处理）────────────────────────────────
        public List<MissionRecord> Missions { get; set; } = new();
        public List<ChainRecord> Chains { get; set; } = new();


        private readonly MissionManager<object> _missionManager;
        private readonly MissionChainManager _chainManager;


        public MissionChainSaver(MissionManager<object> missionManager)
        {
            _missionManager = missionManager;
            _chainManager = missionManager.GetMissionSystemComponent<MissionChainManager>();
        }


        public void OnMissionRemoved(Mission<object> mission, bool isFinished)
        {
            Missions.RemoveAll(r => r.MissionId == mission.id);

            var graphPath = mission.id.Split('.')[0];
            if (string.IsNullOrEmpty(graphPath)) return;


            var chainRecord = Chains.Find(t => t.GraphPath == graphPath);
            if (chainRecord == null) return;

            chainRecord.ActiveNodeIds.Remove(mission.id);
            CleanupChainIfEmpty(graphPath);  // ← 统一走这个方法
        }
        private void CleanupChainIfEmpty(string graphPath)
        {
            var record = Chains.Find(t => t.GraphPath == graphPath);
            if (record == null) return;
            if (record.ActiveNodeIds.Count > 0 || record.PendingSubGraphPaths.Count > 0) return;

            Chains.Remove(record);

            // 递归检查父图
            var lastSlash = graphPath.LastIndexOf('/');
            if (lastSlash > 0)
            {
                var parentPath = graphPath.Substring(0, lastSlash);
                var parentRecord = Chains.Find(t => t.GraphPath == parentPath);
                if (parentRecord != null)
                {
                    parentRecord.PendingSubGraphPaths.Remove(graphPath);
                    CleanupChainIfEmpty(parentPath);  // ← 递归
                }
            }
        }

        public void OnMissionStarted(Mission<object> mission)
        {
            var record = new MissionRecord
            {
                MissionId = mission.id,
                HandleStatuses = mission.HandleStatus.ToList()
            };
            Missions.Add(record);


            var graphPath = mission.id.Split('.')[0];
            if (string.IsNullOrEmpty(graphPath)) return;
            var chainRecord = Chains.Find(t => t.GraphPath == graphPath);
            if (chainRecord == null)
            {
                chainRecord = new ChainRecord { GraphPath = graphPath };
                Chains.Add(chainRecord);
            }

            var lastSlash = graphPath.LastIndexOf('/');
            if (lastSlash > 0)
            {
                var parentPath = graphPath.Substring(0, lastSlash);
                var parentRecord = Chains.Find(t => t.GraphPath == parentPath);
                if (parentRecord == null)
                {
                    parentRecord = new ChainRecord { GraphPath = parentPath };
                    Chains.Add(parentRecord);
                }

                if (!parentRecord.PendingSubGraphPaths.Contains(graphPath))
                    parentRecord.PendingSubGraphPaths.Add(graphPath);
            }

            if (!chainRecord.ActiveNodeIds.Contains(mission.id))
                chainRecord.ActiveNodeIds.Add(mission.id);
        }

        public void OnMissionStatusChanged(Mission<object> mission, bool isFinished)
        {
            var record = Missions.Find(m => m.MissionId == mission.id);
            if (record != null)
                record.HandleStatuses = mission.HandleStatus.ToList();
        }

        public void Load()
        {
            Debugger.Info("Load MissionSystem. ChainsCount:" + Chains.Count + " MissionsCount:" + Missions.Count);
            // 1. 先加载资源，构建 graphResources 字典
            var graphResources = new Dictionary<string, MissionGraph>();
            foreach (var chain in Chains)
            {
                var handle = ModuleSystem.GetModule<IResourceModule>().LoadAsset<MissionGraph>(GraphPathToResourcePath(chain.GraphPath));
                var graph = handle.Asset;
                handle.Release();

                if (graph != null)
                    graphResources[chain.GraphPath] = graph;
            }

            // 2. 恢复 chain handles（LoadMissionChainManager 签名不变）
            var chainStates = Chains.ToDictionary(
                c => c.GraphPath,
                c => (c.ActiveNodeIds, c.PendingSubGraphPaths));
            _chainManager.LoadChain(chainStates, graphResources);

            // 3. 恢复 missions（从同一批图资源重建 proto，不重复 Load）
            foreach (var record in Missions)
            {
                var dot = record.MissionId.LastIndexOf('.');
                if (dot < 0) continue;
                var graphPath = record.MissionId.Substring(0, dot);
                var nodeId = record.MissionId.Substring(dot + 1);

                if (!graphResources.TryGetValue(graphPath, out var graph)) continue;
                if (graph.FindNodeById(nodeId) is not MissionNode mNode) continue;

                var proto = mNode.CreateMissionProto(graphPath);
                _missionManager.LoadMission(proto, record.HandleStatuses.ToArray());
            }
        }

        private string GraphPathToResourcePath(string graphPath)
        {
            // 取最后一段（子图 path 只有最末段是真正的图名）
            var lastSlash = graphPath.LastIndexOf('/');
            var graphName = lastSlash >= 0 ? graphPath.Substring(lastSlash + 1) : graphPath;
            Debugger.Info(MissionPathRoot + graphName + ".tres");
            return MissionPathRoot + graphName + ".tres";
        }
    }
}
