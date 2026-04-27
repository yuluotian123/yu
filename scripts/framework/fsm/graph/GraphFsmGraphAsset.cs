using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Framework
{
    [Tool]
    [GlobalClass]
    public partial class GraphFsmGraphAsset : GraphAsset
    {
        public const string GraphTypeName = "GraphFsm";

        [Export] public string InitialStateName { get; set; } = string.Empty;

        public override string GraphType
        {
            get => GraphTypeName;
            set { }
        }

        public override List<string> GetAllowedNodeTypes() => new() { nameof(GraphFsmStateNodeData) };
        public override GraphConnection CreateConnection() => new GraphFsmTransitionConnection();
        public override string GetEditorTitle() => "Graph FSM Editor";

        public IEnumerable<GraphFsmStateNodeData> StateNodes => Nodes.OfType<GraphFsmStateNodeData>();

        public GraphFsmStateNodeData FindStateById(string stateId)
        {
            return StateNodes.FirstOrDefault(state => state.Id == stateId);
        }

        public GraphFsmStateNodeData FindStateByName(string stateName)
        {
            return StateNodes.FirstOrDefault(state => state.StateName == stateName);
        }

        public GraphFsmStateNodeData GetInitialState(string overrideStateName = null)
        {
            if (!string.IsNullOrWhiteSpace(overrideStateName))
            {
                GraphFsmStateNodeData overrideState = FindStateByName(overrideStateName) ?? FindStateById(overrideStateName);
                if (overrideState != null)
                    return overrideState;
            }

            if (!string.IsNullOrWhiteSpace(InitialStateName))
            {
                GraphFsmStateNodeData configuredState = FindStateByName(InitialStateName) ?? FindStateById(InitialStateName);
                if (configuredState != null)
                    return configuredState;
            }

            return StateNodes.FirstOrDefault(state => state.IsDefault) ?? StateNodes.FirstOrDefault();
        }

        public IEnumerable<GraphFsmTransitionConnection> GetOutgoingTransitions(string stateId)
        {
            return Connections
                .OfType<GraphFsmTransitionConnection>()
                .Where(connection => connection.FromNode == stateId)
                .OrderByDescending(connection => connection.Priority);
        }
    }
}
