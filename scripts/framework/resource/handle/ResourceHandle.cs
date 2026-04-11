using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace Framework
{
    /// <summary>
    /// Generic resource handle state.
    /// </summary>
    public enum ResourceHandleStatus
    {
        None,
        Loading,
        Succeed,
        Failed,
        Cancelled,
        Released,
    }

    /// <summary>
    /// Non-generic resource handle base.
    /// </summary>
    public abstract class ResourceHandleBase : IDisposable
    {
        public string Path { get; }
        public string RequestedTypeName { get; }
        public ResourceHandleStatus Status { get; internal set; }
        public float Progress { get; internal set; }

        public bool IsDone => Status == ResourceHandleStatus.Succeed
                           || Status == ResourceHandleStatus.Failed
                           || Status == ResourceHandleStatus.Cancelled
                           || Status == ResourceHandleStatus.Released;

        public bool IsValid => Status == ResourceHandleStatus.Succeed;
        public string Error { get; internal set; }

        /// <summary>
        /// The in-flight request bound to this handle while it is loading.
        /// </summary>
        internal LoadRequest ActiveRequest { get; set; }

        /// <summary>
        /// True only after the framework ref-count has been acquired for this handle.
        /// </summary>
        internal bool OwnsReference { get; private set; }
        internal int ProfilerId { get; set; }

        protected ResourceHandleBase(string path, string requestedTypeName)
        {
            Path = path;
            RequestedTypeName = requestedTypeName;
            Status = ResourceHandleStatus.None;
            Progress = 0f;
            Error = string.Empty;
        }

        public abstract void Release();

        public void Dispose()
        {
            Release();
        }

        internal void UpdateProgressInternal(float progress) => Progress = progress;
        internal void MarkReferenceAcquiredInternal() => OwnsReference = true;
        internal void ClearReferenceAcquiredInternal() => OwnsReference = false;
        internal abstract void SetSucceedInternal(Resource resource);
        internal abstract void SetFailedInternal(string error);
        internal abstract void SetCancelledInternal();
    }

    /// <summary>
    /// Generic resource handle for non-scene resources.
    /// </summary>
    public sealed class ResourceHandle<T> : ResourceHandleBase where T : Resource
    {
        private T _asset;
        private Action<ResourceHandle<T>> _onCompleted;
        private ResourceModule _resourceModule;
        private TaskCompletionSource<ResourceHandle<T>> _tcs;
        private CancellationTokenRegistration _cancellationRegistration;

        public T Asset => _asset;

        public Task<ResourceHandle<T>> Task
        {
            get
            {
                if (_tcs == null)
                {
                    _tcs = new TaskCompletionSource<ResourceHandle<T>>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    if (IsDone)
                        _tcs.TrySetResult(this);
                }

                return _tcs.Task;
            }
        }

        internal ResourceHandle(string path, ResourceModule resourceModule) : base(path, typeof(T).Name)
        {
            Status = ResourceHandleStatus.Loading;
            _resourceModule = resourceModule;
        }

        public ResourceHandle<T> OnCompleted(Action<ResourceHandle<T>> callback)
        {
            if (IsDone)
                callback?.Invoke(this);
            else
                _onCompleted += callback;
            return this;
        }

        /// <summary>
        /// Queue cancellation onto the module's main-thread processing path.
        /// Rebinding a token replaces the previous registration.
        /// </summary>
        public ResourceHandle<T> WithCancellation(CancellationToken ct)
        {
            if (!ct.CanBeCanceled || IsDone || _resourceModule == null)
                return this;

            _cancellationRegistration.Dispose();

            if (ct.IsCancellationRequested)
            {
                _resourceModule.RequestCancel(this);
                return this;
            }

            _cancellationRegistration = ct.Register(() => _resourceModule?.RequestCancel(this));
            return this;
        }

        public override void Release()
        {
            if (Status == ResourceHandleStatus.Released)
                return;

            var module = _resourceModule;
            _resourceModule = null;

            var ownsReference = OwnsReference;
            ClearReferenceAcquiredInternal();

            if (ownsReference)
                module?.ReleaseAsset(Path);

            CompleteInternal(
                ResourceHandleStatus.Released,
                asset: null,
                error: Error,
                deactivateRequest: true);
        }

        internal override void SetSucceedInternal(Resource resource)
        {
            if (IsDone || _resourceModule == null)
                return;

            _asset = resource as T;
            if (_asset != null)
            {
                CompleteInternal(
                    ResourceHandleStatus.Succeed,
                    _asset,
                    error: Error,
                    deactivateRequest: false);
            }
            else
            {
                CompleteInternal(
                    ResourceHandleStatus.Failed,
                    asset: null,
                    error: $"Resource at '{Path}' cannot be cast to '{typeof(T).Name}'. Actual type: '{resource?.GetType().Name}'.",
                    deactivateRequest: false);
            }
        }

        internal override void SetFailedInternal(string error)
        {
            if (IsDone || _resourceModule == null)
                return;

            CompleteInternal(
                ResourceHandleStatus.Failed,
                asset: null,
                error: error,
                deactivateRequest: false);
        }

        internal override void SetCancelledInternal()
        {
            if (Status == ResourceHandleStatus.Released || IsDone)
                return;

            CompleteInternal(
                ResourceHandleStatus.Cancelled,
                asset: null,
                error: Error,
                deactivateRequest: true);
        }

        private void ClearCancellationRegistration()
        {
            _cancellationRegistration.Dispose();
            _cancellationRegistration = default;
        }

        private void CompleteInternal(ResourceHandleStatus status, T asset, string error, bool deactivateRequest)
        {
            if (deactivateRequest)
                ActiveRequest?.Deactivate();

            ActiveRequest = null;
            ClearCancellationRegistration();
            _asset = asset;
            Error = error;
            Progress = 1f;
            Status = status;

            _onCompleted?.Invoke(this);
            _onCompleted = null;
            _tcs?.TrySetResult(this);
        }
    }
}
