using System;
using Godot;

namespace Framework
{
    /// <summary>
    /// 对象池模块接口。
    /// <para>统一管理纯 C# 对象池（<see cref="IObjectPool{T}"/>）和 Godot Node 对象池（<see cref="INodePool"/>）。</para>
    /// <para>通过 <see cref="ModuleSystem.GetModule{T}"/> 以本接口获取实例。</para>
    /// <example>
    /// <code>
    /// var poolModule = ModuleSystem.GetModule&lt;IObjectPoolModule&gt;();
    ///
    /// // ---- 纯 C# 对象池 ----
    /// var bulletPool = poolModule.CreateObjectPool&lt;Bullet&gt;(capacity: 50);
    /// var bullet = bulletPool.Spawn();
    /// bulletPool.Recycle(bullet);
    ///
    /// // ---- Node 对象池（同步） ----
    /// var nodePool = poolModule.CreateNodePool("res://scenes/bullet.tscn", parentNode);
    /// var node = nodePool.Spawn();
    /// nodePool.Recycle(node);
    ///
    /// // ---- Node 对象池（异步，PackedScene 后台加载） ----
    /// poolModule.CreateNodePoolAsync("res://scenes/bullet.tscn", parentNode,
    ///     onCompleted: pool => { var node = pool?.Spawn(); });
    /// </code>
    /// </example>
    /// </summary>
    public interface IObjectPoolModule
    {
        /// <summary>
        /// 获取当前所管理的对象池总数（纯 C# 池 + Node 池）。
        /// </summary>
        int Count { get; }

        // ===========================
        // 纯 C# 对象池
        // ===========================

        /// <summary>
        /// 检查是否存在指定类型（和名称）的纯 C# 对象池。
        /// </summary>
        /// <typeparam name="T">池化对象类型。</typeparam>
        /// <param name="name">对象池名称，默认为空字符串。</param>
        bool HasObjectPool<T>(string name = "") where T : class, IObjectPoolItem;

        /// <summary>
        /// 获取指定类型（和名称）的纯 C# 对象池。
        /// </summary>
        /// <typeparam name="T">池化对象类型。</typeparam>
        /// <param name="name">对象池名称，默认为空字符串。</param>
        /// <returns>对应的对象池，不存在时返回 null。</returns>
        IObjectPool<T> GetObjectPool<T>(string name = "") where T : class, IObjectPoolItem;

        /// <summary>
        /// 创建纯 C# 对象池。
        /// </summary>
        /// <typeparam name="T">池化对象类型，必须有无参构造函数并实现 <see cref="IObjectPoolItem"/>。</typeparam>
        /// <param name="name">对象池名称，默认为空字符串（同一类型允许多个不同名的池）。</param>
        /// <param name="capacity">容量上限，默认 32。</param>
        /// <param name="autoReleaseInterval">自动释放空闲对象的间隔（秒），小于等于 0 则禁用，默认 60 秒。</param>
        /// <returns>新建的对象池。</returns>
        IObjectPool<T> CreateObjectPool<T>(string name = "", int capacity = 32, float autoReleaseInterval = 60f)
            where T : class, IObjectPoolItem, new();

        /// <summary>
        /// 销毁指定类型（和名称）的纯 C# 对象池，并释放池中所有对象。
        /// </summary>
        /// <typeparam name="T">池化对象类型。</typeparam>
        /// <param name="name">对象池名称，默认为空字符串。</param>
        /// <returns>是否成功销毁。</returns>
        bool DestroyObjectPool<T>(string name = "") where T : class, IObjectPoolItem;

        // ===========================
        // Node 对象池
        // ===========================

        /// <summary>
        /// 检查是否存在指定场景路径（和名称）的 Node 对象池。
        /// </summary>
        /// <param name="scenePath">PackedScene 资源路径（res:// 格式）。</param>
        /// <param name="name">对象池名称，默认为空字符串。</param>
        bool HasNodePool(string scenePath, string name = "");

        /// <summary>
        /// 获取指定场景路径（和名称）的 Node 对象池。
        /// </summary>
        /// <param name="scenePath">PackedScene 资源路径（res:// 格式）。</param>
        /// <param name="name">对象池名称，默认为空字符串。</param>
        /// <returns>对应的 Node 对象池，不存在时返回 null。</returns>
        INodePool GetNodePool(string scenePath, string name = "");

        /// <summary>
        /// 创建 Node 对象池（同步）。
        /// <para>会通过 <see cref="IResourceModule"/> 同步加载对应的 <see cref="PackedScene"/>。
        /// 若场景文件较大，建议使用 <see cref="CreateNodePoolAsync"/> 避免阻塞主线程。</para>
        /// </summary>
        /// <param name="scenePath">PackedScene 资源路径（res:// 格式）。</param>
        /// <param name="parent">Node 实例的父节点；所有由池管理的 Node 都挂载在此节点下。</param>
        /// <param name="name">对象池名称，默认为空字符串。</param>
        /// <param name="capacity">容量上限，默认 32。</param>
        /// <param name="autoReleaseInterval">自动释放空闲 Node 的间隔（秒），小于等于 0 则禁用，默认 60 秒。</param>
        /// <returns>新建的 Node 对象池。</returns>
        INodePool CreateNodePool(string scenePath, Node parent, string name = "",
            int capacity = 32, float autoReleaseInterval = 60f);

        /// <summary>
        /// 创建 Node 对象池（异步）。
        /// <para>通过 <see cref="IResourceModule.LoadAssetAsync{T}"/> 在后台加载 PackedScene，
        /// 加载完成后自动创建对象池并通过 <paramref name="onCompleted"/> 回调返回。</para>
        /// <para>在 <paramref name="onCompleted"/> 中，若 <paramref name="onCompleted"/> 的参数为 null
        /// 则表示加载失败，可通过日志排查原因。</para>
        /// </summary>
        /// <param name="scenePath">PackedScene 资源路径（res:// 格式）。</param>
        /// <param name="parent">Node 实例的父节点；所有由池管理的 Node 都挂载在此节点下。</param>
        /// <param name="onCompleted">加载完成回调；参数为创建好的 <see cref="INodePool"/>，失败时为 null。</param>
        /// <param name="name">对象池名称，默认为空字符串。</param>
        /// <param name="capacity">容量上限，默认 32。</param>
        /// <param name="autoReleaseInterval">自动释放空闲 Node 的间隔（秒），小于等于 0 则禁用，默认 60 秒。</param>
        void CreateNodePoolAsync(string scenePath, Node parent, Action<INodePool> onCompleted,
            string name = "", int capacity = 32, float autoReleaseInterval = 60f);

        /// <summary>
        /// 销毁指定场景路径（和名称）的 Node 对象池，并 QueueFree 池中所有闲置 Node。
        /// </summary>
        /// <param name="scenePath">PackedScene 资源路径（res:// 格式）。</param>
        /// <param name="name">对象池名称，默认为空字符串。</param>
        /// <returns>是否成功销毁。</returns>
        bool DestroyNodePool(string scenePath, string name = "");

        // ===========================
        // 全局操作
        // ===========================

        /// <summary>
        /// 立即释放所有对象池中的所有空闲对象/Node。
        /// </summary>
        void ReleaseAllUnused();
    }
}
