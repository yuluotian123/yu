using Godot;

namespace Framework
{
    /// <summary>
    /// 资源缓存接口。
    /// 实现类负责资源的存取与淘汰策略，不直接管理资源生命周期（由 Godot 引用计数决定释放时机）。
    /// </summary>
    public interface IResourceCache
    {
        /// <summary>
        /// 当前缓存中的资源数量。
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 尝试从缓存中获取资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="resource">获取到的资源，失败时为 null。</param>
        /// <returns>是否命中缓存。</returns>
        bool TryGet(string path, out Resource resource);

        /// <summary>
        /// 将资源写入缓存。若路径已存在则覆盖。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="resource">要缓存的资源。</param>
        void Set(string path, Resource resource);

        /// <summary>
        /// 从缓存中移除指定路径的资源。
        /// 移除后若无其他引用，Godot 自身引用计数归零时会自动释放。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>是否成功移除。</returns>
        bool Remove(string path);

        /// <summary>
        /// 判断指定路径的资源是否在缓存中。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>是否已缓存。</returns>
        bool Contains(string path);

        /// <summary>
        /// 清空缓存中所有资源引用。
        /// </summary>
        void Clear();
    }
}
