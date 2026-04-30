using System.Collections.Generic;
using System.Linq;
using Godot;

[Tool]
[GlobalClass]
public partial class StateGraphAsset : GraphAsset
{
    public const string GraphTypeName = "StateGraph";

    [Export] public string InitialStateName { get; set; } = string.Empty;

    public override string GraphType
    {
        get => GraphTypeName;
        set { }
    }

    public override List<string> GetAllowedNodeTypes()
    {
        return GraphNodeFactory
            .GetNodesForGraphType(GraphType)
            .Where(nodeType =>
            {
                GraphNodeData node = GraphNodeFactory.CreateNodeData(nodeType);
                return IsAllowedStateGraphNode(node);
            })
            .ToList();
    }

    public override GraphConnection CreateConnection() => new StateTransitionConnection();
    public override string GetEditorTitle() => "StateGraph Editor";

    protected virtual bool IsAllowedStateGraphNode(GraphNodeData node)
    {
        return node is IStateNodeData || node is IStatePseudoNodeData;
    }

    public IEnumerable<IStateNodeData> StateNodes => Nodes.OfType<IStateNodeData>();
    public IEnumerable<IStatePseudoNodeData> PseudoNodes => Nodes.OfType<IStatePseudoNodeData>();
    public IEnumerable<AnyStateNodeData> AnyStateNodes => Nodes.OfType<AnyStateNodeData>();

    public IStateNodeData FindStateById(string stateId)
    {
        return StateNodes.FirstOrDefault(state => state.Id == stateId);
    }

    public IStatePseudoNodeData FindPseudoNodeById(string nodeId)
    {
        return PseudoNodes.FirstOrDefault(node => node.Id == nodeId);
    }

    public IStateNodeData FindStateByName(string stateName)
    {
        return StateNodes.FirstOrDefault(state => state.StateName == stateName);
    }

    public IStateNodeData FindState(string stateNameOrId)
    {
        return FindStateByName(stateNameOrId) ?? FindStateById(stateNameOrId);
    }

    public IStateNodeData GetInitialState(string overrideStateName = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideStateName))
        {
            IStateNodeData overrideState = FindState(overrideStateName);
            if (overrideState != null)
                return overrideState;
        }

        if (!string.IsNullOrWhiteSpace(InitialStateName))
        {
            IStateNodeData configuredState = FindState(InitialStateName);
            if (configuredState != null)
                return configuredState;
        }

        return StateNodes.FirstOrDefault(state => state.IsDefault) ?? StateNodes.FirstOrDefault();
    }

    public IEnumerable<StateTransitionConnection> GetOutgoingTransitions(string stateId, int? fromPort = null)
    {
        return GetOutgoingConnections(stateId, fromPort)
            .OfType<StateTransitionConnection>()
            .OrderByDescending(connection => connection.Priority);
    }

    public IEnumerable<StateTransitionConnection> GetAnyStateTransitions(IStateNodeData currentState)
    {
        return AnyStateNodes
            .Where(node => node.CanTransitionFrom(currentState))
            .SelectMany(node => GetOutgoingTransitions(node.Id));
    }
}
