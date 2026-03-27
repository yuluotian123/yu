using Godot;

namespace Framework
{
    /// <summary>
    /// 资源缓存接口。
    /// 实现类负责资源的存取、引用计数管理与淘汰策略。
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
        /// 写入时引用计数不变（需由调用方显式 Acquire）。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="resource">要缓存的资源。</param>
        void Set(string path, Resource resource);

        /// <summary>
        /// 从缓存中强制移除指定路径的资源，无视引用计数。
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

        /// <summary>
        /// 增加指定资源的框架层引用计数（+1）。
        /// 在 LoadAsset / LoadAssetAsync 成功后由 ResourceModule 调用。
        /// </summary>
        /// <param name="path">资源路径。</param>
        void Acquire(string path);

        /// <summary>
        /// 减少指定资源的框架层引用计数（-1），最低为 0。
        /// 引用计数归零后资源变为"可淘汰"状态（LRU 淘汰时优先移除）。
        /// </summary>
        /// <param name="path">资源路径。</param>
        void Release(string path);

        /// <summary>
        /// 获取指定资源的框架层引用计数。不存在则返回 0。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>引用计数。</returns>
        int GetRefCount(string path);
    }
}
