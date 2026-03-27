using System;
using System.Collections.Generic;
using Godot;

namespace Framework
{
    /// <summary>
    /// 对象池模块实现。
    /// <para>
    /// 通过 <see cref="ModuleSystem.GetModule{T}"/> 以 <see cref="IObjectPoolModule"/> 接口获取实例。
    /// 命名遵循框架约定：接口名去掉 'I' 前缀即为实现类名（<c>IObjectPoolModule</c> → <c>ObjectPoolModule</c>）。
    /// </para>
    /// <para>
    /// 功能概览：
    /// <list type="bullet">
    ///   <item>纯 C# 对象池：对实现了 <see cref="IObjectPoolItem"/> 的类，通过 <c>new T()</c> 创建实例，无需 Godot 场景。</item>
    ///   <item>Node 对象池：通过 <see cref="PackedScene"/> 实例化 Godot Node，回收时隐藏节点保留在父节点下。</item>
    ///   <item>自动释放：每帧驱动各池的计时器，超过 <c>AutoReleaseInterval</c> 后自动清空空闲对象。</item>
    ///   <item>可按类型+名称区分：同一类型可创建多个不同名的池。</item>
    /// </list>
    /// </para>
    /// </summary>
    internal sealed class ObjectPoolModule : Module, IObjectPoolModule, IProcessModule
    {
        // 纯 C# 对象池表：TypeNamePair(itemType, name) → ObjectPoolBase
        private readonly Dictionary<TypeNamePair, ObjectPoolBase> _objectPoolMap;
        // Node 对象池表：TypeNamePair(scenePath作为特殊key, name) → NodePool
        private readonly Dictionary<TypeNamePair, NodePool> _nodePoolMap;

        // 用于存储 scenePath 的虚拟 Type（以 string.GetHashCode 区分，通过包装类实现）
        // 实际直接使用 typeof(NodePool) 作为类型键，再用 scenePath 作为 name 键
        private IResourceModule _resourceModule;

        public ObjectPoolModule()
        {
            _objectPoolMap = new Dictionary<TypeNamePair, ObjectPoolBase>();
            _nodePoolMap = new Dictionary<TypeNamePair, NodePool>();
        }

        /// <inheritdoc/>
        public override int Priority => 0;

        /// <inheritdoc/>
        public int Count => _objectPoolMap.Count + _nodePoolMap.Count;

        // ---- Module 生命周期 ----

        public override void OnInit()
        {
            Debugger.Info("[ObjectPoolModule] Initialized.");
        }

        public override void Shutdown()
        {
            // 关闭所有纯 C# 对象池
            foreach (var pool in _objectPoolMap.Values)
                pool.Shutdown();
            _objectPoolMap.Clear();

            // 关闭所有 Node 对象池
            foreach (var pool in _nodePoolMap.Values)
                pool.Shutdown();
            _nodePoolMap.Clear();

            Debugger.Info("[ObjectPoolModule] Shutdown.");
        }

        // ---- IProcessModule ----

        /// <summary>
        /// 每帧驱动所有对象池的自动释放计时器。
        /// </summary>
        public void Process(double elapseSeconds, double realElapseSeconds)
        {
            foreach (var pool in _objectPoolMap.Values)
                pool.Process(elapseSeconds, realElapseSeconds);

            foreach (var pool in _nodePoolMap.Values)
                pool.Process(elapseSeconds, realElapseSeconds);
        }

        // ===========================
        // 纯 C# 对象池
        // ===========================

        /// <inheritdoc/>
        public bool HasObjectPool<T>(string name = "") where T : class, IObjectPoolItem
        {
            return _objectPoolMap.ContainsKey(new TypeNamePair(typeof(T), name));
        }

        /// <inheritdoc/>
        public IObjectPool<T> GetObjectPool<T>(string name = "") where T : class, IObjectPoolItem
        {
            var key = new TypeNamePair(typeof(T), name);
            return _objectPoolMap.TryGetValue(key, out var pool) ? pool as IObjectPool<T> : null;
        }

        /// <inheritdoc/>
        public IObjectPool<T> CreateObjectPool<T>(string name = "", int capacity = 32, float autoReleaseInterval = 60f)
            where T : class, IObjectPoolItem, new()
        {
            var key = new TypeNamePair(typeof(T), name);
            if (_objectPoolMap.ContainsKey(key))
            {
                throw new Exception(
                    $"[ObjectPoolModule] ObjectPool<{typeof(T).Name}>(name='{name}') already exists.");
            }

            var pool = new ObjectPool<T>(name, capacity, autoReleaseInterval);
            _objectPoolMap[key] = pool;

            Debugger.Info(
                $"[ObjectPoolModule] Created ObjectPool<{typeof(T).Name}>(name='{name}', capacity={capacity}, autoRelease={autoReleaseInterval}s).");
            return pool;
        }

        /// <inheritdoc/>
        public bool DestroyObjectPool<T>(string name = "") where T : class, IObjectPoolItem
        {
            var key = new TypeNamePair(typeof(T), name);
            if (_objectPoolMap.TryGetValue(key, out var pool))
            {
                pool.Shutdown();
                _objectPoolMap.Remove(key);
                Debugger.Info($"[ObjectPoolModule] Destroyed ObjectPool<{typeof(T).Name}>(name='{name}').");
                return true;
            }

            return false;
        }

        // ===========================
        // Node 对象池
        // ===========================

