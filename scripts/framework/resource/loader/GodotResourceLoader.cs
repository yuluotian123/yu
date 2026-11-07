using System.Collections.Generic;
using Godot;

namespace Framework
{
    /// <summary>
    /// Default loader built on top of Godot's ResourceLoader.
    /// </summary>
    public sealed class GodotResourceLoader : IResourceLoader
    {
        private readonly int _maxConcurrent;
        private int _activeCount;
        private readonly Dictionary<string, ResourceLoadTask> _allTasks;
        private readonly Queue<string> _waitingQueue;
        private readonly List<string> _completedPaths;
        private readonly HashSet<string> _preparedDependencies = new();
        private readonly List<Script> _scriptDependencies = new();

        public GodotResourceLoader(int maxConcurrent = 8)
        {
            _maxConcurrent = maxConcurrent > 0 ? maxConcurrent : 1;
            _allTasks = new Dictionary<string, ResourceLoadTask>();
            _waitingQueue = new Queue<string>();
            _completedPaths = new List<string>();
        }

        public Resource LoadSync(string path)
        {
            if (!ResourceLoader.Exists(path))
            {
                Debugger.Error($"[ResourceLoader] Resource not found: '{path}'");
                return null;
            }

            var resource = ResourceLoader.Load(path);
            if (resource == null)
                Debugger.Error($"[ResourceLoader] Failed to load resource: '{path}'");
            return resource;
        }

        public void RequestAsync(string path, ResourceHandleBase handle, string typeHint = "")
        {
            if (_allTasks.TryGetValue(path, out var existingTask))
            {
                if (!existingTask.IsDone)
                {
                    existingTask.AddHandle(handle);
                    return;
                }

                _allTasks.Remove(path);
            }

            var task = new ResourceLoadTask(path, OnTaskCompleted);
            task.AddHandle(handle);
            _allTasks[path] = task;

            if (_activeCount < _maxConcurrent)
                StartTask(task, typeHint);
            else
                _waitingQueue.Enqueue(path);
        }

        public void Tick(IResourceCache cache, bool enableLog = false)
        {
            var taskSnapshot = new List<KeyValuePair<string, ResourceLoadTask>>(_allTasks);
            _completedPaths.Clear();
            foreach (var pair in taskSnapshot)
            {
                if (!pair.Value.IsStarted)
                    continue;

                if (pair.Value.Poll(cache, enableLog))
                    _completedPaths.Add(pair.Key);
            }

            foreach (var path in _completedPaths)
                _allTasks.Remove(path);

            while (_waitingQueue.Count > 0 && _activeCount < _maxConcurrent)
            {
                var path = _waitingQueue.Dequeue();
                if (_allTasks.TryGetValue(path, out var task))
                    StartTask(task);
            }
        }

        public ResourceLoaderProfilerSnapshot GetProfilerSnapshot()
        {
            var taskSnapshot = new List<ResourceLoadTask>(_allTasks.Values);
            var tasks = new List<ResourceLoadTaskProfilerEntry>(taskSnapshot.Count);
            foreach (var task in taskSnapshot)
                tasks.Add(task.GetProfilerEntry());

            return new ResourceLoaderProfilerSnapshot
            {
                MaxConcurrent = _maxConcurrent,
                ActiveCount = _activeCount,
                WaitingCount = _waitingQueue.Count,
                TaskCount = _allTasks.Count,
                Tasks = tasks,
            };
        }

        public void Shutdown(string reason = null)
        {
            var taskSnapshot = new List<ResourceLoadTask>(_allTasks.Values);
            foreach (var task in taskSnapshot)
                task.CancelAll(reason);

            // Cancelling our handles does not cancel Godot's native worker. Drain
            // started requests before the engine tears down scripts and materials.
            foreach (var task in taskSnapshot)
            {
                if (!task.IsStarted || ResourceLoader.LoadThreadedGetStatus(task.Path) == ResourceLoader.ThreadLoadStatus.InvalidResource)
                    continue;
                // The result may share its managed wrapper with another owner.
                // Consume the request without disposing that shared resource.
                _ = ResourceLoader.LoadThreadedGet(task.Path);
            }

            _allTasks.Clear();
            _waitingQueue.Clear();
            _completedPaths.Clear();
            _activeCount = 0;
            _preparedDependencies.Clear();
            _scriptDependencies.Clear();
        }

        private void PrepareScriptDependencies(string path)
        {
            if (!_preparedDependencies.Add(path)) return;
            if (path.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
            {
                // Register C# exports on the scene thread before a worker constructs
                // resources that use them. Keep scripts alive for this loader session.
                var script = ResourceLoader.Load<Script>(path);
                if (script != null)
                {
                    using var properties = (Godot.Collections.Array)script.GetScriptPropertyList();
                    _scriptDependencies.Add(script);
                }
                return;
            }
            string extension = path.GetExtension().ToLowerInvariant();
            if (extension is not ("tscn" or "tres" or "res")) return;
            foreach (string dependency in ResourceLoader.GetDependencies(path))
            {
                // With UIDs Godot returns uid::::fallback_path; older assets return paths.
                int separator = dependency.LastIndexOf("::", System.StringComparison.Ordinal);
                string source = separator >= 0 ? dependency[(separator + 2)..] : dependency;
                PrepareScriptDependencies(source);
            }
        }

        private void StartTask(ResourceLoadTask task, string typeHint = "")
        {
            PrepareScriptDependencies(task.Path);
            // C# script-backed typed arrays must finish dependency registration in order.
            // The request remains asynchronous; nested dependency workers can race script registration.
            var err = ResourceLoader.LoadThreadedRequest(task.Path, typeHint, useSubThreads: false);
            if (err != Error.Ok)
            {
                Debugger.Error($"[ResourceLoader] LoadThreadedRequest failed for '{task.Path}': {err}");
                _allTasks.Remove(task.Path);
                task.MarkFailed($"LoadThreadedRequest error: {err}");
                return;
            }

            task.MarkStarted();
            _activeCount++;
        }

        private void OnTaskCompleted()
        {
            if (_activeCount > 0)
                _activeCount--;
        }
    }
}
