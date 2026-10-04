using System;
using System.Collections.Generic;
using Godot;

namespace Framework
{
    /// <summary>
    /// Godot Node 对象池实现。
    /// <para>
    /// 以 <see cref="PackedScene"/> 为模板实例化 Node，回收时将节点设为不可见（<c>Visible = false</c>）
    /// 并保留在父节点的子节点树中，取出时重新设为可见，避免反复的 AddChild/RemoveChild 开销。
    /// </para>
    /// <para>
    /// 若池中 Node 实现了 <see cref="IObjectPoolItem"/>，则在 Spawn/Recycle 时自动调用
    /// <see cref="IObjectPoolItem.OnSpawn"/>/<see cref="IObjectPoolItem.OnRecycle"/>。
    /// </para>
    /// <para>
    /// 超出容量且 <see cref="AllowOverflow"/> 为 false 时，多余的回收 Node 将被
    /// <see cref="Node.QueueFree"/> 彻底销毁。
    /// </para>
    /// </summary>
    internal sealed class NodePool : INodePool
    {
        private readonly Queue<Node> _idleQueue;
        private readonly string _name;
        private readonly string _scenePath;
        private readonly PackedScene _packedScene;
        private readonly ResourceHandle<PackedScene> _sceneHandle;
        private readonly HashSet<Node> _activeNodes = new();
        private readonly Dictionary<Node, PooledNodeState> _idleStates = new();
        private bool _shutdown;
        private Node _parent;
        private int _capacity;
        private bool _allowOverflow;
        private float _autoReleaseInterval;
        private double _autoReleaseTimer;

        /// <summary>
        /// 初始化 Node 对象池。
        /// </summary>
        /// <param name="scenePath">PackedScene 资源路径。</param>
        /// <param name="packedScene">已加载好的 PackedScene 资源。</param>
        /// <param name="parent">所有池中 Node 的父节点。</param>
        /// <param name="name">对象池名称。</param>
        /// <param name="capacity">容量上限。</param>
        /// <param name="autoReleaseInterval">自动释放间隔（秒），小于等于 0 表示禁用。</param>
        /// <param name="sceneHandle">Owned resource reference, released on shutdown.</param>
        public NodePool(string scenePath, PackedScene packedScene, Node parent,
            string name, int capacity, float autoReleaseInterval, ResourceHandle<PackedScene> sceneHandle = null)
        {
            if (packedScene == null)
                throw new Exception($"[NodePool] PackedScene is null for path: '{scenePath}'.");
            if (parent == null)
                throw new Exception($"[NodePool] Parent node is null for pool: '{name}'.");

            _scenePath = scenePath ?? string.Empty;
            _packedScene = packedScene;
            _parent = parent;
            _name = name ?? string.Empty;
            _capacity = capacity > 0 ? capacity : 32;
            _autoReleaseInterval = autoReleaseInterval;
            _allowOverflow = false;
            _autoReleaseTimer = 0;
            _idleQueue = new Queue<Node>(_capacity);
            _sceneHandle = sceneHandle;
        }

        // ---- INodePool 属性实现 ----

        /// <inheritdoc/>
        public string Name => _name;

        /// <inheritdoc/>
        public string ScenePath => _scenePath;

        /// <inheritdoc/>
        public int Count => _idleQueue.Count;

        /// <inheritdoc/>
        public int Capacity
        {
            get => _capacity;
            set => _capacity = value > 0 ? value : 1;
        }

        /// <inheritdoc/>
        public bool AllowOverflow
        {
            get => _allowOverflow;
            set => _allowOverflow = value;
        }

        /// <inheritdoc/>
        public float AutoReleaseInterval
        {
            get => _autoReleaseInterval;
            set
            {
                _autoReleaseInterval = value;
                _autoReleaseTimer = 0;
            }
        }

        // ---- INodePool 方法实现 ----

