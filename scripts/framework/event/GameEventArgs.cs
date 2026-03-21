namespace Framework
{
    /// <summary>
    /// 游戏事件参数基类。
    /// <para>所有自定义事件参数均需继承此类，并实现 <see cref="Id"/> 属性和 <see cref="Clear"/> 方法。</para>
    /// <para>
    /// 本类实现了 <see cref="IObjectPoolItem"/> 接口，子类可通过 <see cref="IObjectPoolModule"/>
    /// 进行对象池管理，以减少高频事件场景下的 GC 压力。对于低频事件也可直接 <c>new</c> 使用。
    /// </para>
    /// <para>
    /// 事件 ID 统一通过 <see cref="GetEventId{T}"/> 获取，确保同类型事件 ID 全局唯一且稳定。
    /// </para>
    /// <example>
    /// <code>
    /// // 1. 定义事件参数
    /// public sealed class PlayerHitEventArgs : GameEventArgs
    /// {
    ///     public override int Id => GameEventArgs.GetEventId&lt;PlayerHitEventArgs&gt;();
    ///     public int Damage { get; private set; }
    ///
    ///     public static PlayerHitEventArgs Create(int damage)
    ///     {
    ///         var e = new PlayerHitEventArgs();
    ///         e.Damage = damage;
    ///         return e;
    ///     }
    ///     public override void Clear() => Damage = 0;
    /// }
    ///
    /// // 2. 订阅事件
    /// eventModule.Subscribe(GameEventArgs.GetEventId&lt;PlayerHitEventArgs&gt;(), OnPlayerHit);
    ///
    /// // 3. 触发事件
    /// eventModule.Fire(this, PlayerHitEventArgs.Create(10));
    /// </code>
    /// </example>
    /// </summary>
    public abstract class GameEventArgs : IObjectPoolItem
    {
        /// <summary>
        /// 获取当前事件参数的事件 ID。
        /// <para>推荐实现为：<c>public override int Id => GameEventArgs.GetEventId&lt;YourEventArgs&gt;();</c></para>
        /// </summary>
        public abstract int Id { get; }

        /// <summary>
        /// 清理事件参数中的所有数据。
        /// <para>在对象归还对象池时自动调用，子类应将所有字段重置为默认值。</para>
        /// </summary>
        public abstract void Clear();

        /// <summary>
        /// 从对象池中取出时调用，重置对象状态。
        /// </summary>
        void IObjectPoolItem.OnSpawn() => Clear();

        /// <summary>
        /// 回收到对象池时调用，清理对象状态。
        /// </summary>
        void IObjectPoolItem.OnRecycle() => Clear();

        /// <summary>
        /// 获取指定事件参数类型对应的唯一事件 ID。
        /// <para>
        /// 利用泛型静态字段的唯一性，每个类型只计算一次，天然线程安全且无字典查询开销。
        /// 同一 <typeparamref name="T"/> 在整个 AppDomain 生命周期内返回相同的值。
        /// </para>
        /// </summary>
        /// <typeparam name="T">事件参数类型，必须继承自 <see cref="GameEventArgs"/>。</typeparam>
        /// <returns>该类型对应的唯一事件 ID。</returns>
        public static int GetEventId<T>() where T : GameEventArgs
        {
            return EventIdCache<T>.Id;
        }

        // 利用泛型静态字段的唯一性实现零开销的类型→ID映射
        private static class EventIdCache<T> where T : GameEventArgs
        {
            // CLR 保证每个封闭泛型类型只初始化一次，static readonly 天然线程安全
            public static readonly int Id = typeof(T).GetHashCode();
        }
    }
}
