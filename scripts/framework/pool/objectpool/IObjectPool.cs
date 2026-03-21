using System;

namespace Framework
{
    /// <summary>
    /// 纯 C# 对象池接口。
    /// <para>通过 <see cref="IObjectPoolModule.GetObjectPool{T}"/> 或
    /// <see cref="IObjectPoolModule.CreateObjectPool{T}"/> 获取实例。</para>
    /// <example>
    /// <code>
    /// // 创建容量为 50、每 30 秒自动释放一次空闲对象的子弹池
    /// var pool = poolModule.CreateObjectPool&lt;Bullet&gt;(capacity: 50, autoReleaseInterval: 30f);
    ///
    /// var bullet = pool.Spawn();
    /// // ...使用 bullet...
    /// pool.Recycle(bullet);
    /// </code>
    /// </example>
    /// </summary>
    public interface IObjectPool<T> where T : class, IObjectPoolItem
    {
        /// <summary>
        /// 获取对象池名称。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 获取池化对象的类型。
        /// </summary>
        Type ItemType { get; }

        /// <summary>
        /// 获取当前池中闲置对象的数量。
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 获取或设置对象池容量上限。
        /// <para>当回收的对象数量超过此上限时，多余的对象将被丢弃（或在 <see cref="AllowOverflow"/> 为 true 时扩容）。</para>
        /// </summary>
        int Capacity { get; set; }

        /// <summary>
        /// 获取或设置是否允许超出容量时自动扩容（即不丢弃回收对象）。
        /// <para>默认为 false，超容量时回收的对象直接丢弃由 GC 回收。</para>
        /// </summary>
        bool AllowOverflow { get; set; }

        /// <summary>
        /// 获取或设置自动释放空闲对象的间隔（秒）。
        /// <para>小于等于 0 时禁用自动释放。</para>
        /// </summary>
        float AutoReleaseInterval { get; set; }

        /// <summary>
        /// 从对象池中取出一个对象。
        /// <para>若池中有空闲对象则直接取出，否则创建新实例。</para>
        /// 取出前会自动调用 <see cref="IObjectPoolItem.OnSpawn"/>。
        /// </summary>
        /// <returns>取出的对象实例。</returns>
        T Spawn();

        /// <summary>
        /// 将对象回收到对象池。
        /// <para>回收前会自动调用 <see cref="IObjectPoolItem.OnRecycle"/>。</para>
        /// <para>若池已满且 <see cref="AllowOverflow"/> 为 false，对象将被丢弃。</para>
        /// </summary>
        /// <param name="item">要回收的对象。</param>
        void Recycle(T item);

        /// <summary>
        /// 立即释放池中所有闲置对象。
        /// </summary>
        void ReleaseAllUnused();
    }
}
