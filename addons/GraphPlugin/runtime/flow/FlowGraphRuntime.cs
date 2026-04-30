using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public sealed class FlowGraphRuntime
{
    private readonly List<ActiveFlowNode> _activeNodes = new();
    private readonly Dictionary<string, object> _nodeData = new(StringComparer.Ordinal);
    private int _propagationSteps;
    private bool _localBlackboardPushed;

    public FlowGraphRuntime(FlowGraphAsset graph, GraphExecutionContext context = null)
    {
        Graph = graph;
        Context = context ?? new GraphExecutionContext(graph, new GraphBlackboardRuntime());
    }

    public FlowGraphAsset Graph { get; }
    public GraphExecutionContext Context { get; }
    public int MaxPropagationSteps { get; set; } = 256;
    public bool IsRunning { get; private set; }
    public bool IsCompleted => IsRunning && _activeNodes.Count == 0;
    public IReadOnlyList<string> ReturnLabels => _returnLabels;

    private readonly List<string> _returnLabels = new();

    public event Action<FlowGraphRuntime, GraphNodeData> NodeEntered;
    public event Action<FlowGraphRuntime, GraphNodeData> NodeExited;
    public event Action<FlowGraphRuntime, string> Returned;

    public bool Start()
    {
        if (Graph == null || Graph.primeNode == null)
            return false;

        Stop();
        IsRunning = true;
        if (!_localBlackboardPushed)
        {
            Context.Blackboard.PushLocal(Graph);
            _localBlackboardPushed = true;
        }

        _returnLabels.Clear();
        _nodeData.Clear();
        _propagationSteps = 0;
        PropagateToNode(Graph.primeNode);
        return true;
    }

    public void Stop()
    {
        if (_activeNodes.Count > 0)
        {
            for (int i = _activeNodes.Count - 1; i >= 0; i--)
                ExitActiveNode(i);
        }

        IsRunning = false;
        if (_localBlackboardPushed)
        {
            Context.Blackboard.PopLocal();
            _localBlackboardPushed = false;
        }

        _nodeData.Clear();
        _propagationSteps = 0;
    }

    public T GetNodeData<T>(string nodeId) where T : class, new()
    {
        if (!_nodeData.TryGetValue(nodeId, out object value) || value is not T typed)
        {
            typed = new T();
            _nodeData[nodeId] = typed;
        }

        return typed;
    }

    public bool TryGetNodeData<T>(string nodeId, out T data) where T : class
    {
        if (!string.IsNullOrWhiteSpace(nodeId) &&
            _nodeData.TryGetValue(nodeId, out object value) &&
            value is T typed)
        {
            data = typed;
            return true;
        }

        data = null;
        return false;
    }

    public void SetNodeData(string nodeId, object data)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return;

        if (data == null)
            _nodeData.Remove(nodeId);
        else
            _nodeData[nodeId] = data;
    }

    public void ClearNodeData(string nodeId)
    {
        if (!string.IsNullOrWhiteSpace(nodeId))
            _nodeData.Remove(nodeId);
    }

    public void Update(double delta)
    {
        if (!IsRunning)
            return;

        _propagationSteps = 0;
        for (int i = _activeNodes.Count - 1; i >= 0; i--)
        {
            ActiveFlowNode active = _activeNodes[i];
            active.Handler.Tick(this, Context, delta);
            if (!active.Handler.TryGetCompletion(this, Context, out NodeCompletion completion))
                continue;

            ExitActiveNode(i);
            HandleCompletion(active.Node, completion);
        }
    }

    public void PropagateFromOutput(string nodeId, int outputPort)
    {
        if (Graph == null)
            return;

        var connections = Graph
            .GetOutgoingConnections(nodeId, outputPort)
            .Where(connection => connection.IsAvailable)
            .ToList();

        for (int i = 0; i < connections.Count; i++)
        {
            GraphNodeData nextNode = Graph.FindNodeById(connections[i].ToNode);
            if (nextNode != null)
                PropagateToNode(nextNode);
        }
    }

    private void PropagateToNode(GraphNodeData node)
    {
        if (node == null)
            return;

        _propagationSteps++;
        if (_propagationSteps > MaxPropagationSteps)
        {
            GD.PushWarning($"[FlowGraphRuntime] Max propagation steps reached in graph: {Graph?.ResourcePath}");
            return;
        }

        NodeEntered?.Invoke(this, node);

        NodeCompletion completion;
        if (node is IFlowNode flowNode)
        {
            flowNode.Enter(this, Context);
            if (!flowNode.TryGetCompletion(this, Context, out completion))
            {
                _activeNodes.Add(new ActiveFlowNode(node, flowNode));
                return;
            }
        }
        else
        {
            node.Execute(Context);
            completion = new NodeCompletion(0);
        }

        NodeExited?.Invoke(this, node);
        HandleCompletion(node, completion);
    }

    private void HandleCompletion(GraphNodeData node, NodeCompletion completion)
    {
        if (node is FlowReturnNodeData)
        {
            _returnLabels.Add(completion.Label);
            Returned?.Invoke(this, completion.Label);
            return;
        }

        PropagateFromOutput(node.Id, completion.OutputPort);
    }

    private void ExitActiveNode(int index)
    {
        ActiveFlowNode active = _activeNodes[index];
        active.Handler.Exit(this, Context);
        _activeNodes.RemoveAt(index);
        NodeExited?.Invoke(this, active.Node);
    }

    private readonly struct ActiveFlowNode
    {
        public ActiveFlowNode(GraphNodeData node, IFlowNode handler)
        {
            Node = node;
            Handler = handler;
        }

        public GraphNodeData Node { get; }
        public IFlowNode Handler { get; }
    }
}
