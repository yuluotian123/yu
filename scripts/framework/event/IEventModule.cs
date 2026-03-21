using System;

namespace Framework
{
    /// <summary>
    /// 游戏事件模块接口。
    /// <para>
    /// 提供基于事件 ID 的发布-订阅（Pub/Sub）机制。
    /// 通过 <see cref="ModuleSystem.GetModule{T}"/> 以本接口获取实例。
    /// </para>
    /// <para>
    /// 事件派发分为两种模式：
    /// <list type="bullet">
    ///   <item><see cref="Fire"/> —— 将事件推入队列，在下一帧 <c>Process</c> 时统一派发（推荐），避免在事件回调中嵌套触发事件引发的问题。</item>
    ///   <item><see cref="FireNow"/> —— 立即同步派发，适用于不需要延迟的场景。</item>
    /// </list>
    /// </para>
    /// <example>
    /// <code>
    /// var eventModule = ModuleSystem.GetModule&lt;IEventModule&gt;();
    ///
    /// // 订阅
    /// eventModule.Subscribe(GameEventArgs.GetEventId&lt;PlayerHitEventArgs&gt;(), OnPlayerHit);
    ///
    /// // 触发（延迟派发，推荐）
    /// eventModule.Fire(this, PlayerHitEventArgs.Create(10));
    ///
    /// // 取消订阅
    /// eventModule.Unsubscribe(GameEventArgs.GetEventId&lt;PlayerHitEventArgs&gt;(), OnPlayerHit);
    ///
    /// void OnPlayerHit(object sender, GameEventArgs e)
    /// {
    ///     var args = (PlayerHitEventArgs)e;
    ///     GD.Print($"Hit! Damage: {args.Damage}");
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public interface IEventModule
    {
        /// <summary>
        /// 获取当前队列中待派发的事件数量。
        /// </summary>
        int EventCount { get; }

        /// <summary>
        /// 检查指定事件是否存在至少一个处理器。
        /// </summary>
        /// <param name="eventId">事件 ID，通过 <see cref="GameEventArgs.GetEventId{T}"/> 获取。</param>
        /// <returns>是否存在处理器。</returns>
        bool HasEventHandler(int eventId);

        /// <summary>
        /// 检查指定处理器是否已订阅某事件。
        /// </summary>
        /// <param name="eventId">事件 ID。</param>
        /// <param name="handler">要检查的处理器。</param>
        /// <returns>是否已订阅。</returns>
        bool HasEventHandler(int eventId, EventHandler<GameEventArgs> handler);

        /// <summary>
        /// 订阅事件。
        /// </summary>
        /// <param name="eventId">事件 ID，通过 <see cref="GameEventArgs.GetEventId{T}"/> 获取。</param>
        /// <param name="handler">事件处理器。</param>
        /// <exception cref="Exception">重复订阅同一处理器时抛出异常。</exception>
        void Subscribe(int eventId, EventHandler<GameEventArgs> handler);

        /// <summary>
        /// 取消订阅事件。
        /// </summary>
        /// <param name="eventId">事件 ID。</param>
        /// <param name="handler">要取消的事件处理器。</param>
        /// <exception cref="Exception">处理器未订阅时抛出异常。</exception>
        void Unsubscribe(int eventId, EventHandler<GameEventArgs> handler);

        /// <summary>
        /// 将事件推入队列，在下一帧 <c>Process</c> 时统一派发（线程安全）。
        /// <para>这是推荐的事件触发方式，可避免在事件处理器中再次触发事件导致的递归问题。</para>
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数，其 <see cref="GameEventArgs.Id"/> 决定派发目标。</param>
        void Fire(object sender, GameEventArgs e);

        /// <summary>
        /// 立即同步派发事件，不经过队列。
        /// <para>适用于需要在当前帧立即响应的场景，但请注意避免在处理器中再次调用 <see cref="FireNow"/> 导致无限递归。</para>
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数，其 <see cref="GameEventArgs.Id"/> 决定派发目标。</param>
        void FireNow(object sender, GameEventArgs e);
    }
}
