using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public sealed class BehaviorTreeRuntime : IGraphRuntimeScope
{
    private readonly Dictionary<string, object> _nodeData = new(StringComparer.Ordinal);
    private readonly RandomNumberGenerator _random = new();
    private bool _localBlackboardPushed;

    public BehaviorTreeRuntime(BehaviorTreeGraphAsset graph, GraphExecutionContext context = null)
    {
        Graph = graph;
        Context = context ?? new GraphExecutionContext(graph, new GraphBlackboardRuntime());
        AddUserDataFirst(this);
        _random.Randomize();
    }

    public BehaviorTreeGraphAsset Graph { get; }
    public GraphExecutionContext Context { get; }
    public IEnumerable<IGraphRuntimeScope> ChildScopes
    {
        get { yield break; }
    }

    public bool IsRunning { get; private set; }
    public BehaviorTreeStatus LastStatus { get; private set; } = BehaviorTreeStatus.Failure;

    public event Action<BehaviorTreeRuntime, BehaviorTreeNodeData, BehaviorTreeStatus> NodeTicked;

    public bool Start()
    {
        if (Graph == null)
            return false;

        Graph.Validate(out GraphValidationResult validation);
        BehaviorTreeValidator.Append(Graph, validation);
        if (!validation.IsValid)
        {
            GD.PushWarning($"[BehaviorTreeRuntime] Graph validation failed:\n{validation.ToDisplayText()}");
            return false;
        }

        Stop();
        PushLocalBlackboardIfNeeded();
        _nodeData.Clear();
        LastStatus = BehaviorTreeStatus.Failure;
        IsRunning = true;
        return true;
    }

    public void Stop()
    {
        if (IsRunning)
            AbortSubtree(Graph?.RootNode);

        IsRunning = false;
        _nodeData.Clear();

        if (_localBlackboardPushed)
        {
            Context.Blackboard.PopLocal();
            _localBlackboardPushed = false;
        }
    }

    public BehaviorTreeStatus Update(double delta)
    {
        if (!IsRunning || Graph?.RootNode == null)
            return BehaviorTreeStatus.Failure;

        LastStatus = TickNode(Graph.RootNode, delta);
        return LastStatus;
    }

    public BehaviorTreeStatus TickNode(BehaviorTreeNodeData node, double delta)
    {
        if (node == null)
            return BehaviorTreeStatus.Failure;

        BehaviorTreeStatus status = node.Tick(this, Context, delta);
        NodeTicked?.Invoke(this, node, status);
        return status;
    }

    public List<BehaviorTreeNodeData> GetChildren(BehaviorTreeNodeData node)
    {
        return node == null ? new List<BehaviorTreeNodeData>() : Graph.GetChildren(node.Id);
    }

    public List<BehaviorTreeChildLink> GetChildLinks(BehaviorTreeNodeData node)
    {
        return node == null ? new List<BehaviorTreeChildLink>() : Graph.GetChildLinks(node.Id);
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

    public void ClearNodeData(string nodeId)
    {
        if (!string.IsNullOrWhiteSpace(nodeId))
            _nodeData.Remove(nodeId);
    }

    public void AbortSubtree(BehaviorTreeNodeData node)
    {
        AbortSubtree(node, new HashSet<string>(StringComparer.Ordinal));
    }

    public void AbortChildAt(BehaviorTreeNodeData parent, int childIndex)
    {
        List<BehaviorTreeNodeData> children = GetChildren(parent);
        if (childIndex >= 0 && childIndex < children.Count)
            AbortSubtree(children[childIndex]);
    }

    public void AbortChildrenExcept(BehaviorTreeNodeData parent, HashSet<string> excludedNodeIds)
    {
        List<BehaviorTreeNodeData> children = GetChildren(parent);
        for (int i = 0; i < children.Count; i++)
        {
            if (excludedNodeIds?.Contains(children[i].Id) == true)
                continue;

            AbortSubtree(children[i]);
        }
    }

    public float Randf() => _random.Randf();

    public bool TryGetValue<T>(string key, out T value)
    {
        return Context.Blackboard.TryGetValue(key, out value);
    }

    public T GetValue<T>(string key, T defaultValue = default)
    {
        return Context.Blackboard.GetValue(key, defaultValue);
    }

    public bool SetValue<T>(string key, T value)
    {
        return GraphRuntimeBlackboardWriter.SetValueRecursive(this, key, value);
    }

    public bool SetGlobalValue<T>(string key, T value)
    {
        return Context.Blackboard.SetGlobalValue(key, value);
    }

    private void AbortSubtree(BehaviorTreeNodeData node, HashSet<string> visited)
    {
        if (node == null || string.IsNullOrWhiteSpace(node.Id) || !visited.Add(node.Id))
            return;

        node.Abort(this, Context);
        List<BehaviorTreeNodeData> children = GetChildren(node);
        for (int i = 0; i < children.Count; i++)
            AbortSubtree(children[i], visited);
    }

    private void PushLocalBlackboardIfNeeded()
    {
        if (_localBlackboardPushed)
            return;

        Context.Blackboard.PushLocal(Graph);
        _localBlackboardPushed = true;
    }

    private void AddUserDataFirst(object value)
    {
        if (value == null)
            return;

        Context.UserData.Remove(value);
        Context.UserData.Insert(0, value);
    }
}

public static class BehaviorTreeValidator
{
    public static void Append(BehaviorTreeGraphAsset graph, GraphValidationResult result)
    {
        if (graph == null)
        {
            result.AddError("BehaviorTree graph is null.");
            return;
        }

        List<BehaviorRootNodeData> roots = graph.Nodes.OfType<BehaviorRootNodeData>().ToList();
        if (roots.Count != 1)
            result.AddError($"BehaviorTree must contain exactly one Root node. Current count: {roots.Count}.");

        ValidateParents(graph, result);
        ValidateAcyclic(graph, result);
    }

    private static void ValidateParents(BehaviorTreeGraphAsset graph, GraphValidationResult result)
    {
        var parentCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (GraphConnection connection in graph.Connections)
        {
            if (connection == null || string.IsNullOrWhiteSpace(connection.ToNode))
                continue;

            parentCounts.TryGetValue(connection.ToNode, out int count);
            parentCounts[connection.ToNode] = count + 1;
        }

        foreach (BehaviorTreeNodeData node in graph.Nodes.OfType<BehaviorTreeNodeData>())
        {
            parentCounts.TryGetValue(node.Id, out int count);
            if (node is BehaviorRootNodeData)
            {
                if (count > 0)
                    result.AddError("Root node can not have a parent.", node.Id);
                continue;
            }

            if (count > 1)
                result.AddError($"BehaviorTree node has more than one parent: {node.GetDisplayName()}.", node.Id);
        }
    }

    private static void ValidateAcyclic(BehaviorTreeGraphAsset graph, GraphValidationResult result)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (BehaviorTreeNodeData node in graph.Nodes.OfType<BehaviorTreeNodeData>())
        {
            if (HasCycle(graph, node, visiting, visited))
                result.AddError("BehaviorTree can not contain directed cycles.", node.Id);
        }
    }

    private static bool HasCycle(
        BehaviorTreeGraphAsset graph,
        BehaviorTreeNodeData node,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (node == null || visited.Contains(node.Id))
            return false;

        if (!visiting.Add(node.Id))
            return true;

        foreach (BehaviorTreeNodeData child in graph.GetChildren(node.Id))
        {
            if (HasCycle(graph, child, visiting, visited))
                return true;
        }

        visiting.Remove(node.Id);
        visited.Add(node.Id);
        return false;
    }
}
