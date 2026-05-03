using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace Framework
{
    /// <summary>
    /// Resource management module implementation.
    /// </summary>
    internal sealed class ResourceModule : Module, IResourceModule, IProcessModule
    {
        private readonly ConcurrentQueue<ResourceHandleBase> _pendingCancels = new();
        private readonly List<WeakReference<ResourceHandleBase>> _trackedHandles = new();
        private readonly object _trackedHandlesLock = new();

        private IResourceLoader _loader;
        private IResourceCache _cache;
        private ResourceSetting _setting;
        private ResourceProfilerOverlay _profilerOverlay;
        private bool _enableLog;
        private int _nextProfilerHandleId;

        public override int Priority => 0;
        public int CacheCount => _cache.Count;

        public override void OnInit()
        {
            ResourceSetting setting = null;
            if (RootModule.Instance?.settings?.resourceSetting != null)
                setting = RootModule.Instance.settings.resourceSetting;

            _setting = setting ?? new ResourceSetting();
            _enableLog = _setting.EnableLog;

            _loader ??= new GodotResourceLoader(_setting.MaxConcurrentLoadCount);
            _cache ??= new ResourceCache(_setting.MaxCacheSize);
            
            EnsureProfilerOverlay();

            if (_profilerOverlay != null)
                _profilerOverlay.SetOverlayVisible(_setting.ShowProfilerOverlayOnStart);

            if (_enableLog)
                Debugger.Info($"[ResourceModule] Initialized. MaxCacheSize={_setting.MaxCacheSize}, MaxConcurrent={_setting.MaxConcurrentLoadCount}");
        }

        public override void Shutdown()
        {
            FlushPendingCancels();
            _loader?.Shutdown("ResourceModule shutdown");
            _cache.Clear();
            ReleaseProfilerOverlay();

            while (_pendingCancels.TryDequeue(out _))
            {
            }

            lock (_trackedHandlesLock)
            {
                _trackedHandles.Clear();
            }

            if (_enableLog)
                Debugger.Info("[ResourceModule] Shutdown.");
        }

        public void Process(double elapseSeconds, double realElapseSeconds)
        {
            FlushPendingCancels();
            _loader.Tick(_cache, _enableLog);
        }

        public ResourceHandle<T> LoadAsset<T>(string path) where T : Resource
        {
            var handle = CreateHandle<T>(path);

            if (TryFailInvalidPath(path, handle, "LoadAsset"))
                return handle;

            if (TryCompleteFromCache(path, handle, "Cache hit (sync)"))
                return handle;

            var resource = _loader.LoadSync(path);
            if (resource != null)
            {
                _cache.Set(path, resource);
                CompleteSuccess(path, resource, handle, "Loaded sync");
                return handle;
            }

            handle.SetFailedInternal($"Failed to load resource: '{path}'");
            Debugger.Error($"[ResourceModule] LoadAsset failed: '{path}'");
            return handle;
        }

        public T LoadAssetOnce<T>(string path) where T : Resource
        {
            return TryLoadAssetOnce(path, out T asset) ? asset : null;
        }

        public bool TryLoadAssetOnce<T>(string path, out T asset) where T : Resource
        {
            ResourceHandle<T> handle = LoadAsset<T>(path);
            asset = handle.Asset;
            bool isValid = handle.IsValid;
            handle.Release();
            return isValid && asset != null;
        }

        public ResourceHandle<T> LoadAssetAsync<T>(string path) where T : Resource
        {
            var handle = CreateHandle<T>(path);

            if (TryFailInvalidPath(path, handle, "LoadAssetAsync"))
                return handle;

            if (TryCompleteFromCache(path, handle, "Async cache hit"))
                return handle;

            _loader.RequestAsync(path, handle);

            if (_enableLog)
                Debugger.Info($"[ResourceModule] Async load requested: '{path}'");
            return handle;
        }

        public SceneHandle LoadSceneAsync(string path)
        {
            return new SceneHandle(LoadAssetAsync<PackedScene>(path));
        }

        public void ReleaseAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            _cache.Release(path);

            if (_enableLog)
                Debugger.Info($"[ResourceModule] ReleaseAsset: '{path}' [RefCount={_cache.GetRefCount(path)}]");
        }

        public void ForceUnloadAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            if (_cache.Remove(path) && _enableLog)
                Debugger.Info($"[ResourceModule] Force unloaded: '{path}'");
        }

        public void UnloadAllAssets()
        {
            _cache.Clear();
            if (_enableLog)
                Debugger.Info("[ResourceModule] All assets unloaded from cache.");
        }

        public bool HasAsset(string path)
        {
            return !string.IsNullOrEmpty(path) && _cache.Contains(path);
        }

        public int GetRefCount(string path)
        {
            return _cache.GetRefCount(path);
        }

        public ResourceProfilerSnapshot GetProfilerSnapshot()
        {
            var handles = CollectHandleProfilerEntries();
            var snapshot = new ResourceProfilerSnapshot
            {
                CreatedAtUtc = DateTime.UtcNow,
                CacheCount = _cache.Count,
                PendingCancelCount = _pendingCancels.Count,
                LiveHandleCount = handles.Count,
                LoadingHandleCount = CountHandles(handles, ResourceHandleStatus.Loading),
                SucceedHandleCount = CountHandles(handles, ResourceHandleStatus.Succeed),
                FailedHandleCount = CountHandles(handles, ResourceHandleStatus.Failed),
                CancelledHandleCount = CountHandles(handles, ResourceHandleStatus.Cancelled),
                ReleasedHandleCount = CountHandles(handles, ResourceHandleStatus.Released),
                InvalidHandleCount = CountInvalidHandles(handles),
                Loader = _loader.GetProfilerSnapshot(),
                Handles = handles,
                CacheEntries = _cache.GetProfilerEntries(),
            };

            return snapshot;
        }

        public bool IsProfilerOverlayVisible => _profilerOverlay != null && _profilerOverlay.Visible;

        public void DumpProfilerToLog(bool includeHandles = true, bool includeCacheEntries = true)
        {
            var snapshot = GetProfilerSnapshot();
            var summary = new StringBuilder();
            summary.Append("[ResourceProfiler] ");
            summary.Append($"handles={snapshot.LiveHandleCount} ");
            summary.Append($"loading={snapshot.LoadingHandleCount} ");
            summary.Append($"succeed={snapshot.SucceedHandleCount} ");
            summary.Append($"failed={snapshot.FailedHandleCount} ");
            summary.Append($"cancelled={snapshot.CancelledHandleCount} ");
            summary.Append($"released={snapshot.ReleasedHandleCount} ");
            summary.Append($"cache={snapshot.CacheCount} ");
            summary.Append($"pendingCancels={snapshot.PendingCancelCount} ");
            summary.Append($"loaderActive={snapshot.Loader.ActiveCount}/{snapshot.Loader.MaxConcurrent} ");
            summary.Append($"loaderWaiting={snapshot.Loader.WaitingCount}");
            Debugger.Info(summary.ToString());

            foreach (var task in snapshot.Loader.Tasks)
            {
                Debugger.Info(
                    $"[ResourceProfiler][Task] path='{task.Path}' started={task.IsStarted} done={task.IsDone} progress={task.Progress:0.00} requests={task.RequestCount} activeRequests={task.ActiveRequestCount}");
            }

            if (includeCacheEntries)
            {
                foreach (var entry in snapshot.CacheEntries)
                {
                    Debugger.Info(
                        $"[ResourceProfiler][Cache] lru={entry.LruIndex} path='{entry.Path}' type={entry.ResourceTypeName} refCount={entry.RefCount}");
                }
            }

            if (includeHandles)
            {
                foreach (var handle in snapshot.Handles)
                {
                    Debugger.Info(
                        $"[ResourceProfiler][Handle] id={handle.HandleId} path='{handle.Path}' type={handle.RequestedTypeName} status={handle.Status} progress={handle.Progress:0.00} ownsRef={handle.OwnsReference} valid={handle.IsValid} error='{handle.Error}'");
                }
            }
        }

        public void SetProfilerOverlayVisible(bool visible)
        {
            if (visible)
                EnsureProfilerOverlay(forceCreate: true);

            _profilerOverlay?.SetOverlayVisible(visible);
        }

        public void ToggleProfilerOverlay()
        {
            if (_profilerOverlay == null)
            {
                EnsureProfilerOverlay(forceCreate: true);
                _profilerOverlay?.SetOverlayVisible(true);
                return;
            }

            _profilerOverlay.SetOverlayVisible(!_profilerOverlay.Visible);
        }

        public void SetLoader(IResourceLoader loader)
        {
            if (loader == null)
            {
                Debugger.Error("[ResourceModule] SetLoader: loader is null.");
                return;
            }

            _loader = loader;
        }

        public void SetCache(IResourceCache cache)
        {
            if (cache == null)
            {
                Debugger.Error("[ResourceModule] SetCache: cache is null.");
                return;
            }

            _cache = cache;
        }

        internal void RequestCancel(ResourceHandleBase handle)
        {
            if (handle == null)
                return;

            _pendingCancels.Enqueue(handle);
        }

        private ResourceHandle<T> CreateHandle<T>(string path) where T : Resource
        {
            var handle = new ResourceHandle<T>(path, this);
            TrackHandle(handle);
            return handle;
        }

        private void EnsureProfilerOverlay(bool forceCreate = false)
        {
            if (_profilerOverlay != null || _setting == null || (!forceCreate && !_setting.EnableProfilerOverlay))
                return;

            if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
                return;

            _profilerOverlay = new ResourceProfilerOverlay(
                GetProfilerSnapshot,
                () => DumpProfilerToLog(includeHandles: true, includeCacheEntries: true),
                _setting.ProfilerOverlayRefreshInterval,
                _setting.ProfilerOverlayMaxRows);

            tree.Root.CallDeferred(Node.MethodName.AddChild, _profilerOverlay);
        }

        private void ReleaseProfilerOverlay()
        {
            if (_profilerOverlay == null)
                return;

            if (GodotObject.IsInstanceValid(_profilerOverlay))
                _profilerOverlay.QueueFree();

            _profilerOverlay = null;
        }

        private void TrackHandle(ResourceHandleBase handle)
        {
            lock (_trackedHandlesLock)
            {
                handle.ProfilerId = ++_nextProfilerHandleId;
                _trackedHandles.Add(new WeakReference<ResourceHandleBase>(handle));
            }
        }

        private List<ResourceHandleProfilerEntry> CollectHandleProfilerEntries()
        {
            var entries = new List<ResourceHandleProfilerEntry>();

            lock (_trackedHandlesLock)
            {
                for (int i = _trackedHandles.Count - 1; i >= 0; i--)
                {
                    if (!_trackedHandles[i].TryGetTarget(out var handle))
                    {
                        _trackedHandles.RemoveAt(i);
                        continue;
                    }

                    entries.Add(new ResourceHandleProfilerEntry
                    {
                        HandleId = handle.ProfilerId,
                        Path = handle.Path,
                        RequestedTypeName = handle.RequestedTypeName,
                        Status = handle.Status,
                        Progress = handle.Progress,
                        OwnsReference = handle.OwnsReference,
                        IsValid = handle.IsValid,
                        Error = handle.Error,
                    });
                }
            }

            entries.Sort((left, right) =>
            {
                var pathCompare = string.CompareOrdinal(left.Path, right.Path);
                return pathCompare != 0 ? pathCompare : left.HandleId.CompareTo(right.HandleId);
            });

            return entries;
        }

        private void FlushPendingCancels()
        {
            while (_pendingCancels.TryDequeue(out var handle))
            {
                if (handle == null || handle.IsDone)
                    continue;

                handle.SetCancelledInternal();

                if (_enableLog)
                    Debugger.Info($"[ResourceModule] Cancelled: '{handle.Path}'");
            }
        }

        private bool TryFailInvalidPath<T>(string path, ResourceHandle<T> handle, string caller) where T : Resource
        {
            if (!string.IsNullOrEmpty(path))
                return false;

            handle.SetFailedInternal("Path is null or empty.");
            Debugger.Error($"[ResourceModule] {caller}: path is null or empty.");
            return true;
        }

        private bool TryCompleteFromCache<T>(string path, ResourceHandle<T> handle, string logLabel) where T : Resource
        {
            if (!_cache.TryGet(path, out var cached))
                return false;

            CompleteSuccess(path, cached, handle, logLabel);
            return true;
        }

        private void CompleteSuccess<T>(string path, Resource resource, ResourceHandle<T> handle, string logLabel) where T : Resource
        {
            handle.SetSucceedInternal(resource);

            if (handle.IsValid)
            {
                _cache.Acquire(path);
                handle.MarkReferenceAcquiredInternal();
            }

            if (_enableLog)
                Debugger.Info($"[ResourceModule] {logLabel}: '{path}' [RefCount={_cache.GetRefCount(path)}]");
        }

        private static int CountHandles(IReadOnlyList<ResourceHandleProfilerEntry> handles, ResourceHandleStatus status)
        {
            var count = 0;
            for (int i = 0; i < handles.Count; i++)
            {
                if (handles[i].Status == status)
                    count++;
            }

            return count;
        }

        private static int CountInvalidHandles(IReadOnlyList<ResourceHandleProfilerEntry> handles)
        {
            var count = 0;
            for (int i = 0; i < handles.Count; i++)
            {
                if (!handles[i].IsValid)
                    count++;
            }

            return count;
        }
    }
}
