using System.Collections.Generic;
using Godot;

namespace Framework
{
    /// <summary>
    /// 封装 Godot 原生 <see cref="ResourceLoader.LoadThreadedRequest"/> 的单次异步加载任务。
    /// 在每帧 Process 中调用 <see cref="Poll"/> 轮询加载状态。
    /// </summary>
    public sealed class ResourceLoadTask
    {
        private readonly Godot.Collections.Array _progressBuffer = new Godot.Collections.Array();

        /// <summary>
        /// 资源路径。
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// 挂载在此任务上的所有待完成句柄（同一路径可能被多处同时请求）。
        /// </summary>
        public List<ResourceHandleBase> Handles { get; } = new List<ResourceHandleBase>();

        /// <summary>
        /// 任务是否已结束（成功或失败）。
        /// </summary>
        public bool IsDone { get; private set; }

        /// <summary>
        /// 当前进度（0~1）。
        /// </summary>
        public float Progress { get; private set; }

        public ResourceLoadTask(string path)
        {
            Path = path;
        }

        /// <summary>
        /// 立即将任务标记为失败（在发起请求时就已知失败，无需轮询）。
        /// </summary>
        /// <param name="error">错误信息。</param>
        internal void MarkFailed(string error)
        {
            Complete(null, error);
        }

        /// <summary>
        /// 每帧轮询一次 Godot 后台加载状态。
        /// 加载成功时先写入 <paramref name="cache"/>，再通知所有句柄，确保缓存与句柄持有同一份资源引用。
        /// </summary>
        /// <param name="cache">资源缓存，用于在加载完成时写入资源。</param>
        /// <param name="enableLog">是否打印日志。</param>
        /// <returns>如果任务已结束（成功或失败）则返回 true。</returns>
        public bool Poll(IResourceCache cache, bool enableLog = false)
        {
            if (IsDone) return true;

            _progressBuffer.Clear();
            var status = ResourceLoader.LoadThreadedGetStatus(Path, _progressBuffer);

            if (_progressBuffer.Count > 0)
            {
                Progress = (float)_progressBuffer[0];
                // 同步进度到所有句柄
                foreach (var handle in Handles)
                    handle.UpdateProgressInternal(Progress);
            }

            switch (status)
            {
                case ResourceLoader.ThreadLoadStatus.Loaded:
                    // 唯一一次 LoadThreadedGet，先写缓存再派发
                    var resource = ResourceLoader.LoadThreadedGet(Path);
                    if (resource != null && cache != null && !cache.Contains(Path))
                    {
                        cache.Set(Path, resource);
                        if (enableLog)
                            Debugger.Info($"[ResourceLoadTask] Cached after async load: '{Path}'");
                    }
                    Complete(resource, null);
                    return true;

                case ResourceLoader.ThreadLoadStatus.Failed:
                    Complete(null, $"Godot ThreadLoad failed for '{Path}'.");
                    return true;

                case ResourceLoader.ThreadLoadStatus.InvalidResource:
                    Complete(null, $"Invalid resource path '{Path}'.");
                    return true;

                default:
                    // InProgress — 继续等待
                    return false;
            }
        }

        /// <summary>
        /// 完成任务，通知所有挂载的句柄。
        /// </summary>
        private void Complete(Resource resource, string error)
        {
            IsDone = true;
            foreach (var handle in Handles)
            {
                if (resource != null && string.IsNullOrEmpty(error))
                    handle.SetSucceedInternal(resource);
                else
                    handle.SetFailedInternal(error ?? "Load failed.");
            }
            Handles.Clear();
        }
    }
}
