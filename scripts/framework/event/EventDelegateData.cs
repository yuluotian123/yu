using System;
using System.Collections.Generic;

namespace Framework
{
    /// <summary>Dispatches a stable subscriber list until the outermost send finishes.</summary>
    internal class EventDelegateData
    {
        private readonly int _eventId;
        private List<Delegate> _handlers = new();
        private List<Delegate> _pendingHandlers;
        private Type _signature;
        private int _executionDepth;

        internal EventDelegateData(int eventId) => _eventId = eventId;

        internal bool AddHandler(Delegate handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            ValidateSignature(handler.GetType());
            var handlers = GetWritableHandlers();
            if (handlers.Contains(handler))
            {
                Debugger.Warn($"[EventDelegateData] Repeated Subscribe, EventId={_eventId}");
                return false;
            }
            handlers.Add(handler);
            return true;
        }

        internal void RemoveHandler(Delegate handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            ValidateSignature(handler.GetType());
            if (!GetWritableHandlers().Remove(handler))
                Debugger.Warn($"[EventDelegateData] Unsubscribe failed, EventId={_eventId}");
        }

        public void Invoke()
        {
            BeginInvoke(typeof(Action));
            try
            {
                for (int i = 0; i < _handlers.Count; i++)
                    ((Action)_handlers[i])();
            }
            finally { EndInvoke(); }
        }

        public void Invoke<T1>(T1 arg1)
        {
            BeginInvoke(typeof(Action<T1>));
            try
            {
                for (int i = 0; i < _handlers.Count; i++)
                    ((Action<T1>)_handlers[i])(arg1);
            }
            finally { EndInvoke(); }
        }

        public void Invoke<T1, T2>(T1 arg1, T2 arg2)
        {
            BeginInvoke(typeof(Action<T1, T2>));
            try
            {
                for (int i = 0; i < _handlers.Count; i++)
                    ((Action<T1, T2>)_handlers[i])(arg1, arg2);
            }
            finally { EndInvoke(); }
        }

        public void Invoke<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3)
        {
            BeginInvoke(typeof(Action<T1, T2, T3>));
            try
            {
                for (int i = 0; i < _handlers.Count; i++)
                    ((Action<T1, T2, T3>)_handlers[i])(arg1, arg2, arg3);
            }
            finally { EndInvoke(); }
        }

        public void Invoke<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            BeginInvoke(typeof(Action<T1, T2, T3, T4>));
            try
            {
                for (int i = 0; i < _handlers.Count; i++)
                    ((Action<T1, T2, T3, T4>)_handlers[i])(arg1, arg2, arg3, arg4);
            }
            finally { EndInvoke(); }
        }

        private List<Delegate> GetWritableHandlers() => _executionDepth == 0
            ? _handlers
            : _pendingHandlers ??= new List<Delegate>(_handlers);

        private void ValidateSignature(Type signature)
        {
            if (_signature != null && _signature != signature)
                throw new ArgumentException($"Event {_eventId} expects {_signature}, received {signature}.");
            _signature = signature;
        }

        private void BeginInvoke(Type signature)
        {
            ValidateSignature(signature);
            _executionDepth++;
        }

        private void EndInvoke()
        {
            if (--_executionDepth != 0 || _pendingHandlers == null)
                return;
            _handlers = _pendingHandlers;
            _pendingHandlers = null;
        }
    }
}
