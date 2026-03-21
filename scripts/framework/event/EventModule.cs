using System;
using System.Collections.Generic;

namespace Framework
{
    /// <summary>
    /// 游戏事件模块实现。
    /// <para>
    /// 通过 <see cref="ModuleSystem.GetModule{T}"/> 以 <see cref="IEventModule"/> 接口获取实例。
    /// 命名遵循框架约定：接口名去掉 'I' 前缀即为实现类名（<c>IEventModule</c> → <c>EventModule</c>）。
    /// </para>
    /// <para>
    /// 功能概览：
    /// <list type="bullet">
    ///   <item>基于事件 ID 的发布-订阅机制，每个事件 ID 对应一个处理器链表。</item>
    ///   <item><see cref="Fire"/> 将事件推入队列，在每帧 <see cref="Process"/> 时统一派发，避免回调中嵌套触发事件的问题。</item>
    ///   <item><see cref="FireNow"/> 立即同步派发，适用于需要当帧响应的场景。</item>
    /// </list>
    /// </para>
    /// </summary>
    internal sealed class EventModule : Module, IEventModule, IProcessModule
    {
        // 事件处理器表：eventId → 处理器链表
        private readonly Dictionary<int, LinkedList<EventHandler<GameEventArgs>>> _eventHandlerMap;

        // 事件队列节点：存储 sender 和 EventArgs
        private struct EventNode
        {
            public object Sender;
            public GameEventArgs EventArgs;

            public EventNode(object sender, GameEventArgs eventArgs)
            {
                Sender = sender;
                EventArgs = eventArgs;
            }
        }

        // 待派发的事件队列（Fire 推入，Process 消费）
        private readonly Queue<EventNode> _eventQueue;

        // 正在派发中的临时缓冲（避免 Process 过程中 Fire 影响迭代）
        private readonly List<EventNode> _dispatchBuffer;

        public EventModule()
        {
            _eventHandlerMap = new Dictionary<int, LinkedList<EventHandler<GameEventArgs>>>();
            _eventQueue = new Queue<EventNode>();
            _dispatchBuffer = new List<EventNode>();
        }

        /// <inheritdoc/>
        public override int Priority => 0;

        // ---- Module 生命周期 ----

        /// <inheritdoc/>
        public override void OnInit()
        {
            Debugger.Info("[EventModule] Initialized.");
        }

        /// <inheritdoc/>
        public override void Shutdown()
        {
            _eventHandlerMap.Clear();
            _eventQueue.Clear();
            _dispatchBuffer.Clear();
            Debugger.Info("[EventModule] Shutdown.");
        }

        // ---- IProcessModule ----

        /// <summary>
        /// 每帧消费事件队列，依次派发所有待处理事件。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
        /// <param name="realElapseSeconds">真实流逝时间（秒）。</param>
        public void Process(double elapseSeconds, double realElapseSeconds)
        {
            if (_eventQueue.Count == 0)
            {
                return;
            }

            // 将队列内容转移到缓冲区，允许处理器内部继续调用 Fire 而不影响本次迭代
            _dispatchBuffer.Clear();
            while (_eventQueue.Count > 0)
            {
                _dispatchBuffer.Add(_eventQueue.Dequeue());
            }

            int count = _dispatchBuffer.Count;
            for (int i = 0; i < count; i++)
            {
                var node = _dispatchBuffer[i];
                HandleEvent(node.Sender, node.EventArgs);
            }
        }

        // ---- IEventModule ----

        /// <inheritdoc/>
        public int EventCount => _eventQueue.Count;

        /// <inheritdoc/>
        public bool HasEventHandler(int eventId)
        {
            return _eventHandlerMap.TryGetValue(eventId, out var list) && list.Count > 0;
        }

        /// <inheritdoc/>
        public bool HasEventHandler(int eventId, EventHandler<GameEventArgs> handler)
        {
            if (handler == null)
            {
                throw new Exception("[EventModule] HasEventHandler: handler is null.");
            }

            return _eventHandlerMap.TryGetValue(eventId, out var list) && list.Contains(handler);
        }

        /// <inheritdoc/>
        public void Subscribe(int eventId, EventHandler<GameEventArgs> handler)
        {
            if (handler == null)
            {
                throw new Exception("[EventModule] Subscribe: handler is null.");
            }

            if (!_eventHandlerMap.TryGetValue(eventId, out var list))
            {
                list = new LinkedList<EventHandler<GameEventArgs>>();
                _eventHandlerMap[eventId] = list;
            }

            if (list.Contains(handler))
            {
                throw new Exception(
                    $"[EventModule] Subscribe: handler already subscribed to event ID '{eventId}'. Duplicate subscription is not allowed.");
            }

            list.AddLast(handler);
        }

        /// <inheritdoc/>
        public void Unsubscribe(int eventId, EventHandler<GameEventArgs> handler)
        {
            if (handler == null)
            {
                throw new Exception("[EventModule] Unsubscribe: handler is null.");
            }

            if (!_eventHandlerMap.TryGetValue(eventId, out var list) || !list.Remove(handler))
            {
                throw new Exception(
                    $"[EventModule] Unsubscribe: handler is not subscribed to event ID '{eventId}'.");
            }
        }

        /// <inheritdoc/>
        public void Fire(object sender, GameEventArgs e)
        {
            if (e == null)
            {
                throw new Exception("[EventModule] Fire: GameEventArgs is null.");
            }

            _eventQueue.Enqueue(new EventNode(sender, e));
        }

        /// <inheritdoc/>
        public void FireNow(object sender, GameEventArgs e)
        {
            if (e == null)
            {
                throw new Exception("[EventModule] FireNow: GameEventArgs is null.");
            }

            HandleEvent(sender, e);
        }

        // ---- 私有方法 ----

        /// <summary>
        /// 执行事件派发，将事件分发给所有已订阅的处理器。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void HandleEvent(object sender, GameEventArgs e)
        {
            int eventId = e.Id;

            if (!_eventHandlerMap.TryGetValue(eventId, out var list) || list.Count == 0)
            {
                Debugger.Warn($"[EventModule] HandleEvent: No handler subscribed to event ID '{eventId}' ({e.GetType().Name}). Event dropped.");
                return;
            }

            // 遍历处理器链表逐一调用
            // 使用 LinkedList 遍历，即使某个处理器内部 Unsubscribe 也不会影响当前遍历
            // （LinkedList 的 Remove 不会使已获取的 Next 指针失效）
            LinkedListNode<EventHandler<GameEventArgs>> current = list.First;
            while (current != null)
            {
                // 提前取 Next，防止处理器内部 Unsubscribe 当前节点后指针丢失
                LinkedListNode<EventHandler<GameEventArgs>> next = current.Next;
                try
                {
                    current.Value?.Invoke(sender, e);
                }
                catch (Exception ex)
                {
                    Debugger.Error(
                        $"[EventModule] HandleEvent: Exception in handler for event ID '{eventId}' ({e.GetType().Name}): {ex}");
                }

                current = next;
            }
        }
    }
}
