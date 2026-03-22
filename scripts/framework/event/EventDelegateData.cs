using System;
using System.Collections.Generic;

namespace Framework
{
    /// <summary>
    /// 单个事件 ID 的委托数据容器。
    /// <para>
    /// 参考 TEngine EventDelegateData 实现，通过"脏数据（dirty）"缓冲机制解决
    /// 在回调执行期间 Subscribe / Unsubscribe 导致的迭代器失效问题：
    /// <list type="bullet">
    ///   <item>执行中调用 AddHandler → 先进 <c>_addList</c>，执行完再合并；</item>
    ///   <item>执行中调用 RemoveHandler → 先进 <c>_removeList</c>，执行完再移除；</item>
    ///   <item>执行完毕后统一通过 <c>ApplyModify()</c> 应用变更。</item>
    /// </list>
    /// </para>
    /// </summary>
    internal class EventDelegateData
    {
        private readonly int _eventId;
        private readonly List<Delegate> _handlers   = new List<Delegate>();
        private readonly List<Delegate> _addList    = new List<Delegate>();
        private readonly List<Delegate> _removeList = new List<Delegate>();
        private bool _isExecuting = false;
        private bool _dirty       = false;

        internal EventDelegateData(int eventId)
        {
            _eventId = eventId;
        }

        // ------------------------------------------------------------------ 订阅管理

        /// <summary>添加处理器。重复添加返回 false（打日志，不抛异常）。</summary>
        internal bool AddHandler(Delegate handler)
        {
            if (_handlers.Contains(handler))
            {
                Debugger.Error($"[EventDelegateData] Repeated Subscribe, EventId={_eventId}");
                return false;
            }

            if (_isExecuting)
            {
                _dirty = true;
                _addList.Add(handler);
            }
            else
            {
                _handlers.Add(handler);
            }
            return true;
        }

        /// <summary>移除处理器。不存在时打日志，不抛异常。</summary>
        internal void RemoveHandler(Delegate handler)
        {
            if (_isExecuting)
            {
                _dirty = true;
                _removeList.Add(handler);
            }
            else
            {
                if (!_handlers.Remove(handler))
                {
                    Debugger.Warn($"[EventDelegateData] Unsubscribe failed, handler not found, EventId={_eventId}");
                }
            }
        }

        // ------------------------------------------------------------------ 执行

        public void Invoke()
        {
            _isExecuting = true;
            for (int i = 0; i < _handlers.Count; i++)
            {
                if (_handlers[i] is Action action)
                    action();
            }
            ApplyModify();
        }

        public void Invoke<T1>(T1 arg1)
        {
            _isExecuting = true;
            for (int i = 0; i < _handlers.Count; i++)
            {
                if (_handlers[i] is Action<T1> action)
                    action(arg1);
            }
            ApplyModify();
        }

        public void Invoke<T1, T2>(T1 arg1, T2 arg2)
        {
            _isExecuting = true;
            for (int i = 0; i < _handlers.Count; i++)
            {
                if (_handlers[i] is Action<T1, T2> action)
                    action(arg1, arg2);
            }
            ApplyModify();
        }

        public void Invoke<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3)
        {
            _isExecuting = true;
            for (int i = 0; i < _handlers.Count; i++)
            {
                if (_handlers[i] is Action<T1, T2, T3> action)
                    action(arg1, arg2, arg3);
            }
            ApplyModify();
        }

        public void Invoke<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            _isExecuting = true;
            for (int i = 0; i < _handlers.Count; i++)
            {
                if (_handlers[i] is Action<T1, T2, T3, T4> action)
                    action(arg1, arg2, arg3, arg4);
            }
            ApplyModify();
        }

        // ------------------------------------------------------------------ 私有辅助

        /// <summary>执行完毕后统一应用延迟的 Add/Remove 变更。</summary>
        private void ApplyModify()
        {
            _isExecuting = false;
            if (!_dirty) return;

            for (int i = 0; i < _addList.Count; i++)
                _handlers.Add(_addList[i]);
            _addList.Clear();

            for (int i = 0; i < _removeList.Count; i++)
                _handlers.Remove(_removeList[i]);
            _removeList.Clear();

            _dirty = false;
        }
    }
}
