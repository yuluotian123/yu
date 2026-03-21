using Godot;

namespace Framework
{
    /// <summary>
    /// 基于 Godot 原生 <see cref="ResourceLoader"/> 的加载器实现。
    /// 同步加载使用 <see cref="ResourceLoader.Load"/>，
    /// 异步加载使用 <see cref="ResourceLoader.LoadThreadedRequest"/> + <see cref="ResourceLoadTask.Poll"/>。
    /// </summary>
    public sealed class GodotResourceLoader : IResourceLoader
    {
        /// <inheritdoc/>
        public Resource LoadSync(string path)
        {
            if (!ResourceLoader.Exists(path))
            {
                Debugger.Error($"[ResourceLoader] Resource not found: '{path}'");
                return null;
            }

            var resource = ResourceLoader.Load(path);
            if (resource == null)
            {
                Debugger.Error($"[ResourceLoader] Failed to load resource: '{path}'");
            }
            return resource;
        }

        /// <inheritdoc/>
        public ResourceLoadTask RequestAsync(string path, string typeHint = "")
        {
            var task = new ResourceLoadTask(path);

            var err = ResourceLoader.LoadThreadedRequest(path, typeHint, useSubThreads: true);
            if (err != Error.Ok)
            {
                Debugger.Error($"[ResourceLoader] LoadThreadedRequest failed for '{path}': {err}");
                // 标记为立即失败：在第一次 Poll 时会返回 InvalidResource/Failed 状态
                // 此处直接让任务处于就绪但失败状态
                task.MarkFailed($"LoadThreadedRequest error: {err}");
            }

            return task;
        }

        /// <inheritdoc/>
        public bool Exists(string path)
        {
            return ResourceLoader.Exists(path);
        }
    }
}
