using System.Collections.Generic;
using Godot;

namespace Framework
{
    /// <summary>
    /// 资源管理模块实现。
    /// <para>
    /// 通过 <see cref="ModuleSystem.GetModule{T}"/> 以 <see cref="IResourceModule"/> 接口获取实例。
    /// 命名遵循框架约定：接口名去掉 'I' 前缀即为实现类名（<c>IResourceModule</c> → <c>ResourceModule</c>）。
    /// </para>
    /// <para>
    /// 功能概览：
    /// <list type="bullet">
    ///   <item>同步加载：优先命中缓存，缓存未命中时使用 <see cref="IResourceLoader"/> 同步读取。</item>
    ///   <item>异步加载：利用 Godot 后台线程（<c>LoadThreadedRequest</c>），每帧在 <see cref="Process"/> 中轮询，支持同路径合并请求。</item>
    ///   <item>LRU 缓存：默认使用 <see cref="ResourceCache"/>，淘汰时依赖 Godot 原生引用计数判断资源是否可释放。</item>
    ///   <item>可替换策略：通过 <see cref="SetLoader"/> / <see cref="SetCache"/> 注入自定义实现。</item>
    /// </list>
    /// </para>
    /// </summary>
    internal sealed class ResourceModule : Module, IResourceModule, IProcessModule
    {
        // 进行中的异步任务表：path → task
        private readonly Dictionary<string, ResourceLoadTask> _pendingTasks;
        // 本帧需要移除的已完成任务（避免遍历时修改字典）
        private readonly List<string> _completedPaths;

        private IResourceLoader _loader;
        private IResourceCache _cache;
        private ResourceSetting _setting;
        private bool _enableLog;

        public ResourceModule()
        {
            _pendingTasks = new Dictionary<string, ResourceLoadTask>();
            _completedPaths = new List<string>();
        }

        /// <inheritdoc/>
        public override int Priority => 0;

        /// <inheritdoc/>
        public int CacheCount => _cache.Count;

        // ---- Module 生命周期 ----

        public override void OnInit()
        {
            // 尝试从 RootModule 读取配置，若无则使用默认值
            ResourceSetting setting = null;
            if (RootModule.Instance?.settings?.resourceSetting != null)
            {
                setting = RootModule.Instance.settings.resourceSetting;
            }

            _setting = setting ?? new ResourceSetting();
            _enableLog = _setting.EnableLog;

            _loader ??= new GodotResourceLoader();
            _cache ??= new ResourceCache(_setting.MaxCacheSize);

            if (_enableLog)
                Debugger.Info($"[ResourceModule] Initialized. MaxCacheSize={_setting.MaxCacheSize}, MaxConcurrent={_setting.MaxConcurrentLoadCount}");
        }

        public override void Shutdown()
        {
            _cache.Clear();
            _pendingTasks.Clear();
            _completedPaths.Clear();

            if (_enableLog)
                Debugger.Info("[ResourceModule] Shutdown.");
        }

        // ---- IProcessModule ----

        /// <summary>
        /// 每帧轮询所有进行中的异步加载任务。
        /// </summary>
        public void Process(double elapseSeconds, double realElapseSeconds)
        {
            if (_pendingTasks.Count == 0) return;

            _completedPaths.Clear();

            foreach (var kv in _pendingTasks)
            {
                var task = kv.Value;
                if (task.Poll(_cache, _enableLog))
                {
                    _completedPaths.Add(kv.Key);
                }
            }

            foreach (var path in _completedPaths)
                _pendingTasks.Remove(path);
        }

        // ---- IResourceModule ----

        /// <inheritdoc/>
        public T LoadAsset<T>(string path) where T : Resource
        {
            if (string.IsNullOrEmpty(path))
            {
                Debugger.Error("[ResourceModule] LoadAsset: path is null or empty.");
                return null;
            }

            // 命中缓存
            if (_cache.TryGet(path, out var cached))
            {
                if (_enableLog) Debugger.Info($"[ResourceModule] Cache hit: '{path}'");
                return cached as T;
            }

            // 同步加载
            var resource = _loader.LoadSync(path);
            if (resource != null)
            {
                _cache.Set(path, resource);
                if (_enableLog) Debugger.Info($"[ResourceModule] Loaded sync: '{path}'");
                return resource as T;
            }

            Debugger.Error($"[ResourceModule] LoadAsset failed: '{path}'");
            return null;
        }

        /// <inheritdoc/>
        public ResourceHandle<T> LoadAssetAsync<T>(string path) where T : Resource
        {
            var handle = new ResourceHandle<T>(path, this);

            if (string.IsNullOrEmpty(path))
            {
                handle.SetFailedInternal("Path is null or empty.");
                return handle;
            }

            // 命中缓存，立即完成
            if (_cache.TryGet(path, out var cached))
            {
                if (_enableLog) Debugger.Info($"[ResourceModule] Async cache hit: '{path}'");
                handle.SetSucceedInternal(cached);
                return handle;
            }

            // 已有相同路径的进行中任务，直接挂载句柄
            if (_pendingTasks.TryGetValue(path, out var existingTask))
            {
                existingTask.Handles.Add(handle);
                if (_enableLog) Debugger.Info($"[ResourceModule] Merged async request: '{path}'");
                return handle;
            }

            // 发起新的异步加载任务
            var task = _loader.RequestAsync(path);
            task.Handles.Add(handle);
            _pendingTasks[path] = task;

            if (_enableLog) Debugger.Info($"[ResourceModule] Async load requested: '{path}'");
            return handle;
        }

        /// <inheritdoc/>
        public void UnloadAsset(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (_cache.Remove(path))
            {
                if (_enableLog) Debugger.Info($"[ResourceModule] Unloaded: '{path}'");
            }
        }

        /// <inheritdoc/>
        public void UnloadAllAssets()
        {
            _cache.Clear();
            if (_enableLog) Debugger.Info("[ResourceModule] All assets unloaded from cache.");
        }

        /// <inheritdoc/>
        public bool HasAsset(string path)
        {
            return !string.IsNullOrEmpty(path) && _cache.Contains(path);
        }

        /// <inheritdoc/>
        public void SetLoader(IResourceLoader loader)
        {
            if (loader == null)
            {
                Debugger.Error("[ResourceModule] SetLoader: loader is null.");
                return;
            }
            _loader = loader;
        }

        /// <inheritdoc/>
        public void SetCache(IResourceCache cache)
        {
            if (cache == null)
            {
                Debugger.Error("[ResourceModule] SetCache: cache is null.");
                return;
            }
            _cache = cache;
        }

    }
}
