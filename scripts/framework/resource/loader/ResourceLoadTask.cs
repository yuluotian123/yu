using System;
using System.Collections.Generic;
using Godot;

namespace Framework
{
    /// <summary>
    /// Request wrapper that allows per-handle deactivation.
    /// </summary>
    internal sealed class LoadRequest
    {
        public ResourceHandleBase Handle { get; }
        public bool IsActive { get; private set; } = true;

        public LoadRequest(ResourceHandleBase handle)
        {
            Handle = handle;
        }

        public void Deactivate() => IsActive = false;
    }

    /// <summary>
    /// Single threaded-load task for a resource path.
    /// </summary>
    public sealed class ResourceLoadTask
    {
        private readonly Godot.Collections.Array _progressBuffer = new();
        private readonly Action _onStartedCompleted;
        private readonly List<LoadRequest> _requests = new();

        public string Path { get; }
        public bool IsDone { get; private set; }
        public bool IsStarted { get; private set; }
        public float Progress { get; private set; }

        public ResourceLoadTask(string path, Action onStartedCompleted = null)
        {
            Path = path;
            _onStartedCompleted = onStartedCompleted;
        }

        internal void AddHandle(ResourceHandleBase handle)
        {
            var request = new LoadRequest(handle);
            _requests.Add(request);
            handle.ActiveRequest = request;
        }

        internal void MarkStarted() => IsStarted = true;

        internal void MarkFailed(string error) => Complete(null, error, null);

        internal ResourceLoadTaskProfilerEntry GetProfilerEntry()
        {
            var activeRequestCount = 0;
            foreach (var request in _requests)
            {
                if (request.IsActive)
                    activeRequestCount++;
            }

            return new ResourceLoadTaskProfilerEntry
            {
                Path = Path,
                IsStarted = IsStarted,
                IsDone = IsDone,
                Progress = Progress,
                RequestCount = _requests.Count,
                ActiveRequestCount = activeRequestCount,
            };
        }

        internal void CancelAll(string reason = null)
        {
            if (IsDone)
                return;

            IsDone = true;

            if (IsStarted)
                _onStartedCompleted?.Invoke();

            foreach (var request in _requests)
            {
                request.Handle.ActiveRequest = null;
                if (!request.IsActive)
                    continue;

                request.Handle.SetCancelledInternal();
            }

            _requests.Clear();
        }

        public bool Poll(IResourceCache cache, bool enableLog = false)
        {
            if (IsDone)
                return true;
            if (!IsStarted)
                return false;

            _progressBuffer.Clear();
            var status = ResourceLoader.LoadThreadedGetStatus(Path, _progressBuffer);

            if (_progressBuffer.Count > 0)
            {
                Progress = (float)_progressBuffer[0];
                foreach (var request in _requests)
                {
                    if (request.IsActive)
                        request.Handle.UpdateProgressInternal(Progress);
                }
            }

            switch (status)
            {
                case ResourceLoader.ThreadLoadStatus.Loaded:
                    var resource = ResourceLoader.LoadThreadedGet(Path);
                    if (resource != null && cache != null && !cache.Contains(Path))
                    {
                        cache.Set(Path, resource);
                        if (enableLog)
                            Debugger.Info($"[ResourceLoadTask] Cached after async load: '{Path}'");
                    }

                    Complete(resource, null, cache);
                    return true;

                case ResourceLoader.ThreadLoadStatus.Failed:
                    Complete(null, $"Godot ThreadLoad failed for '{Path}'.", cache);
                    return true;

                case ResourceLoader.ThreadLoadStatus.InvalidResource:
                    Complete(null, $"Invalid resource path '{Path}'.", cache);
                    return true;

                default:
                    return false;
            }
        }

        private void Complete(Resource resource, string error, IResourceCache cache)
        {
            if (IsDone)
                return;

            IsDone = true;

            if (IsStarted)
                _onStartedCompleted?.Invoke();

            foreach (var request in _requests)
            {
                request.Handle.ActiveRequest = null;
                if (!request.IsActive)
                    continue;

                if (resource != null && string.IsNullOrEmpty(error))
                {
                    request.Handle.SetSucceedInternal(resource);
                    if (request.Handle.IsValid)
                    {
                        cache?.Acquire(Path);
                        request.Handle.MarkReferenceAcquiredInternal();
                    }
                }
                else
                {
                    request.Handle.SetFailedInternal(error ?? "Load failed.");
                }
            }

            _requests.Clear();
        }
    }
}
