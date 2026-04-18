using System.Collections.Generic;
using Framework;
using Godot;

namespace GameLogic
{
    public class MissionChainManager : IMissionSystemComponent<object>
    {
        private readonly MissionManager<object> missionManager;
        private readonly Dictionary<string, MissionChainHandle> handles = new Dictionary<string, MissionChainHandle>();

        public MissionChainManager(MissionManager<object> missionManager)
        {
            this.missionManager = missionManager;
        }

        /// <summary>启动根图，graphPath 默认为 graphName</summary>
        public void StartChain(MissionGraph chain) => StartChain(chain, chain.graphName);

        /// <summary>启动图，指定完整路径（子图调用时传入）</summary>
        public void StartChain(MissionGraph chain, string graphPath)
        {
            if (chain == null || handles.ContainsKey(graphPath)) return;
            var handle = new MissionChainHandle(chain, this, graphPath);
            handles.Add(graphPath, handle);
            handle.Start();
            handle.FlushBuffer(t => missionManager.StartMission(t));
            if (handle.IsCompleted)
            {
                Debugger.Info("[MissionChainManager]HandleComplete:" + graphPath);
                handles.Remove(graphPath);
                NotifyParentChainCompleted(graphPath);
            }
        }

        public void LoadChain(Dictionary<string, (List<string> activeIds, List<string> pendingPaths)> chainStates,Dictionary<string, MissionGraph> graphResources)
        {
            handles.Clear();
            foreach (var kv in chainStates)
            {
                var graphPath = kv.Key;
                if (!graphResources.TryGetValue(graphPath, out var graph)) continue;

                var handle = new MissionChainHandle(graph, this, graphPath);
                handles[graphPath] = handle;
                handle.Load(kv.Value.activeIds, kv.Value.pendingPaths);
            }
        }


        public void OnMissionStarted(Mission<object> mission)
        {
            Debugger.Info("[MissionChainManager]Create Mission:" + mission.id);
        }

        public void OnMissionRemoved(Mission<object> mission, bool isFinished)
        {
            // 用 Split('.')[0] 得到该任务所属图的 graphPath
            var graphpath = mission.id.Split('.')[0];
            if (!handles.TryGetValue(graphpath, out var handle)) return;

            // Notify the handle that the mission is completed
            handle.OnMissionComplete(mission.id, isFinished);
            handle.FlushBuffer(t => missionManager.StartMission(t));

            // 如果 handle 完成了，检查是否是某个父图的子图
            if (handle.IsCompleted)
            {
                Debugger.Info("[MissionChainManager]HandleComplete:" + graphpath);
                handles.Remove(graphpath);
                NotifyParentChainCompleted(graphpath);
            }
        }

        public void OnMissionStatusChanged(Mission<object> mission, bool isFinished) { }

        private void NotifyParentChainCompleted(string completedGraphPath)
        {
            var lastSlash = completedGraphPath.LastIndexOf('/');
            if (lastSlash <= 0) return; // 根图，没有父图

            var parentGraphPath = completedGraphPath.Substring(0, lastSlash);
            if (!handles.TryGetValue(parentGraphPath, out var parentHandle)) return;

            parentHandle.OnSubGraphComplete(completedGraphPath);
            parentHandle.FlushBuffer(t => missionManager.StartMission(t));

            if (parentHandle.IsCompleted)
            {
                Debugger.Info("[MissionChainManager]HandleComplete:" + parentGraphPath);
                handles.Remove(parentGraphPath);
                NotifyParentChainCompleted(parentGraphPath);
            }
        }
    }
}
