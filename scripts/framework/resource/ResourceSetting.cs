using Godot;

namespace Framework
{
    /// <summary>
    /// Resource module settings.
    /// </summary>
    [GlobalClass]
    public partial class ResourceSetting : Resource
    {
        [Export]
        public int MaxCacheSize { get; set; } = 128;

        [Export]
        public int MaxConcurrentLoadCount { get; set; } = 8;

        [Export]
        public bool EnableLog { get; set; } = true;

        [Export]
        public bool EnableProfilerOverlay { get; set; } = true;

        [Export]
        public bool ShowProfilerOverlayOnStart { get; set; } = false;

        [Export(PropertyHint.Range, "0.1,2.0,0.05")]
        public float ProfilerOverlayRefreshInterval { get; set; } = 0.25f;

        [Export(PropertyHint.Range, "4,40,1")]
        public int ProfilerOverlayMaxRows { get; set; } = 12;
    }
}
