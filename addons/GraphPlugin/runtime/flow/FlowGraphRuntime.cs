using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// FlowGraph 的通用运行时。
/// 
/// 核心语义：
/// - 节点进入时推进 Parallel 连接。
/// - 节点完成时根据 NodeCompletion.OutputPort 推进 Sequence 连接。
/// - 实现 IFlowNode 的节点可以保持 active，直到 TryGetCompletion 返回完成结果。
/// 
/// 业务图类型可以继承它，通过 protected hooks 追加调试、筛选连接或保存恢复逻辑。
/// </summary>
public class FlowGraphRuntime : IGraphRuntimeScope
{
    /// <summary>
    /// 当前还没有完成的 IFlowNode 节点。
    /// </summary>
    private readonly List<ActiveFlowNode> _activeNodes = new();

    /// <summary>
    /// 节点运行时临时数据。
    /// key 通常是 nodeId，value 由节点自己决定。
    /// </summary>
    private readonly Dictionary<string, object> _nodeData = new(StringComparer.Ordinal);

    /// <summary>
    /// 单次传播中的步数计数，防止图内同步循环导致无限递归。
    /// </summary>
    private int _propagationSteps;

    /// <summary>
    /// 当前 runtime 是否已经向 Blackboard 压入本图的 local scope。
    /// </summary>
    private bool _localBlackboardPushed;

    /// <summary>
    /// 创建 FlowGraphRuntime。
    /// 如果没有传入 context，会自动创建一份独立 Blackboard。
    /// </summary>
    public FlowGraphRuntime(FlowGraphAsset graph, GraphExecutionContext context = null)
    {
        Graph = graph;
        Context = context ?? new GraphExecutionContext(graph, new GraphBlackboardRuntime());
    }

    public FlowGraphAsset Graph { get; }
    public GraphExecutionContext Context { get; }

    /// <summary>
    /// FlowGraph 当前没有内建子运行时，保留空集合以接入统一运行时作用域协议。
    /// </summary>
    public virtual IEnumerable<IGraphRuntimeScope> ChildScopes
    {
        get { yield break; }
    }

    /// <summary>
    /// 单次同步传播允许的最大步数。
    /// 主要用于防止 Enter/Complete 立即互相触发形成死循环。
    /// </summary>
    public int MaxPropagationSteps { get; set; } = 256;
    public bool ManageLocalBlackboardScope { get; set; } = true;

    public bool IsRunning { get; protected set; }

    /// <summary>
    /// 默认完成条件：runtime 正在运行且没有 active 节点。
    /// 子类可以扩展额外业务等待条件。
    /// </summary>
    public virtual bool IsCompleted => IsRunning && _activeNodes.Count == 0;

    public IReadOnlyList<string> ReturnLabels => _returnLabels;

    /// <summary>
    /// 当前 active 节点 Id 列表，主要供保存恢复使用。
    /// </summary>
    public IReadOnlyList<string> ActiveNodeIds
    {
        get
        {
            var ids = new List<string>();
            for (int i = 0; i < _activeNodes.Count; i++)
            {
                string id = _activeNodes[i].Node?.Id;
                if (!string.IsNullOrWhiteSpace(id))
                    ids.Add(id);
            }

            return ids;
        }
    }

    private readonly List<string> _returnLabels = new();

    public event Action<FlowGraphRuntime, GraphNodeData> NodeEntered;
    public event Action<FlowGraphRuntime, GraphNodeData> NodeExited;
    public event Action<FlowGraphRuntime, string> Returned;

    /// <summary>
    /// 子类可读取 active 节点数量，但不能直接修改 active 列表。
    /// </summary>
    protected int ActiveNodeCount => _activeNodes.Count;

    /// <summary>
    /// 验证并启动图，从 PrimeNode 开始同步传播。
    /// </summary>
    public virtual bool Start()
    {
        if (Graph == null || Graph.PrimeNode == null)
            return false;

        if (!Graph.Validate(out GraphValidationResult validation))
        {
            GD.PushWarning($"[FlowGraphRuntime] 图验证失败，无法启动：\n{validation.ToDisplayText()}");
            return false;
        }

        Stop();
        IsRunning = true;
        PushLocalBlackboardIfNeeded();

        _returnLabels.Clear();
        _nodeData.Clear();
        _propagationSteps = 0;
        PropagateToNode(Graph.PrimeNode);
        return true;
    }

