using System;

namespace Framework
{
    /// <summary>
    /// 对象池基类（非泛型）。
    /// <para>供 <see cref="ObjectPoolModule"/> 以统一方式管理不同类型的对象池，
    /// 无需在管理层引入泛型参数。</para>
    /// </summary>
    public abstract class ObjectPoolBase
    {
        /// <summary>
        /// 获取对象池名称。
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// 获取池化对象的实际类型。
        /// </summary>
        public abstract Type ItemType { get; }

        /// <summary>
        /// 获取当前池中闲置对象的数量。
        /// </summary>
        public abstract int Count { get; }

        /// <summary>
        /// 获取或设置对象池容量上限。
        /// </summary>
        public abstract int Capacity { get; set; }

        /// <summary>
        /// 获取或设置是否允许超容量扩容。
        /// </summary>
        public abstract bool AllowOverflow { get; set; }

        /// <summary>
        /// 获取或设置自动释放间隔（秒）。小于等于 0 表示禁用。
        /// </summary>
        public abstract float AutoReleaseInterval { get; set; }

        /// <summary>
        /// 立即释放池中所有空闲对象。
        /// </summary>
        public abstract void ReleaseAllUnused();

        /// <summary>
        /// 每帧驱动自动释放计时。由 <see cref="ObjectPoolModule"/> 在 Process 中调用。
        /// </summary>
        internal abstract void Process(double elapseSeconds, double realElapseSeconds);

        /// <summary>
        /// 关闭并清理对象池，释放所有引用。由 <see cref="ObjectPoolModule"/> 在 Shutdown 或销毁时调用。
        /// </summary>
        internal abstract void Shutdown();
    }
}
