using System;
using Godot;

namespace Framework
{
    /// <summary>
    /// 资源管理模块接口。
    /// 通过 <see cref="ModuleSystem.GetModule{T}"/> 获取实例。
    /// <para>
    /// 所有加载接口均返回 <see cref="ResourceHandle{T}"/>，每个有效句柄持有一个框架引用计数。
    /// 使用完毕后必须调用 <see cref="ResourceHandle{T}.Release"/> 释放引用，
    /// 引用计数归零后资源可被 LRU 缓存淘汰。
    /// </para>
    /// <example>
    /// <code>
    /// var res = ModuleSystem.GetModule&lt;IResourceModule&gt;();
    ///
    /// // 同步加载（返回句柄，引用计数 +1）
    /// var handle = res.LoadAsset&lt;Texture2D&gt;("res://assets/icon.png");
    /// if (handle.IsValid) { /* 使用 handle.Asset */ }
    /// handle.Release(); // 引用计数 -1
    ///
    /// // 异步加载（返回句柄，加载成功后引用计数 +1）
    /// res.LoadAssetAsync&lt;PackedScene&gt;("res://scenes/level.tscn")
    ///    .OnCompleted(h =&gt; {
    ///        if (h.IsValid) AddChild(h.Asset.Instantiate());
    ///        // 不再需要时 h.Release();
    ///    });
    /// </code>
    /// </example>
    /// </summary>
    public interface IResourceModule
    {
        /// <summary>
        /// 同步加载资源，返回句柄。会优先命中缓存。
        /// 每次调用成功后框架引用计数 +1，使用完毕后需调用 <see cref="ResourceHandle{T}.Release"/> 释放。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="path">资源路径（res:// 格式）。</param>
        /// <returns>资源句柄。失败时 <see cref="ResourceHandleBase.IsValid"/> 为 false。</returns>
        ResourceHandle<T> LoadAsset<T>(string path) where T : Resource;

        /// <summary>
        /// 发起异步加载请求，立即返回句柄。
        /// 通过句柄的 <see cref="ResourceHandle{T}.OnCompleted"/> 注册回调，
        /// 或在每帧检查 <see cref="ResourceHandleBase.IsDone"/>。
        /// 加载成功后框架引用计数 +1，使用完毕后需调用 <see cref="ResourceHandle{T}.Release"/> 释放。
        /// <remarks>对同一路径的多次请求会共用同一个后台加载任务，不会重复发起。每个成功句柄各自持有一个引用。</remarks>
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="path">资源路径（res:// 格式）。</param>
        /// <returns>资源句柄。</returns>
        ResourceHandle<T> LoadAssetAsync<T>(string path) where T : Resource;

        /// <summary>
        /// 强制从缓存中移除指定路径的资源，无视引用计数。
        /// 适用于场景切换等明确知道资源不再需要的场景。
        /// <remarks>若仍有 Handle 持有该资源引用，其 Asset 不会自动置 null，
        /// 但资源可能在 Godot 引用计数归零后被引擎回收。</remarks>
        /// </summary>
        /// <param name="path">资源路径。</param>
        void ForceUnloadAsset(string path);

        /// <summary>
        /// 清空所有缓存资源引用（强制，无视引用计数）。
        /// </summary>
        void UnloadAllAssets();

        /// <summary>
        /// 判断指定路径的资源是否已在缓存中。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>是否已缓存。</returns>
        bool HasAsset(string path);

        /// <summary>
        /// 获取当前缓存资源数量。
        /// </summary>
        int CacheCount { get; }

        /// <summary>
        /// 获取指定资源的框架层引用计数。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>引用计数，不存在则为 0。</returns>
        int GetRefCount(string path);

        /// <summary>
        /// 设置自定义加载器（替换默认的 <see cref="GodotResourceLoader"/>）。
        /// 必须在首次加载资源之前调用。
        /// </summary>
        /// <param name="loader">自定义加载器实现。</param>
        void SetLoader(IResourceLoader loader);

        /// <summary>
        /// 设置自定义缓存（替换默认的 <see cref="ResourceCache"/>）。
        /// 必须在首次加载资源之前调用。
        /// </summary>
        /// <param name="cache">自定义缓存实现。</param>
        void SetCache(IResourceCache cache);

        // ── 内部方法（供 Handle.Release 调用，不建议外部直接使用）──

        /// <summary>
        /// 减少指定路径资源的框架引用计数（-1）。
        /// 由 <see cref="ResourceHandle{T}.Release"/> 内部调用。
        /// 引用计数归零后资源变为"可淘汰"状态，在缓存满时被 LRU 策略移除。
        /// </summary>
        /// <param name="path">资源路径。</param>
        void ReleaseAsset(string path);
    }
}
