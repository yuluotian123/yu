using System;
using System.Collections.Generic;

namespace Framework
{
    /// <summary>
    /// 事件模块实现。
    /// <para>
    /// 对齐 TEngine EventDispatcher 设计：
    /// <list type="bullet">
    ///   <item>使用 <see cref="Action{T}"/> 泛型委托，直接传参，编译期类型安全，无拆装箱；</item>
    ///   <item>通过 <see cref="EventDelegateData"/> 的脏数据机制保证回调中 Subscribe/Unsubscribe 安全；</item>
    ///   <item>所有 Send 均为同步立即派发，无队列延迟；</item>
    ///   <item>事件 ID 推荐通过 <see cref="EventId.Get{T}"/> 生成（单调递增，天然无碰撞）。</item>
    /// </list>
    /// </para>
    /// </summary>
    public class EventModule : Module, IEventModule
    {
        /// <summary>eventId → 该 ID 对应的委托数据。</summary>
        private readonly Dictionary<int, EventDelegateData> _eventTable
            = new Dictionary<int, EventDelegateData>();

        // ------------------------------------------------------------------ Module 生命周期

        public override void OnInit() { }

        public override void Shutdown()
        {
            _eventTable.Clear();
        }

        // ------------------------------------------------------------------ 内部辅助

        /// <summary>获取或创建 eventId 对应的数据条目。</summary>
        private EventDelegateData GetOrCreate(int eventId)
        {
            if (!_eventTable.TryGetValue(eventId, out var data))
            {
                data = new EventDelegateData(eventId);
                _eventTable.Add(eventId, data);
            }
            return data;
        }

        // ------------------------------------------------------------------ 订阅

        /// <inheritdoc/>
        public bool Subscribe(int eventId, Action handler)
            => GetOrCreate(eventId).AddHandler(handler);

        /// <inheritdoc/>
        public bool Subscribe<T1>(int eventId, Action<T1> handler)
            => GetOrCreate(eventId).AddHandler(handler);

        /// <inheritdoc/>
        public bool Subscribe<T1, T2>(int eventId, Action<T1, T2> handler)
            => GetOrCreate(eventId).AddHandler(handler);

        /// <inheritdoc/>
        public bool Subscribe<T1, T2, T3>(int eventId, Action<T1, T2, T3> handler)
            => GetOrCreate(eventId).AddHandler(handler);

        /// <inheritdoc/>
        public bool Subscribe<T1, T2, T3, T4>(int eventId, Action<T1, T2, T3, T4> handler)
            => GetOrCreate(eventId).AddHandler(handler);

        // ------------------------------------------------------------------ 取消订阅

        /// <inheritdoc/>
        public void Unsubscribe(int eventId, Action handler)
        {
            if (_eventTable.TryGetValue(eventId, out var data))
                data.RemoveHandler(handler);
            else
                Debugger.Warn($"[EventModule] Unsubscribe: eventId={eventId} has no subscribers.");
        }

        /// <inheritdoc/>
        public void Unsubscribe<T1>(int eventId, Action<T1> handler)
        {
            if (_eventTable.TryGetValue(eventId, out var data))
                data.RemoveHandler(handler);
            else
                Debugger.Warn($"[EventModule] Unsubscribe: eventId={eventId} has no subscribers.");
        }

        /// <inheritdoc/>
        public void Unsubscribe<T1, T2>(int eventId, Action<T1, T2> handler)
        {
            if (_eventTable.TryGetValue(eventId, out var data))
                data.RemoveHandler(handler);
            else
                Debugger.Warn($"[EventModule] Unsubscribe: eventId={eventId} has no subscribers.");
        }

        /// <inheritdoc/>
        public void Unsubscribe<T1, T2, T3>(int eventId, Action<T1, T2, T3> handler)
        {
            if (_eventTable.TryGetValue(eventId, out var data))
                data.RemoveHandler(handler);
            else
                Debugger.Warn($"[EventModule] Unsubscribe: eventId={eventId} has no subscribers.");
        }

        /// <inheritdoc/>
        public void Unsubscribe<T1, T2, T3, T4>(int eventId, Action<T1, T2, T3, T4> handler)
        {
            if (_eventTable.TryGetValue(eventId, out var data))
                data.RemoveHandler(handler);
            else
                Debugger.Warn($"[EventModule] Unsubscribe: eventId={eventId} has no subscribers.");
        }

        // ------------------------------------------------------------------ 发送

        /// <inheritdoc/>
        public void Send(int eventId)
        {
            if (_eventTable.TryGetValue(eventId, out var data))
                data.Invoke();
        }

        /// <inheritdoc/>
        public void Send<T1>(int eventId, T1 arg1)
        {
            if (_eventTable.TryGetValue(eventId, out var data))
                data.Invoke(arg1);
            else 
                Debugger.Warn($"[EventModule] Send: eventId={eventId} has no subscribers.");
        }

        /// <inheritdoc/>
        public void Send<T1, T2>(int eventId, T1 arg1, T2 arg2)
        {
            if (_eventTable.TryGetValue(eventId, out var data))
                data.Invoke(arg1, arg2);
        }

        /// <inheritdoc/>
        public void Send<T1, T2, T3>(int eventId, T1 arg1, T2 arg2, T3 arg3)
        {
            if (_eventTable.TryGetValue(eventId, out var data))
                data.Invoke(arg1, arg2, arg3);
        }

        /// <inheritdoc/>
        public void Send<T1, T2, T3, T4>(int eventId, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (_eventTable.TryGetValue(eventId, out var data))
                data.Invoke(arg1, arg2, arg3, arg4);
        }
    }
}