        /// <summary>
        /// 以 scenePath 作为 "名称字段"、<see cref="NodePool"/> 类型作为 "类型字段" 构建 Key。
        /// 再用池名称 name 拼接到 scenePath 后以区分同路径不同名的池。
        /// </summary>
        private static TypeNamePair MakeNodePoolKey(string scenePath, string name)
        {
            // 使用 typeof(NodePool) 作为固定类型占位，scenePath+name 作为唯一标识符
            string combinedName = string.IsNullOrEmpty(name) ? scenePath : $"{scenePath}#{name}";
            return new TypeNamePair(typeof(NodePool), combinedName);
        }

        /// <inheritdoc/>
        public bool HasNodePool(string scenePath, string name = "")
        {
            return _nodePoolMap.ContainsKey(MakeNodePoolKey(scenePath, name));
        }

        /// <inheritdoc/>
        public INodePool GetNodePool(string scenePath, string name = "")
        {
            var key = MakeNodePoolKey(scenePath, name);
            return _nodePoolMap.TryGetValue(key, out var pool) ? pool : null;
        }

        /// <inheritdoc/>
        public INodePool CreateNodePool(string scenePath, Node parent, string name = "",
            int capacity = 32, float autoReleaseInterval = 60f)
        {
            if (string.IsNullOrEmpty(scenePath))
                throw new Exception("[ObjectPoolModule] CreateNodePool: scenePath is null or empty.");

            if (parent == null)
                throw new Exception("[ObjectPoolModule] CreateNodePool: parent node is null.");

            var key = MakeNodePoolKey(scenePath, name);
            if (_nodePoolMap.ContainsKey(key))
            {
                throw new Exception(
                    $"[ObjectPoolModule] NodePool(scenePath='{scenePath}', name='{name}') already exists.");
            }

            // 通过 ResourceModule 同步加载 PackedScene（返回 Handle）
            _resourceModule ??= ModuleSystem.GetModule<IResourceModule>();
            var handle = _resourceModule?.LoadAsset<PackedScene>(scenePath);

            if (handle == null || !handle.IsValid)
            {
                throw new Exception(
                    $"[ObjectPoolModule] CreateNodePool: Failed to load PackedScene at '{scenePath}'.");
            }

            var pool = new NodePool(scenePath, handle.Asset, parent, name, capacity, autoReleaseInterval, _resourceModule);
            _nodePoolMap[key] = pool;

            Debugger.Info(
                $"[ObjectPoolModule] Created NodePool(scenePath='{scenePath}', name='{name}', capacity={capacity}, autoRelease={autoReleaseInterval}s).");
            return pool;
        }

        /// <inheritdoc/>
        public void CreateNodePoolAsync(string scenePath, Node parent, Action<INodePool> onCompleted,
            string name = "", int capacity = 32, float autoReleaseInterval = 60f)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                Debugger.Error("[ObjectPoolModule] CreateNodePoolAsync: scenePath is null or empty.");
                onCompleted?.Invoke(null);
                return;
            }

            if (parent == null)
            {
                Debugger.Error("[ObjectPoolModule] CreateNodePoolAsync: parent node is null.");
                onCompleted?.Invoke(null);
                return;
            }

            var key = MakeNodePoolKey(scenePath, name);
            if (_nodePoolMap.ContainsKey(key))
            {
                Debugger.Warn(
                    $"[ObjectPoolModule] CreateNodePoolAsync: NodePool(scenePath='{scenePath}', name='{name}') already exists, returning existing pool.");
                onCompleted?.Invoke(_nodePoolMap[key]);
                return;
            }

            // 通过 ResourceModule 异步加载 PackedScene，遵循 ResourceModule 的资源管理规范
            _resourceModule ??= ModuleSystem.GetModule<IResourceModule>();
            _resourceModule.LoadAssetAsync<PackedScene>(scenePath)
                .OnCompleted(handle =>
                {
                    if (!handle.IsValid)
                    {
                        Debugger.Error(
                            $"[ObjectPoolModule] CreateNodePoolAsync: Failed to load PackedScene at '{scenePath}'. Error: {handle.Error}");
                        onCompleted?.Invoke(null);
                        return;
                    }

                    // 加载完成后再次检查（防止在异步等待期间重复创建）
                    if (_nodePoolMap.ContainsKey(key))
                    {
                        Debugger.Warn(
                            $"[ObjectPoolModule] CreateNodePoolAsync: Pool was created during async load, returning existing pool.");
                        onCompleted?.Invoke(_nodePoolMap[key]);
                        return;
                    }

                    var pool = new NodePool(scenePath, handle.Asset, parent, name, capacity, autoReleaseInterval, _resourceModule);
                    _nodePoolMap[key] = pool;

                    Debugger.Info(
                        $"[ObjectPoolModule] Created NodePool async (scenePath='{scenePath}', name='{name}', capacity={capacity}, autoRelease={autoReleaseInterval}s).");
                    onCompleted?.Invoke(pool);
                });
        }

        /// <inheritdoc/>
        public bool DestroyNodePool(string scenePath, string name = "")
        {
            var key = MakeNodePoolKey(scenePath, name);
            if (_nodePoolMap.TryGetValue(key, out var pool))
            {
                pool.Shutdown();
                _nodePoolMap.Remove(key);
                Debugger.Info($"[ObjectPoolModule] Destroyed NodePool(scenePath='{scenePath}', name='{name}').");
                return true;
            }

            return false;
        }

        // ===========================
        // 全局操作
        // ===========================

        /// <inheritdoc/>
        public void ReleaseAllUnused()
        {
            foreach (var pool in _objectPoolMap.Values)
                pool.ReleaseAllUnused();

            foreach (var pool in _nodePoolMap.Values)
                pool.ReleaseAllUnused();

            Debugger.Info("[ObjectPoolModule] ReleaseAllUnused: all pools flushed.");
        }
    }
}
