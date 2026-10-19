using Godot;

namespace GameLogic;

/// <summary>固定世界区域的实验 DDGI 配置；缓存使用未曝光的线性 HDR。</summary>
[Tool, GlobalClass]
public partial class DdgiProfile : Resource
{
    [Export] public bool Enabled { get; set; } = true;
    [Export] public Vector3 Origin { get; set; } = new(-2.5f, .4f, -2.5f);
    [Export] public Vector3I ProbeCounts { get; set; } = new(6, 4, 6);
    [Export(PropertyHint.Range, "0.25,8,0.05")] public float Spacing { get; set; } = 1;
    [Export(PropertyHint.Range, "32,512,32")] public int RaysPerProbe { get; set; } = 128;
    [Export(PropertyHint.Range, "1,4096,1")] public int ProbesPerFrame { get; set; } = 48;
    [Export(PropertyHint.Range, "0,0.99,0.01")] public float Hysteresis { get; set; } = .9f;
    [Export(PropertyHint.Range, "1,100,1")] public float MaxRayDistance { get; set; } = 30;
    [Export] public Color SkyRadiance { get; set; } = new(.035f, .045f, .07f);
    [Export] public bool UseEnvironmentSky { get; set; } = true;
    [Export(PropertyHint.Range, "0,4,0.01")] public float Energy { get; set; } = 1;
    [Export(PropertyHint.Range, "0,0.5,0.005")] public float NormalBias { get; set; } = .08f;
    [Export(PropertyHint.Range, "0,0.5,0.005")] public float ViewBias { get; set; } = .02f;

    internal Godot.Collections.Dictionary Snapshot() => new()
    {
        ["enabled"] = Enabled,
        ["origin"] = Origin,
        ["counts"] = new Vector3I(Mathf.Clamp(ProbeCounts.X, 2, 32), Mathf.Clamp(ProbeCounts.Y, 2, 16), Mathf.Clamp(ProbeCounts.Z, 2, 32)),
        ["spacing"] = Mathf.Clamp(Spacing, .25f, 8),
        ["rays"] = Mathf.Clamp(RaysPerProbe, 32, 512),
        ["budget"] = Mathf.Clamp(ProbesPerFrame, 1, 4096),
        ["history"] = Mathf.Clamp(Hysteresis, 0, .99f),
        ["max_distance"] = Mathf.Clamp(MaxRayDistance, 1, 100),
        ["sky"] = new Vector3(SkyRadiance.R, SkyRadiance.G, SkyRadiance.B),
        ["environment_sky"] = UseEnvironmentSky,
        ["energy"] = Mathf.Max(Energy, 0),
        ["normal_bias"] = Mathf.Clamp(NormalBias, 0, .5f),
        ["view_bias"] = Mathf.Clamp(ViewBias, 0, .5f)
    };
}
