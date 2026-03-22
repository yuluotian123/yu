using System.Collections.Generic;
using System.Threading;

namespace Framework
{
    /// <summary>
    /// 事件 ID 生成器。支持两种方式获取全局唯一、单调递增的事件 ID：
    /// <list type="number">
    ///   <item>
    ///     <b>泛型哑类型（编译期）</b>：<see cref="Get{T}"/>
    ///     <para>利用 CLR 泛型静态字段唯一性，零运行时查找开销，适合在静态字段初始化时使用。</para>
    ///   </item>
    ///   <item>
    ///     <b>字符串名称（运行期）</b>：<see cref="Get(string)"/>
    ///     <para>
    ///     对齐 TEngine EventId.Get(string) 实现：
    ///     同一字符串始终返回相同 ID，内部用字典缓存，首次注册时分配新 ID，线程安全。
    ///     适合动态事件名、配置驱动场景。
    ///     </para>
    ///   </item>
    /// </list>
    /// <example>
    /// <code>
    /// // ① 泛型方式——推荐用于已知事件集
    /// public static class GameEvents
    /// {
    ///     private struct NoticeTag { }
    ///     private struct PlayerHitTag { }
    ///
    ///     public static readonly int GameNotice = EventId.Get&lt;NoticeTag&gt;();
    ///     public static readonly int PlayerHit  = EventId.Get&lt;PlayerHitTag&gt;();
    /// }
    ///
    /// // ② 字符串方式——适合动态/配置驱动
    /// int id = EventId.Get("game.notice");   // 首次调用分配新 ID
    /// int id2 = EventId.Get("game.notice");  // 返回同一个 ID
    /// </code>
    /// </example>
    /// </summary>
    public static class EventId
    {
        private static int _counter = 0;

        // 字符串 → ID 映射表，用普通 Dictionary + lock 保证线程安全（避免引入 ConcurrentDictionary 依赖）
        private static readonly Dictionary<string, int> _nameMap = new Dictionary<string, int>();
        private static readonly object _lock = new object();

        // ------------------------------------------------------------------ 泛型方式

        /// <summary>
        /// 获取类型 <typeparamref name="T"/> 对应的唯一事件 ID。
        /// <para>同一 <typeparamref name="T"/> 在整个 AppDomain 生命周期内始终返回相同的值。</para>
        /// </summary>
        /// <typeparam name="T">用于区分事件的哑类型，推荐使用专属的 struct 或 class。</typeparam>
        public static int Get<T>() => EventIdCache<T>.Id;

        // CLR 保证每个封闭泛型类型只初始化一次，Interlocked.Increment 保证线程安全
        private static class EventIdCache<T>
        {
            public static readonly int Id = Interlocked.Increment(ref _counter);
        }

        // ------------------------------------------------------------------ 字符串方式

        /// <summary>
        /// 获取字符串 <paramref name="eventName"/> 对应的唯一事件 ID。
        /// <para>
        /// 对齐 TEngine <c>EventId.Get(string)</c>：
        /// 同一字符串始终返回相同的 ID；首次调用时分配新 ID；线程安全。
        /// </para>
        /// </summary>
        /// <param name="eventName">事件名称，区分大小写，不可为 null 或空字符串。</param>
        /// <returns>全局唯一、单调递增的事件 ID。</returns>
        public static int Get(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                Debugger.Error("[EventId] eventName 不能为 null 或空字符串。");
                return -1;
            }

            lock (_lock)
            {
                if (_nameMap.TryGetValue(eventName, out int id))
                    return id;

                id = Interlocked.Increment(ref _counter);
                _nameMap[eventName] = id;
                return id;
            }
        }

        /// <summary>
        /// 查询字符串名称是否已被注册（不会创建新 ID）。
        /// </summary>
        public static bool IsRegistered(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return false;
            lock (_lock) { return _nameMap.ContainsKey(eventName); }
        }
    }
}
