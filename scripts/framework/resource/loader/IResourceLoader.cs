using Godot;

namespace Framework
{
    /// <summary>
    /// 资源加载器接口（策略模式）。
    /// 不同实现可对应 Godot 内置加载、热更新加载等方式。
    /// </summary>
    public interface IResourceLoader
    {
        /// <summary>
        /// 同步加载资源。
        /// </summary>
        /// <param name="path">资源路径（res:// 路径）。</param>
        /// <returns>加载到的资源，失败时返回 null。</returns>
        Resource LoadSync(string path);

        /// <summary>
        /// 发起异步加载请求，返回对应的加载任务。
        /// 调用方需在每帧调用 <see cref="ResourceLoadTask.Poll"/> 轮询任务状态。
        /// </summary>
        /// <param name="path">资源路径（res:// 路径）。</param>
        /// <param name="typeHint">资源类型提示，可为空字符串。</param>
        /// <returns>封装了 Godot 异步状态的加载任务。</returns>
        ResourceLoadTask RequestAsync(string path, string typeHint = "");

        /// <summary>
        /// 判断指定路径的资源是否存在。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>资源是否存在。</returns>
        bool Exists(string path);
    }
}
