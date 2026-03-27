using System;
using Godot;

namespace Framework
{
    /// <summary>
    /// 资源句柄状态枚举。
    /// </summary>
    public enum ResourceHandleStatus
    {
        /// <summary>未初始化。</summary>
        None,
        /// <summary>加载中。</summary>
        Loading,
        /// <summary>加载成功。</summary>
        Succeed,
        /// <summary>加载失败。</summary>
        Failed,
    }

    /// <summary>
    /// 资源句柄基类，用于在不知道具体泛型类型时统一操作。
    /// </summary>
    public abstract class ResourceHandleBase
    {
        public string Path { get; }
        public ResourceHandleStatus Status { get; internal set; }
        public float Progress { get; internal set; }
        public bool IsDone => Status == ResourceHandleStatus.Succeed || Status == ResourceHandleStatus.Failed;
        public bool IsValid => Status == ResourceHandleStatus.Succeed;
        public string Error { get; internal set; }

        protected ResourceHandleBase(string path)
        {
            Path = path;
            Status = ResourceHandleStatus.None;
            Progress = 0f;
            Error = string.Empty;
        }

        /// <summary>获取原始 Godot Resource 对象（不带泛型类型）。</summary>
        public abstract Resource GetRawAsset();

        /// <summary>释放句柄（减引用计数），由子类实现。</summary>
        public abstract void Release();

        // ---- 仅供 loader 内部调用 ----
        internal void UpdateProgressInternal(float progress) => Progress = progress;
        internal abstract void SetSucceedInternal(Resource resource);
        internal abstract void SetFailedInternal(string error);
    }

    /// <summary>
    /// 泛型资源句柄，持有具体类型的资源引用和一个框架引用计数。
    /// <remarks>
    /// 每个通过 <see cref="IResourceModule.LoadAsset{T}"/> 或 <see cref="IResourceModule.LoadAssetAsync{T}"/>
    /// 获得的有效句柄都持有一个引用计数。调用 <see cref="Release"/> 会将引用计数 -1，
    /// 引用计数归零后资源可被 LRU 缓存淘汰，之后若无其他引用，Godot 自动释放资源内存。
    /// </remarks>
    /// </summary>
    public sealed class ResourceHandle<T> : ResourceHandleBase where T : Resource
    {
        private T _asset;
        private Action<ResourceHandle<T>> _onCompleted;
        private IResourceModule _resourceModule;

        /// <summary>加载完成后的资源实例。未完成或失败时为 null。</summary>
        public T Asset => _asset;

        internal ResourceHandle(string path, IResourceModule resourceModule) : base(path)
        {
            Status = ResourceHandleStatus.Loading;
            _resourceModule = resourceModule;
        }

        /// <summary>
        /// 注册加载完成回调。若已完成则立即调用。支持链式调用。
        /// </summary>
        public ResourceHandle<T> OnCompleted(Action<ResourceHandle<T>> callback)
        {
            if (IsDone)
                callback?.Invoke(this);
            else
                _onCompleted += callback;
            return this;
        }

        /// <summary>
        /// 释放句柄：减少框架引用计数（-1），并清空句柄内持有的资源引用。
        /// <remarks>
        /// 调用后 <see cref="Asset"/> 将变为 null，<see cref="IsValid"/> 变为 false。
        /// 重复调用 Release 是安全的（幂等），不会多次减少引用计数。
        /// 引用计数归零后，资源变为"可淘汰"状态，在缓存满时被 LRU 淘汰。
        /// </remarks>
        /// </summary>
        public override void Release()
        {
            if (_resourceModule == null) return; // 已释放，幂等

            _resourceModule.ReleaseAsset(Path);
            _asset = null;
            _resourceModule = null;
            _onCompleted = null;
            Status = ResourceHandleStatus.None;
        }

        /// <inheritdoc/>
        internal override void SetSucceedInternal(Resource resource)
        {
            _asset = resource as T;
            if (_asset != null)
            {
                Status = ResourceHandleStatus.Succeed;
                Progress = 1f;
            }
            else
            {
                Status = ResourceHandleStatus.Failed;
                Error = $"Resource at '{Path}' cannot be cast to '{typeof(T).Name}'. Actual type: '{resource?.GetType().Name}'.";
                Progress = 1f;
            }
            _onCompleted?.Invoke(this);
            _onCompleted = null;
        }

        /// <inheritdoc/>
        internal override void SetFailedInternal(string error)
        {
            _asset = null;
            Status = ResourceHandleStatus.Failed;
            Error = error;
            Progress = 1f;
            _onCompleted?.Invoke(this);
            _onCompleted = null;
        }

        /// <inheritdoc/>
        public override Resource GetRawAsset() => _asset;
    }
}
