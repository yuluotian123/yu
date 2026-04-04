using System.Collections.Generic;

namespace GameLogic.Mission
{
    public class MissionChainManager : IMissionSystemComponent<object>
    {
        private readonly MissionManager<object> missionManager;
        private readonly Dictionary<string, MissionChainHandle> handles = new Dictionary<string, MissionChainHandle>();
        
        public MissionChainManager(MissionManager<object> missionManager)
        {
            this.missionManager = missionManager;
        }

        public void StartChain(MissionGraph chain)
        {
            if (chain == null || handles.ContainsKey(chain.graphName)) return;
            var handle = new MissionChainHandle(chain);
            handle.FlushBuffer(t => missionManager.StartMission(t));
            if (!handle.IsCompleted)
                handles.Add(chain.graphName, handle);
        }

        public void OnMissionStarted(Mission<object> mission) { }

        public void OnMissionRemoved(Mission<object> mission, bool isFinished)
        {
            // Get the mission chain handle
            var missionChainId = mission.id.Split('.')[0];
            if (!handles.TryGetValue(missionChainId, out var handle)) return;
            
            // Notify the handle that the mission is completed
            handle.OnMissionComplete(mission.id, isFinished);
            handle.FlushBuffer(t => missionManager.StartMission(t));
            
            // Remove the handle if the mission is finished
            if (handle.IsCompleted) handles.Remove(missionChainId);
        }

        public void OnMissionStatusChanged(Mission<object> mission, bool isFinished) { }
    }
}
