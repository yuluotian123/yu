using System.Collections.Generic;
using Godot;

namespace GameLogic.Mission
{
    public class MissionChainManager : IMissionSystemComponent<object>
    {
        private readonly MissionManager<object> missionManager;
        private readonly Dictionary<string, MissionChainHandle> handles = new Dictionary<string, MissionChainHandle>();

        // 子图 graphName → 父图 graphName 的映射
        private readonly Dictionary<string, string> subGraphToParent = new Dictionary<string, string>();

        public MissionChainManager(MissionManager<object> missionManager)
        {
            this.missionManager = missionManager;
        }

        public void StartChain(MissionGraph chain)
        {
            if (chain == null || handles.ContainsKey(chain.graphName)) return;
            var handle = new MissionChainHandle(chain, this);
            handles.Add(chain.graphName, handle);          // 先加入字典
            handle.Initialize();                         // 再执行节点遍历
            handle.FlushBuffer(t => missionManager.StartMission(t));
            if (handle.IsCompleted)
            {
                handles.Remove(chain.graphName);
                NotifyParentChainCompleted(chain.graphName);
            }
        }

        /// <summary>由 MissionChainHandle 调用，注册子图与父图的关系</summary>
        internal void RegisterSubGraph(string subGraphName, string parentGraphName)
        {
            subGraphToParent[subGraphName] = parentGraphName;
        }

        public void OnMissionStarted(Mission<object> mission) { }

        public void OnMissionRemoved(Mission<object> mission, bool isFinished)
        {
            // 用 Split('.')[0] 得到该任务所属图的 graphName
            var graphName = mission.id.Split('.')[0];
            if (!handles.TryGetValue(graphName, out var handle)) return;

            // Notify the handle that the mission is completed
            handle.OnMissionComplete(mission.id, isFinished);
            handle.FlushBuffer(t => missionManager.StartMission(t));

            // 如果 handle 完成了，检查是否是某个父图的子图
            if (handle.IsCompleted)
            {
                GD.Print(graphName);
                handles.Remove(graphName);
                NotifyParentChainCompleted(graphName);
            }
        }

        public void OnMissionStatusChanged(Mission<object> mission, bool isFinished) { }

        /// <summary>子图完成后通知父图继续</summary>
        private void NotifyParentChainCompleted(string completedGraphName)
        {
            if (!subGraphToParent.Remove(completedGraphName, out var parentGraphName)) return;
            if (!handles.TryGetValue(parentGraphName, out var parentHandle)) return;

            parentHandle.OnSubGraphComplete(completedGraphName);
            parentHandle.FlushBuffer(t => missionManager.StartMission(t));

            if (parentHandle.IsCompleted)
            {
                handles.Remove(parentGraphName);
                NotifyParentChainCompleted(parentGraphName);
            }
        }
    }
}
