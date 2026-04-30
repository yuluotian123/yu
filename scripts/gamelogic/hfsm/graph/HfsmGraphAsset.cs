using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic
{
    [Tool]
    [GlobalClass]
    public partial class HfsmGraphAsset : GraphAsset
    {
        public const string GraphTypeName = "HfsmGraph";

        [Export] public string InitialStateName { get; set; } = string.Empty;

        public override string GraphType
        {
            get => GraphTypeName;
            set { }
        }

        public override List<string> GetAllowedNodeTypes()
        {
            return GraphNodeFactory
                .GetNodesForGraphType(GraphTypeName)
                .Where(nodeType =>
                {
                    GraphNodeData node = GraphNodeFactory.CreateNodeData(nodeType);
                    return node is IHfsmStateNodeData || node is IHfsmPseudoNodeData;
                })
                .ToList();
        }

        public override GraphConnection CreateConnection() => new HfsmTransitionConnection();
        public override string GetEditorTitle() => "HFSM Editor";

        public IEnumerable<IHfsmStateNodeData> StateNodes => Nodes.OfType<IHfsmStateNodeData>();
        public IEnumerable<IHfsmPseudoNodeData> PseudoNodes => Nodes.OfType<IHfsmPseudoNodeData>();
        public IEnumerable<HfsmAnyStateNodeData> AnyStateNodes => Nodes.OfType<HfsmAnyStateNodeData>();

        public IHfsmStateNodeData FindStateById(string stateId)
        {
            return StateNodes.FirstOrDefault(state => state.Id == stateId);
        }

        public IHfsmPseudoNodeData FindPseudoNodeById(string nodeId)
        {
            return PseudoNodes.FirstOrDefault(node => node.Id == nodeId);
        }

        public IHfsmStateNodeData FindStateByName(string stateName)
        {
            return StateNodes.FirstOrDefault(state => state.StateName == stateName);
        }

        public IHfsmStateNodeData FindState(string stateNameOrId)
        {
            return FindStateByName(stateNameOrId) ?? FindStateById(stateNameOrId);
        }

        public IHfsmStateNodeData GetInitialState(string overrideStateName = null)
        {
            if (!string.IsNullOrWhiteSpace(overrideStateName))
            {
                IHfsmStateNodeData overrideState = FindState(overrideStateName);
                if (overrideState != null)
                    return overrideState;
            }

            if (!string.IsNullOrWhiteSpace(InitialStateName))
            {
                IHfsmStateNodeData configuredState = FindState(InitialStateName);
                if (configuredState != null)
                    return configuredState;
            }

            return StateNodes.FirstOrDefault(state => state.IsDefault) ?? StateNodes.FirstOrDefault();
        }

        public IEnumerable<HfsmTransitionConnection> GetOutgoingTransitions(string stateId)
        {
            return Connections
                .OfType<HfsmTransitionConnection>()
                .Where(connection => connection.FromNode == stateId)
                .OrderByDescending(connection => connection.Priority);
        }

        public IEnumerable<HfsmTransitionConnection> GetAnyStateTransitions(IHfsmStateNodeData currentState)
        {
            return AnyStateNodes
                .Where(node => node.CanTransitionFrom(currentState))
                .SelectMany(node => GetOutgoingTransitions(node.Id));
        }
    }
}
