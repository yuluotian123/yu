using System;
using Godot;

namespace Framework
{
    /// <summary>
    /// 资源管理模块接口。
    /// 通过 <see cref="ModuleSystem.GetModule{T}"/> 获取实例。
    /// <example>
    /// <code>
    /// var res = ModuleSystem.GetModule&lt;IResourceModule&gt;();
    ///
    /// // 同步加载
    /// var texture = res.LoadAsset&lt;Texture2D&gt;("res://assets/icon.png");
    ///
    /// // 异步加载（链式回调）
    /// res.LoadAssetAsync&lt;PackedScene&gt;("res://scenes/level.tscn")
    ///    .OnCompleted(handle => {
    ///        if (handle.IsValid) AddChild(handle.Asset.Instantiate());
    ///    });
    /// </code>
    /// </example>
    /// </summary>
    public interface IResourceModule
    {
        /// <summary>
        /// 同步加载资源。会优先命中缓存。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="path">资源路径（res:// 格式）。</param>
        /// <returns>加载到的资源，失败时返回 null。</returns>
        T LoadAsset<T>(string path) where T : Resource;

        /// <summary>
        /// 发起异步加载请求，立即返回句柄。
        /// 通过句柄的 <see cref="ResourceHandle{T}.OnCompleted"/> 注册回调，
        /// 或在每帧检查 <see cref="ResourceHandleBase.IsDone"/>。
        /// <remarks>对同一路径的多次请求会共用同一个后台加载任务，不会重复发起。</remarks>
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="path">资源路径（res:// 格式）。</param>
        /// <returns>资源句柄。</returns>
        ResourceHandle<T> LoadAssetAsync<T>(string path) where T : Resource;

        /// <summary>
        /// 从缓存中移除指定路径的资源。
        /// 若资源还有其他引用者（Godot 引用计数 > 1），仅从缓存字典中移除，不强制释放。
        /// </summary>
        /// <param name="path">资源路径。</param>
        void UnloadAsset(string path);

        /// <summary>
        /// 清空所有缓存资源引用。
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
    }
}