    public virtual bool StartFromNode(GraphNodeData node)
    {
        if (Graph == null || node == null)
            return false;

        Stop();
        IsRunning = true;
        PushLocalBlackboardIfNeeded();
        _returnLabels.Clear();
        _nodeData.Clear();
        _propagationSteps = 0;
        PropagateToNode(node);
        return true;
    }

    /// <summary>
    /// 停止 runtime，退出所有 active 节点并弹出 Blackboard local scope。
    /// </summary>
    public virtual void Stop()
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

    /// <summary>
    /// 受控恢复 active 节点。
    /// 
    /// 这个 API 只把实现 IFlowNode 的节点放回 active 列表，不调用 Enter。
    /// 保存恢复场景需要这一点，否则 MissionNode 这类节点会重复部署外部任务。
    /// </summary>
    public virtual int RestoreActiveNodes(IEnumerable<string> nodeIds)
    {
        if (nodeIds == null || Graph == null)
            return 0;

        if (_activeNodes.Count > 0)
        {
            for (int i = _activeNodes.Count - 1; i >= 0; i--)
                ExitActiveNode(i);
        }

        IsRunning = true;
        PushLocalBlackboardIfNeeded();
        _returnLabels.Clear();
        _propagationSteps = 0;

        int restored = 0;
        foreach (string nodeId in nodeIds)
        {
            if (RestoreActiveNode(nodeId))
                restored++;
        }

        return restored;
    }

    /// <summary>
    /// 获取或创建节点私有运行时数据。
    /// </summary>
    public T GetNodeData<T>(string nodeId) where T : class, new()
    {
        if (!_nodeData.TryGetValue(nodeId, out object value) || value is not T typed)
        {
            typed = new T();
            _nodeData[nodeId] = typed;
        }

        return typed;
    }

    /// <summary>
    /// 尝试读取节点私有运行时数据。
    /// </summary>
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

    /// <summary>
    /// 设置节点私有运行时数据。data 为 null 时移除该记录。
    /// </summary>
    public void SetNodeData(string nodeId, object data)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return;

