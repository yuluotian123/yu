using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic.Mission
{
    public class MissionChainHandle
    {
        private readonly MissionGraph chain;
        private readonly MissionChainManager manager;

        private readonly Dictionary<string, MissionNode> activeNodes = new Dictionary<string, MissionNode>();
        private readonly Queue<MissionNode> buffer = new Queue<MissionNode>();

        // 等待子图完成的节点：subGraphGraphName → SubGraphNodeData
        private readonly Dictionary<string, SubGraphNodeData> pendingSubGraphs = new Dictionary<string, SubGraphNodeData>();

        public bool IsCompleted => activeNodes.Count == 0 && pendingSubGraphs.Count == 0;

        public MissionChainHandle(MissionGraph chain, MissionChainManager manager)
        {
            this.chain = chain;
            this.manager = manager;
        }

         /// <summary>
        /// 从 primeNode 开始执行节点遍历。
        /// 必须在 handle 已注册到 MissionChainManager.handles 之后再调用，
        /// 以确保子图立即完成时回调能正确找到父图。
        /// </summary>
        public void Initialize()
        {
            if (chain.primeNode != null)
                ExecuteNode(chain.primeNode);
        }

        public void FlushBuffer(Action<MissionPrototype<object>> deployer)
        {
            if (buffer.Count == 0) return;
            while (buffer.Count > 0)
            {
                var node = buffer.Dequeue();
                var missionProto = node.GetMissionProto();
                activeNodes.Add(missionProto.id, node);
                deployer(missionProto);
            }
        }

        public void OnMissionComplete(string missionId, bool continues)
        {
            if (!activeNodes.Remove(missionId, out var node)) return;

            /* execute all available output connections */
            if (continues)
            {
                foreach (var outConnection in chain.GetOutgoingConnections(node.Id).Where
                   (c => ((ConnectionWithConditon)c).IsAvailable
                      && ((ConnectionWithConditon)c).IsSequence))
                    ExecuteNode(chain.FindNodeById(outConnection.ToNode));
            }
        }

        /// <summary>子图完成后回调，继续执行子图节点的后续连接</summary>
        public void OnSubGraphComplete(string subGraphGraphName)
        {
            if (!pendingSubGraphs.Remove(subGraphGraphName, out var subNode)) return;

            foreach (var outConnection in chain.GetOutgoingConnections(subNode.Id).Where
               (c => ((ConnectionWithConditon)c).IsAvailable
                  && ((ConnectionWithConditon)c).IsSequence))
                ExecuteNode(chain.FindNodeById(outConnection.ToNode));


        }

        /// <summary>execute given node</summary>
        public void ExecuteNode(GraphNodeData node)
        {
            if (node is null) return;

            switch (node)
            {
                /* execute mission node, add output prototype to buffer queue */
                case MissionNode missionNode:
                    if (activeNodes.ContainsKey(missionNode.GraphName + "." + missionNode.Id)) return;
                    buffer.Enqueue(missionNode);
                    break;

                /* enter sub graph: register pending and start sub chain via manager */
                case SubGraphNodeData subGraphNode:
                    var subGraph = subGraphNode.GetSubGraph() as MissionGraph;
                    if (subGraph == null)
                    {
                        GD.PushWarning($"[MissionChain] 子图未绑定或不是 MissionGraph: {subGraphNode.SubGraphPath}");
                        break;
                    }
                    if (pendingSubGraphs.ContainsKey(subGraph.graphName)) break;
                    pendingSubGraphs.Add(subGraph.graphName, subGraphNode);
                    manager.RegisterSubGraph(subGraph.graphName, chain.graphName);
                    manager.StartChain(subGraph);
                    break;

                default:
                    node.Execute();
                    foreach (var outConnection in chain.GetOutgoingConnections(node.Id).Where
                       (c => ((ConnectionWithConditon)c).IsAvailable
                          && ((ConnectionWithConditon)c).IsSequence))
                        ExecuteNode(chain.FindNodeById(outConnection.ToNode));
                    break;
            }

            foreach (var outConnection in chain.GetOutgoingConnections(node.Id).Where
                   (c => ((ConnectionWithConditon)c).IsAvailable
                      && ((ConnectionWithConditon)c).IsParallel))
                ExecuteNode(chain.FindNodeById(outConnection.ToNode));
        }
    }
}
