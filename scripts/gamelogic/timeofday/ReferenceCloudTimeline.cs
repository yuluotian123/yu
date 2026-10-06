using Godot;

namespace GameLogic;

/// <summary>原仓库 Timeline 曲线；保持关键帧及 Hermite 切线，时间域归一到 0..1。</summary>
[GlobalClass]
[Tool]
public partial class ReferenceCloudTimeline : Resource
{
    [Export] public Godot.Collections.Dictionary<string, Curve> Curves { get; set; } = new();

    public float Sample(string property, float phase, float fallback = 0) =>
        Curves.TryGetValue(property, out var curve) && curve != null ? curve.Sample(Mathf.Clamp(phase, 0, 1)) : fallback;

    public Color SampleColor(string property, float phase) => new(
        Sample(property + ".r", phase, 1), Sample(property + ".g", phase, 1),
        Sample(property + ".b", phase, 1), Sample(property + ".a", phase, 1));
}