        /// <summary>
        /// 从对象池中取出一个 Node。
        /// <para>若池中有空闲 Node 则 Dequeue 并设为可见；否则由 PackedScene 实例化新节点并加入父节点。</para>
        /// <para>若 Node 实现了 <see cref="IObjectPoolItem"/>，则自动调用 <see cref="IObjectPoolItem.OnSpawn"/>。</para>
        /// </summary>
        public Node Spawn()
        {
            if (_shutdown || !GodotObject.IsInstanceValid(_parent) || _parent.IsQueuedForDeletion())
                throw new InvalidOperationException($"NodePool '{_name}' has no active owner.");
            Node node = null;
            while (_idleQueue.Count > 0)
            {
                node = _idleQueue.Dequeue();
                _idleStates.Remove(node, out var state);
                if (GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
                {
                    state?.Restore();
                    break;
                }
                node = null;
            }
            if (node == null)
            {
                // 实例化新节点并加入父节点
                node = _packedScene.Instantiate();
                _parent.AddChild(node);
                Debugger.Info($"[NodePool({_scenePath})] Instantiated new node (total managed: {_parent.GetChildCount()}).");
            }

            _activeNodes.Add(node);
            try
            {
                if (node is IObjectPoolItem poolItem)
                    poolItem.OnSpawn();
            }
            catch
            {
                _activeNodes.Remove(node);
                node.QueueFree();
                throw;
            }

            return node;
        }

        /// <summary>
        /// 将 Node 回收到对象池。
        /// <para>若 Node 实现了 <see cref="IObjectPoolItem"/>，则先调用 <see cref="IObjectPoolItem.OnRecycle"/>。</para>
        /// <para>若池已满且 <see cref="AllowOverflow"/> 为 false，Node 将被 <see cref="Node.QueueFree"/> 销毁。</para>
        /// </summary>
        public void Recycle(Node node)
        {
            if (node == null || !_activeNodes.Remove(node))
            {
                Debugger.Warn($"[NodePool({_scenePath})] Recycle rejected: node is not active in this pool.");
                return;
            }
            if (!GodotObject.IsInstanceValid(node) || node.IsQueuedForDeletion())
                return;
            var state = new PooledNodeState(node);
            state.Suspend();
            try
            {
                if (node is IObjectPoolItem poolItem)
                    poolItem.OnRecycle();
            }
            catch
            {
                node.QueueFree();
                throw;
            }

            if (!GodotObject.IsInstanceValid(node) || node.IsQueuedForDeletion())
                return;
            if (!_shutdown && (_idleQueue.Count < _capacity || _allowOverflow))
            {
                _idleStates[node] = state;
                _idleQueue.Enqueue(node);
            }
            else
            {
                // 超出容量：彻底销毁
                Debugger.Warn($"[NodePool({_scenePath})] Pool is full (capacity={_capacity}), node queued for free.");
                node.QueueFree();
            }
        }

        /// <summary>
        /// 立即释放池中所有空闲 Node（调用 <see cref="Node.QueueFree"/>）。
        /// </summary>
        public void ReleaseAllUnused()
        {
            int count = _idleQueue.Count;
            while (_idleQueue.Count > 0)
            {
                var node = _idleQueue.Dequeue();
                _idleStates.Remove(node);
                if (GodotObject.IsInstanceValid(node))
                    node.QueueFree();
            }
            Debugger.Info($"[NodePool({_scenePath})] Released {count} idle nodes.");
        }

        // ---- 供 ObjectPoolModule 调用的内部方法 ----

        /// <summary>
        /// 每帧更新自动释放计时器。
        /// </summary>
        internal void Process(double elapseSeconds, double realElapseSeconds)
        {
            if (_shutdown)
                return;
            if (!GodotObject.IsInstanceValid(_parent) || _parent.IsQueuedForDeletion())
            {
                Shutdown();
                return;
            }
            _activeNodes.RemoveWhere(node => !GodotObject.IsInstanceValid(node));
            if (_autoReleaseInterval <= 0f || _idleQueue.Count == 0)
                return;

            _autoReleaseTimer += realElapseSeconds;
            if (_autoReleaseTimer >= _autoReleaseInterval)
            {
                _autoReleaseTimer = 0;
                ReleaseAllUnused();
            }
        }

        /// <summary>
        /// 关闭并清理 Node 对象池：
        /// <list type="bullet">
        ///   <item>QueueFree 所有空闲节点。</item>
        ///   <item>Release this pool's scene handle without invalidating other users.</item>
        /// </list>
        /// <para>注意：Spawn 出去但尚未 Recycle 的节点不在此管理范围内，
        /// 调用方需自行确保所有活跃节点在 Shutdown 前已 Recycle 或 QueueFree。</para>
        /// </summary>
        internal void Shutdown()
        {
            if (_shutdown)
                return;
            _shutdown = true;
            ReleaseAllUnused();
            _activeNodes.Clear();
            _sceneHandle?.Dispose();
            _parent = null;
        }
    }
}
