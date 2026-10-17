using Godot;

namespace GameLogic;

/// <summary>从完整 Relink 捕获提取的 HDR 调色；原生 Environment 和相机设置独立控制。</summary>
[Tool, GlobalClass]
public partial class RelinkPostProcessProfile : Resource
{
    /// <summary>整套 Relink 开关；不改原生 Environment 或各阶段 Enabled。</summary>
    [Export] public bool Active { get; set; } = true;
    [ExportGroup("Captured HDR LUTs")]
    /// <summary>近景 HDR 调色 LUT，与远景共用曝光与距离权重。</summary>
    [Export] public Texture3D NearLut { get; set; }
    /// <summary>远景 HDR 调色 LUT。</summary>
    [Export] public Texture3D FarLut { get; set; }
    /// <summary>LUT 混合强度，0 只保留电影曲线和泛光，1 完整调色。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float LutStrength { get; set; } = 1;
    /// <summary>近 LUT 淡出起止、远 LUT 淡入起止距离，单位米。</summary>
    [Export] public Vector4 DepthRanges { get; set; } = new(0, 15, 15, 60);
    // LUT 内烘焙了该帧的曝光，查询前除、查询后乘，保持当前相机的曝光独立。
    /// <summary>LUT 捕获曝光元数据，默认 1.4142035；通常无需调整。</summary>
    [Export(PropertyHint.Range, "0.01,8,0.001")] public float CapturedExposure { get; set; } = 1.4142035f;
    /// <summary>天空是否参与 LUT 调色；仍经过启用的电影曲线。</summary>
    [Export] public bool GradeSky { get; set; }
    [ExportGroup("Film and Bloom")]
    /// <summary>电影曲线输入补偿，逐帧读取；与原生及相机曝光独立。</summary>
    [Export(PropertyHint.Range, "0.01,8,0.01")] public float Exposure { get; set; } = 3f;
    /// <summary>Relink 泛光开关，不影响电影曲线或原生 Glow。</summary>
    [Export] public bool BloomEnabled { get; set; } = true;
    /// <summary>Relink 泛光强度，运行时直接读取；开启昼夜泛光时由曲线驱动。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float BloomIntensity { get; set; } = .04f;
    /// <summary>高亮提取阈值，越大参与泛光的区域越少。</summary>
    [Export(PropertyHint.Range, "0.1,8,0.05")] public float BloomThreshold { get; set; } = 1.8f;
    /// <summary>低于阈值的泛光底值，0 表示仅从高亮提取。</summary>
    [Export(PropertyHint.Range, "0,0.2,0.005")] public float BloomFloor { get; set; }
}
