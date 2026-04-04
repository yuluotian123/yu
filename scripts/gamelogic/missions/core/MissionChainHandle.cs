using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic.Mission
{
    public class MissionChainHandle
    {
        private readonly MissionGraph chain;

        private readonly Dictionary<string, MissionNode> activeNodes = new Dictionary<string, MissionNode>();
        private readonly Queue<MissionNode> buffer = new Queue<MissionNode>();

        public bool IsCompleted => activeNodes.Count == 0;

        public MissionChainHandle(MissionGraph chain)
        {
            this.chain = chain;

            /* execute prime node */
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
                var nodeId = missionId[(missionId.IndexOf('.') + 1)..];

                foreach (var outConnection in chain.GetOutgoingConnections(node.Id).Where
                   (c => ((ConnectionWithConditon)c).IsAvailable
                      && ((ConnectionWithConditon)c).IsSequence))
                    ExecuteNode(chain.FindNodeById(outConnection.ToNode));
            }
        }

        /// <summary>execute given node</summary>
        public void ExecuteNode(GraphNodeData node)
        {
            if (node is null) return;


            switch (node)
            {

                /* execute mission node, add output prototype to buffer queue */
                case MissionNode missionNode:
                    if (activeNodes.ContainsKey(missionNode.Id)) return;
                    buffer.Enqueue(missionNode);
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
