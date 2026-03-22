using System;

namespace Framework
{
    /// <summary>
    /// 游戏事件模块接口。
    /// <para>
    /// 对齐 TEngine EventDispatcher 设计，提供基于事件 ID 的发布-订阅（Pub/Sub）机制。
    /// 通过 <see cref="ModuleSystem.GetModule{T}"/> 以本接口获取实例。
    /// </para>
    /// <para>
    /// 相比旧版（<c>EventHandler&lt;GameEventArgs&gt;</c>）的改进：
    /// <list type="bullet">
    ///   <item>使用泛型 <see cref="Action{T}"/> 委托，直接传参，编译期类型安全，无需继承 GameEventArgs 或强转；</item>
    ///   <item>支持 0~4 个参数的委托，覆盖绝大多数场景；</item>
    ///   <item>所有 <c>Send</c> 均为同步立即派发，脏数据机制保证回调中 Subscribe / Unsubscribe 安全；</item>
    ///   <item>事件 ID 推荐通过 <see cref="EventId.Get(string)"/> 或 <see cref="EventId.Get{T}"/> 生成。</item>
    /// </list>
    /// </para>
    /// <example>
    /// <code>
    /// var ev = ModuleSystem.GetModule&lt;IEventModule&gt;();
    ///
    /// // ① 字符串 ID 方式（推荐）
    /// int id = EventId.Get("game.notice");
    ///
    /// // 订阅
    /// ev.Subscribe&lt;string&gt;(id, OnGameNotice);
    /// void OnGameNotice(string msg) { GD.Print(msg); }
    ///
    /// // 发送
    /// ev.Send&lt;string&gt;(id, "Hello!");
    ///
    /// // 取消订阅（一般在 OnDestroy / _ExitTree 中调用）
    /// ev.Unsubscribe&lt;string&gt;(id, OnGameNotice);
    /// </code>
    /// </example>
    /// </summary>
    public interface IEventModule
    {
        // ------------------------------------------------------------------ 订阅

        /// <summary>订阅无参数事件。重复订阅同一处理器时打 Error 日志并返回 false，不抛异常。</summary>
        bool Subscribe(int eventId, Action handler);

        /// <summary>订阅 1 参数事件。</summary>
        bool Subscribe<T1>(int eventId, Action<T1> handler);

        /// <summary>订阅 2 参数事件。</summary>
        bool Subscribe<T1, T2>(int eventId, Action<T1, T2> handler);

        /// <summary>订阅 3 参数事件。</summary>
        bool Subscribe<T1, T2, T3>(int eventId, Action<T1, T2, T3> handler);

        /// <summary>订阅 4 参数事件。</summary>
        bool Subscribe<T1, T2, T3, T4>(int eventId, Action<T1, T2, T3, T4> handler);

        // ------------------------------------------------------------------ 取消订阅

        /// <summary>取消订阅无参数事件。处理器未找到时打 Warn 日志，不抛异常。</summary>
        void Unsubscribe(int eventId, Action handler);

        /// <summary>取消订阅 1 参数事件。</summary>
        void Unsubscribe<T1>(int eventId, Action<T1> handler);

        /// <summary>取消订阅 2 参数事件。</summary>
        void Unsubscribe<T1, T2>(int eventId, Action<T1, T2> handler);

        /// <summary>取消订阅 3 参数事件。</summary>
        void Unsubscribe<T1, T2, T3>(int eventId, Action<T1, T2, T3> handler);

        /// <summary>取消订阅 4 参数事件。</summary>
        void Unsubscribe<T1, T2, T3, T4>(int eventId, Action<T1, T2, T3, T4> handler);

        // ------------------------------------------------------------------ 发送（同步立即派发）

        /// <summary>
        /// 立即同步派发无参数事件。
        /// </summary>
        void Send(int eventId);

        /// <summary>
        /// 立即同步派发 1 参数事件。
        /// </summary>
        void Send<T1>(int eventId, T1 arg1);

        /// <summary>
        /// 立即同步派发 2 参数事件。
        /// </summary>
        void Send<T1, T2>(int eventId, T1 arg1, T2 arg2);

        /// <summary>
        /// 立即同步派发 3 参数事件。
        /// </summary>
        void Send<T1, T2, T3>(int eventId, T1 arg1, T2 arg2, T3 arg3);

        /// <summary>
        /// 立即同步派发 4 参数事件。
        /// </summary>
        void Send<T1, T2, T3, T4>(int eventId, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    }
}
