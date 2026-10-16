using Godot;

namespace GameLogic;

/// <summary>从完整 Relink 捕获提取的 HDR 调色；相机物理曝光仍由昼夜系统控制。</summary>
[Tool, GlobalClass]
public partial class RelinkPostProcessProfile : Resource
{
    [Export] public bool Active { get; set; } = true;
    [ExportGroup("Captured HDR LUTs")]
    [Export] public Texture3D NearLut { get; set; }
    [Export] public Texture3D FarLut { get; set; }
    [Export(PropertyHint.Range, "0,1,0.01")] public float LutStrength { get; set; } = 1;
    [Export] public Vector4 DepthRanges { get; set; } = new(0, 15, 15, 60);
    // LUT 内烘焙了该帧的曝光，查询前除、查询后乘，保持当前相机的曝光独立。
    [Export(PropertyHint.Range, "0.01,8,0.001")] public float CapturedExposure { get; set; } = 1.4142035f;
    [Export] public bool GradeSky { get; set; }
    [ExportGroup("Film and Bloom")]
    [Export(PropertyHint.Range, "0.01,8,0.01")] public float Exposure { get; set; } = 3f;
    [Export] public bool BloomEnabled { get; set; } = true;
    [Export(PropertyHint.Range, "0,1,0.01")] public float BloomIntensity { get; set; } = .04f;
    [Export(PropertyHint.Range, "0.1,8,0.05")] public float BloomThreshold { get; set; } = 1.8f;
    [Export(PropertyHint.Range, "0,0.2,0.005")] public float BloomFloor { get; set; }
    [ExportGroup("Godot Fallback")]
    [Export] public Godot.Environment.ToneMapper NativeTonemap { get; set; } = Godot.Environment.ToneMapper.Agx;
    [Export(PropertyHint.Range, "0.01,8,0.01")] public float NativeExposure { get; set; } = .7f;
    [Export] public bool NativeGlow { get; set; } = true;
    [Export] public bool NativeAdjustment { get; set; } = true;
}
