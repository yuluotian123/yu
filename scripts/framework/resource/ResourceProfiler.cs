using System;
using System.Collections.Generic;

namespace Framework
{
    public sealed class ResourceProfilerSnapshot
    {
        public DateTime CreatedAtUtc { get; init; }
        public int CacheCount { get; init; }
        public int PendingCancelCount { get; init; }
        public int LiveHandleCount { get; init; }
        public int LoadingHandleCount { get; init; }
        public int SucceedHandleCount { get; init; }
        public int FailedHandleCount { get; init; }
        public int CancelledHandleCount { get; init; }
        public int ReleasedHandleCount { get; init; }
        public int InvalidHandleCount { get; init; }
        public ResourceLoaderProfilerSnapshot Loader { get; init; }
        public IReadOnlyList<ResourceHandleProfilerEntry> Handles { get; init; }
        public IReadOnlyList<ResourceCacheProfilerEntry> CacheEntries { get; init; }
    }

    public sealed class ResourceHandleProfilerEntry
    {
        public int HandleId { get; init; }
        public string Path { get; init; }
        public string RequestedTypeName { get; init; }
        public ResourceHandleStatus Status { get; init; }
        public float Progress { get; init; }
        public bool OwnsReference { get; init; }
        public bool IsValid { get; init; }
        public string Error { get; init; }
    }

    public sealed class ResourceCacheProfilerEntry
    {
        public string Path { get; init; }
        public string ResourceTypeName { get; init; }
        public int RefCount { get; init; }
        public int LruIndex { get; init; }
    }

    public sealed class ResourceLoaderProfilerSnapshot
    {
        public int MaxConcurrent { get; init; }
        public int ActiveCount { get; init; }
        public int WaitingCount { get; init; }
        public int TaskCount { get; init; }
        public IReadOnlyList<ResourceLoadTaskProfilerEntry> Tasks { get; init; }
    }

    public sealed class ResourceLoadTaskProfilerEntry
    {
        public string Path { get; init; }
        public bool IsStarted { get; init; }
        public bool IsDone { get; init; }
        public float Progress { get; init; }
        public int RequestCount { get; init; }
        public int ActiveRequestCount { get; init; }
    }
}