        if (data == null)
            _nodeData.Remove(nodeId);
        else
            _nodeData[nodeId] = data;
    }

    /// <summary>
    /// 清理指定节点的私有运行时数据。
    /// </summary>
    public void ClearNodeData(string nodeId)
    {
        if (!string.IsNullOrWhiteSpace(nodeId))
            _nodeData.Remove(nodeId);
    }

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

    /// <summary>
    /// Tick active 节点。
    /// 当节点报告 completion 时，先退出节点，再根据 completion 推进 Sequence 连接。
    /// </summary>
    public virtual void Update(double delta)
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

    /// <summary>
    /// 从指定输出端口推进 Sequence 连接。
    /// </summary>
    public void PropagateFromOutput(string nodeId, int outputPort)
    {
        PropagateFromOutput(nodeId, outputPort, FlowConnectionMode.Sequence);
    }

    /// <summary>
    /// 节点刚进入后推进 Parallel 连接。
    /// Parallel 不依赖节点完成，因此适合“同时启动旁路流程”。
    /// </summary>
    protected void PropagateParallelFromNode(string nodeId)
    {
        PropagateFromOutput(nodeId, null, FlowConnectionMode.Parallel);
    }

    /// <summary>
    /// 根据连接 mode 和 outputPort 选择可穿越连接，并传播到目标节点。
    /// </summary>
    protected void PropagateFromOutput(string nodeId, int? outputPort, FlowConnectionMode mode)
    {
        if (Graph == null)
            return;

        var connections = Graph
            .GetOutgoingConnections(nodeId, outputPort)
            .Where(connection => ShouldTraverseConnection(connection, mode))
            .ToList();

        for (int i = 0; i < connections.Count; i++)
        {
            GraphNodeData nextNode = Graph.FindNodeById(connections[i].ToNode);
            if (nextNode != null)
            {
                OnConnectionTraversed(connections[i], mode);
                PropagateToNode(nextNode);
            }
        }
    }

    /// <summary>
    /// 判断连接是否属于当前推进时机，并进行可穿越检查。
    /// 子类可 override 注入业务筛选。
    /// </summary>
    protected virtual bool ShouldTraverseConnection(GraphConnection connection, FlowConnectionMode mode)
    {
        if (connection == null)
            return false;

        FlowConnectionMode connectionMode = connection is FlowConnection flowConnection
            ? flowConnection.Mode
            : FlowConnectionMode.Sequence;

        if (connectionMode != mode)
            return false;

        return CanTraverseConnection(connection);
    }

    /// <summary>
    /// 判断连接自身条件是否允许穿越。
    /// FlowConnection 会执行 Conditions；普通 GraphConnection 只检查 IsAvailable。
    /// </summary>
    protected virtual bool CanTraverseConnection(GraphConnection connection)
    {
        if (connection is FlowConnection flowConnection)
            return flowConnection.CanTraverse(Context);

        return connection?.IsAvailable == true;
    }

    /// <summary>
    /// 连接被穿越后的 hook。
    /// 默认不做事，业务 runtime 可用于记录统计或自定义行为。
    /// </summary>
    protected virtual void OnConnectionTraversed(GraphConnection connection, FlowConnectionMode mode)
    {
    }

    /// <summary>
    /// 进入一个节点并处理它的同步完成。
    /// IFlowNode 未完成时会加入 active 列表，非 IFlowNode 默认立即走 0 号输出。
    /// </summary>
    protected virtual void PropagateToNode(GraphNodeData node)
    {
        if (node == null)
            return;

        _propagationSteps++;
        if (_propagationSteps > MaxPropagationSteps)
        {
            GD.PushWarning($"[FlowGraphRuntime] Max propagation steps reached in graph: {Graph?.ResourcePath}");
            return;
        }

        OnNodeEntered(node);

        NodeCompletion completion;
        if (node is IFlowNode flowNode)
        {
            flowNode.Enter(this, Context);
            PropagateParallelFromNode(node.Id);

            // Enter 后立即查询一次完成状态。
            // 这样瞬时完成节点可以同步推进；长任务节点则进入 active 列表等待 Update。
            if (!flowNode.TryGetCompletion(this, Context, out completion))
            {
                _activeNodes.Add(new ActiveFlowNode(node, flowNode));
                return;
            }
        }
        else
        {
            node.Execute(Context);
            PropagateParallelFromNode(node.Id);
            completion = new NodeCompletion(0);
        }

        OnNodeExited(node);
        HandleCompletion(node, completion);
    }

    /// <summary>
    /// 根据节点完成结果处理返回或 Sequence 推进。
    /// OutputPort 小于 0 表示节点完成但不输出。
    /// </summary>
    protected virtual void HandleCompletion(GraphNodeData node, NodeCompletion completion)
    {
        if (node is FlowReturnNodeData)
        {
            _returnLabels.Add(completion.Label);
            OnReturned(node, completion.Label);
            return;
        }

        if (completion.OutputPort < 0)
            return;

        PropagateFromOutput(node.Id, completion.OutputPort);
    }

    /// <summary>
    /// 退出 active 节点并触发 OnNodeExited。
    /// </summary>
    private void ExitActiveNode(int index)
    {
        ActiveFlowNode active = _activeNodes[index];
        active.Handler.Exit(this, Context);
        _activeNodes.RemoveAt(index);
        OnNodeExited(active.Node);
    }

    /// <summary>
    /// 节点进入 hook。
    /// 触发 NodeEntered 事件。
    /// </summary>
    protected virtual void OnNodeEntered(GraphNodeData node)
    {
        NodeEntered?.Invoke(this, node);
    }

    /// <summary>
    /// 节点退出 hook。
    /// 触发 NodeExited 事件。
    /// </summary>
    protected virtual void OnNodeExited(GraphNodeData node)
    {
        NodeExited?.Invoke(this, node);
    }

    /// <summary>
    /// FlowReturnNodeData 返回 hook。
    /// </summary>
    protected virtual void OnReturned(GraphNodeData node, string label)
    {
        Returned?.Invoke(this, label);
    }

    /// <summary>
    /// 恢复单个 active 节点。
    /// 只接受 IFlowNode，因为只有 IFlowNode 才有可等待的生命周期。
    /// </summary>
    private bool RestoreActiveNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return false;

        for (int i = 0; i < _activeNodes.Count; i++)
        {
            if (_activeNodes[i].Node?.Id == nodeId)
                return false;
        }

        GraphNodeData node = Graph.FindNodeById(nodeId);
        if (node is not IFlowNode flowNode)
            return false;

        _activeNodes.Add(new ActiveFlowNode(node, flowNode));
        return true;
    }

    /// <summary>
    /// 确保当前 Graph 的 Blackboard local scope 已经入栈。
    /// </summary>
    private void PushLocalBlackboardIfNeeded()
    {
        if (!ManageLocalBlackboardScope || _localBlackboardPushed)
            return;

        Context.Blackboard.PushLocal(Graph);
        _localBlackboardPushed = true;
    }

    /// <summary>
    /// active 节点和其 IFlowNode handler 的运行时绑定。
    /// </summary>
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
