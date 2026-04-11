using System.Collections.Generic;
using Godot;

namespace Framework
{
    /// <summary>
    /// Resource cache strategy interface.
    /// </summary>
    public interface IResourceCache
    {
        int Count { get; }

        bool TryGet(string path, out Resource resource);

        void Set(string path, Resource resource);

        bool Remove(string path);

        bool Contains(string path);

        void Clear();

        void Acquire(string path);

        void Release(string path);

        int GetRefCount(string path);

        IReadOnlyList<ResourceCacheProfilerEntry> GetProfilerEntries();
    }
}
