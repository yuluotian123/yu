using System.Collections.Generic;
using System.Linq;
using GameLogic.Save;

namespace GameLogic.Mission
{
    public class MissionRecord
    {
        public string MissionId { get; set; }
        public List<string> HandleStatuses { get; set; } = new();
    }

    public class ChainRecord
    {
        public string GraphName { get; set; }
        public List<string> ActiveNodeIds { get; set; } = new();
        public List<string> PendingSubGraphNames { get; set; } = new();
    }

    public class MissionChainSaver : IMissionSystemComponent<object>, ISaveable
    {
        public string SaveKey => "mission_system";

         // ── 可序列化属性（SaveModule 自动处理）────────────────────────────────
        public List<MissionRecord> Missions { get; set; } = new();
        public List<ChainRecord> Chains { get; set; } = new();
        public Dictionary<string, string> SubGraphToParent { get; set; } = new();

        public void OnMissionRemoved(Mission<object> mission, bool isFinished)
        {          
             Missions.RemoveAll(r => r.MissionId == mission.id);
        }

        public void OnMissionStarted(Mission<object> mission)
        {
            var record = new MissionRecord
            {
                MissionId = mission.id,
                HandleStatuses = mission.HandleStatus.ToList()
            };
            Missions.Add(record);
        }

        public void OnMissionStatusChanged(Mission<object> mission, bool isFinished)
        {
            var record = Missions.Find(m=>m.MissionId == mission.id) ;
            if (record != null)
                record.HandleStatuses = mission.HandleStatus.ToList();
        }
    }
}