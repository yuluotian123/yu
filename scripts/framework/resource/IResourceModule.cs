using Godot;

namespace Framework
{
    /// <summary>
    /// Resource module facade.
    /// </summary>
    public interface IResourceModule
    {
        ResourceHandle<T> LoadAsset<T>(string path) where T : Resource;

        T LoadAssetOnce<T>(string path) where T : Resource;

        bool TryLoadAssetOnce<T>(string path, out T asset) where T : Resource;

        ResourceHandle<T> LoadAssetAsync<T>(string path) where T : Resource;

        /// <summary>
        /// Loads a PackedScene asynchronously and returns a dedicated scene handle.
        /// Scene-specific operations such as instantiation and node lifetime binding live on SceneHandle.
        /// </summary>
        SceneHandle LoadSceneAsync(string path);

        void ForceUnloadAsset(string path);

        void UnloadAllAssets();

        bool HasAsset(string path);

        int CacheCount { get; }

        int GetRefCount(string path);

        ResourceProfilerSnapshot GetProfilerSnapshot();

        void DumpProfilerToLog(bool includeHandles = true, bool includeCacheEntries = true);

        bool IsProfilerOverlayVisible { get; }

        void SetProfilerOverlayVisible(bool visible);

        void ToggleProfilerOverlay();

        void SetLoader(IResourceLoader loader);

        void SetCache(IResourceCache cache);

        void ReleaseAsset(string path);
    }
}
