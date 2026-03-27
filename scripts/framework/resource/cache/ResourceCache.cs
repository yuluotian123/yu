using System.Collections.Generic;
using Godot;

namespace Framework
{
    /// <summary>
    /// 基于 LRU（最近最少使用）策略的资源缓存实现，内置框架层引用计数。
    /// <remarks>
    /// 使用 <see cref="LinkedList{T}"/> + <see cref="Dictionary{TKey,TValue}"/> 实现 O(1) 存取与淘汰。
    /// 淘汰时优先移除最久未访问且框架引用计数为 0（无使用者持有）的资源。
    /// 若缓存已满但所有资源均被引用（RefCount > 0），则扩容写入并打印警告。
    /// </remarks>
    /// </summary>
    public sealed class ResourceCache : IResourceCache
    {
        private readonly int _maxSize;
        private readonly Dictionary<string, LinkedListNode<CacheEntry>> _map;
        private readonly LinkedList<CacheEntry> _lruList; // 头部为最近使用，尾部为最久未使用

        public ResourceCache(int maxSize)
        {
            _maxSize = maxSize > 0 ? maxSize : 128;
            _map = new Dictionary<string, LinkedListNode<CacheEntry>>(_maxSize);
            _lruList = new LinkedList<CacheEntry>();
        }

        /// <inheritdoc/>
        public int Count => _map.Count;

        /// <inheritdoc/>
        public bool TryGet(string path, out Resource resource)
        {
            if (_map.TryGetValue(path, out var node))
            {
                // 移到链表头部（最近使用）
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                resource = node.Value.Resource;
                return true;
            }

            resource = null;
            return false;
        }

        /// <inheritdoc/>
        public void Set(string path, Resource resource)
        {
            if (_map.TryGetValue(path, out var existingNode))
            {
                existingNode.Value.Resource = resource;
                _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
                return;
            }

            // 缓存已满时尝试淘汰
            if (_map.Count >= _maxSize)
            {
                Evict();
            }

            var entry = new CacheEntry(path, resource);
            var node = _lruList.AddFirst(entry);
            _map[path] = node;
        }

        /// <inheritdoc/>
        public bool Remove(string path)
        {
            if (_map.TryGetValue(path, out var node))
            {
                _lruList.Remove(node);
                _map.Remove(path);
                return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public bool Contains(string path)
        {
            return _map.ContainsKey(path);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _map.Clear();
            _lruList.Clear();
        }

        // ---- 框架层引用计数 ----

        /// <inheritdoc/>
        public void Acquire(string path)
        {
            if (_map.TryGetValue(path, out var node))
            {
                node.Value.RefCount++;
            }
        }

        /// <inheritdoc/>
        public void Release(string path)
        {
            if (_map.TryGetValue(path, out var node))
            {
                if (node.Value.RefCount > 0)
                    node.Value.RefCount--;
            }
        }

        /// <inheritdoc/>
        public int GetRefCount(string path)
        {
            if (_map.TryGetValue(path, out var node))
                return node.Value.RefCount;
            return 0;
        }

        /// <summary>
        /// 从尾部（最久未使用）开始淘汰一个框架引用计数为 0 的资源。
        /// 若所有资源均被引用则打印警告并跳过淘汰。
        /// </summary>
        private void Evict()
        {
            var node = _lruList.Last;
            while (node != null)
            {
                // 框架引用计数为 0 说明没有使用者持有该资源，可安全淘汰
                if (node.Value.RefCount <= 0)
                {
                    _map.Remove(node.Value.Path);
                    _lruList.Remove(node);
                    Debugger.Info($"[ResourceCache] Evicted: '{node.Value.Path}'");
                    return;
                }
                node = node.Previous;
            }

            // 所有资源均被引用，无法淘汰，缓存超出上限打印警告
            Debugger.Warn($"[ResourceCache] Cache is full ({_map.Count}/{_maxSize}) and all resources are referenced. Consider increasing MaxCacheSize.");
        }

        // ---- 内部数据结构 ----

        private sealed class CacheEntry
        {
            public string Path { get; }
            public Resource Resource { get; set; }
            /// <summary>框架层引用计数。每次 LoadAsset/LoadAssetAsync 成功 +1，Release 时 -1。</summary>
            public int RefCount { get; set; }

            public CacheEntry(string path, Resource resource)
            {
                Path = path;
                Resource = resource;
                RefCount = 0;
            }
        }
    }
}
