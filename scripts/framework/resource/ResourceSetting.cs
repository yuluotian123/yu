using Godot;

namespace Framework
{
    /// <summary>
    /// 资源模块配置。
    /// </summary>
    [GlobalClass]
    public partial class ResourceSetting : Resource
    {
        /// <summary>
        /// 资源缓存最大数量。
        /// 超出时将按 LRU 策略移除最久未访问且引用计数为 1（仅缓存持有）的资源。
        /// </summary>
        [Export]
        public int MaxCacheSize { get; set; } = 128;

        /// <summary>
        /// 最大并发异步加载数量。
        /// </summary>
        [Export]
        public int MaxConcurrentLoadCount { get; set; } = 8;

        /// <summary>
        /// 是否启用资源加载日志。
        /// </summary>
        [Export]
        public bool EnableLog { get; set; } = true;
    }
}
