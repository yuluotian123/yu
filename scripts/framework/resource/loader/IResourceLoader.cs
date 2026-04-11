using Godot;

namespace Framework
{
    /// <summary>
    /// Resource loader strategy interface.
    /// </summary>
    public interface IResourceLoader
    {
        Resource LoadSync(string path);

        void RequestAsync(string path, ResourceHandleBase handle, string typeHint = "");

        void Tick(IResourceCache cache, bool enableLog = false);

        ResourceLoaderProfilerSnapshot GetProfilerSnapshot();

        /// <summary>
        /// Ends all pending and in-flight requests tracked by the framework.
        /// This does not guarantee the Godot engine stops the underlying threaded load,
        /// but all handles must leave the Loading state.
        /// </summary>
        void Shutdown(string reason = null);
    }
}
