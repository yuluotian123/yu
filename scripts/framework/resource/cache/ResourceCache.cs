using System.Collections.Generic;
using Godot;

namespace Framework
{
    /// <summary>
    /// 基于 LRU（最近最少使用）策略的资源缓存实现。
    /// <remarks>
    /// 使用 <see cref="LinkedList{T}"/> + <see cref="Dictionary{TKey,TValue}"/> 实现 O(1) 存取与淘汰。
    /// 淘汰时优先移除最久未访问且 Godot 引用计数为 1（仅本缓存持有）的资源。
    /// 若缓存已满但所有资源均被外部引用（引用计数 > 1），则扩容写入并打印警告。
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

        /// <summary>
        /// 从尾部（最久未使用）开始淘汰一个引用计数为 1 的资源。
        /// 若所有资源均被外部引用则打印警告并跳过淘汰。
        /// </summary>
        private void Evict()
        {
            var node = _lruList.Last;
            while (node != null)
            {
                var resource = node.Value.Resource;
                // GetReferenceCount() == 1 说明只有本缓存持有该资源，可安全淘汰
                if (resource != null && resource.GetReferenceCount() <= 1)
                {
                    _map.Remove(node.Value.Path);
                    _lruList.Remove(node);
                    Debugger.Info($"[ResourceCache] Evicted: '{node.Value.Path}'");
                    return;
                }
                node = node.Previous;
            }

            // 所有资源均被外部引用，无法淘汰，缓存超出上限打印警告
            Debugger.Warn($"[ResourceCache] Cache is full ({_map.Count}/{_maxSize}) and all resources are in use. Consider increasing MaxCacheSize.");
        }

        // ---- 内部数据结构 ----

        private sealed class CacheEntry
        {
            public string Path { get; }
            public Resource Resource { get; set; }

            public CacheEntry(string path, Resource resource)
            {
                Path = path;
                Resource = resource;
            }
        }
    }
}
