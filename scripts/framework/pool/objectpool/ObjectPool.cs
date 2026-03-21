using System;
using System.Collections.Generic;

namespace Framework
{
    /// <summary>
    /// 泛型纯 C# 对象池实现。
    /// <para>内部使用 <see cref="Queue{T}"/> 存储空闲对象，支持自动定时释放和容量溢出策略。</para>
    /// </summary>
    /// <typeparam name="T">池化对象类型，必须实现 <see cref="IObjectPoolItem"/> 且有无参构造函数。</typeparam>
    internal sealed class ObjectPool<T> : ObjectPoolBase, IObjectPool<T>
        where T : class, IObjectPoolItem, new()
    {
        private readonly Queue<T> _idleQueue;
        private readonly string _name;
        private int _capacity;
        private bool _allowOverflow;
        private float _autoReleaseInterval;
        private double _autoReleaseTimer;

        /// <summary>
        /// 初始化对象池。
        /// </summary>
        /// <param name="name">对象池名称。</param>
        /// <param name="capacity">容量上限。</param>
        /// <param name="autoReleaseInterval">自动释放间隔（秒），小于等于 0 表示禁用。</param>
        public ObjectPool(string name, int capacity, float autoReleaseInterval)
        {
            _name = name ?? string.Empty;
            _capacity = capacity > 0 ? capacity : 32;
            _autoReleaseInterval = autoReleaseInterval;
            _allowOverflow = false;
            _autoReleaseTimer = 0;
            _idleQueue = new Queue<T>(_capacity);
        }

        // ---- ObjectPoolBase 抽象属性实现 ----

        /// <inheritdoc/>
        public override string Name => _name;

        /// <inheritdoc/>
        public override Type ItemType => typeof(T);

        /// <inheritdoc/>
        public override int Count => _idleQueue.Count;

        /// <inheritdoc/>
        public override int Capacity
        {
            get => _capacity;
            set => _capacity = value > 0 ? value : 1;
        }

        /// <inheritdoc/>
        public override bool AllowOverflow
        {
            get => _allowOverflow;
            set => _allowOverflow = value;
        }

        /// <inheritdoc/>
        public override float AutoReleaseInterval
        {
            get => _autoReleaseInterval;
            set
            {
                _autoReleaseInterval = value;
                _autoReleaseTimer = 0;
            }
        }

        // ---- IObjectPool<T> 方法实现 ----

        /// <summary>
        /// 从对象池中取出一个对象。
        /// <para>若池中有空闲对象则直接取出，否则通过 <c>new T()</c> 创建新实例。</para>
        /// 取出后自动调用 <see cref="IObjectPoolItem.OnSpawn"/>。
        /// </summary>
        public T Spawn()
        {
            T item = _idleQueue.Count > 0 ? _idleQueue.Dequeue() : new T();
            item.OnSpawn();
            return item;
        }

        /// <summary>
        /// 将对象回收到对象池。
        /// <para>先调用 <see cref="IObjectPoolItem.OnRecycle"/>，
        /// 再根据容量策略决定入队或丢弃。</para>
        /// </summary>
        public void Recycle(T item)
        {
            if (item == null)
            {
                Debugger.Warn($"[ObjectPool<{typeof(T).Name}>] Recycle: item is null, ignored.");
                return;
            }

            item.OnRecycle();

            if (_idleQueue.Count < _capacity || _allowOverflow)
            {
                _idleQueue.Enqueue(item);
            }
            else
            {
                // 超过容量且不允许扩容：丢弃，交由 GC 处理
                Debugger.Warn($"[ObjectPool<{typeof(T).Name}>] Pool is full (capacity={_capacity}), object discarded.");
            }
        }

        /// <summary>
        /// 立即释放池中所有空闲对象（清空队列，等待 GC 回收）。
        /// </summary>
        public override void ReleaseAllUnused()
        {
            int count = _idleQueue.Count;
            _idleQueue.Clear();
            Debugger.Info($"[ObjectPool<{typeof(T).Name}>] Released {count} idle objects.");
        }

        // ---- ObjectPoolBase 内部方法实现 ----

        /// <summary>
        /// 每帧更新自动释放计时器。
        /// </summary>
        internal override void Process(double elapseSeconds, double realElapseSeconds)
        {
            if (_autoReleaseInterval <= 0f || _idleQueue.Count == 0)
                return;

            _autoReleaseTimer += realElapseSeconds;
            if (_autoReleaseTimer >= _autoReleaseInterval)
            {
                _autoReleaseTimer = 0;
                ReleaseAllUnused();
            }
        }

        /// <summary>
        /// 关闭并清理对象池。
        /// </summary>
        internal override void Shutdown()
        {
            _idleQueue.Clear();
        }
    }
}
