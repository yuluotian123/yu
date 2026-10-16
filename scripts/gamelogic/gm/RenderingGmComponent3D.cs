using System.Text;
using Godot;

namespace GameLogic;

/// <summary>渲染诊断页。</summary>
[GlobalClass]
public partial class RenderingGmComponent3D : Component3D
{
    public override int Priority => ComponentPriority.VFX;
    [Export] public NodePath WorldEnvironmentPath { get; set; } = new("../EnvironmentRig/WorldEnvironment");
    private Control _page;
    private Label _fps;
    private RichTextLabel _details;
    private WorldEnvironment _world;
    private ulong _lastRefresh;

    public override void OnInit()
    {
        _page = Owner.GetNode<Control>("%RenderingPage");
        _fps = Owner.GetNode<Label>("%RenderFps");
        _details = Owner.GetNode<RichTextLabel>("%RenderingText");
        _world = Owner.GetNodeOrNull<WorldEnvironment>(WorldEnvironmentPath);
        Owner.SetMeta("rendering_gm", this);
        Refresh();
    }

    public override void OnUpdate(double delta)
    {
        if (!_page.IsVisibleInTree()) return;
        ulong now = Time.GetTicksMsec();
        if (now - _lastRefresh >= 500) Refresh();
    }

    public void Refresh()
    {
        _lastRefresh = Time.GetTicksMsec();
        double fps = Engine.GetFramesPerSecond();
        _fps.Text = $"{fps:0} FPS  ·  帧间隔 {(fps > 0 ? 1000.0 / fps : 0):0.00} ms";
        _details.Text = BuildSnapshot();
    }

    public string BuildSnapshot()
    {
        var viewport = Owner.GetViewport();
        var camera = viewport.GetCamera3D();
        var world3D = viewport.FindWorld3D();
        var environment = camera?.Environment ?? world3D?.Environment ?? world3D?.FallbackEnvironment;
        var compositor = camera?.Compositor ?? (GodotObject.IsInstanceValid(_world) ? _world.Compositor : null);
        var attributes = camera?.Attributes ?? world3D?.CameraAttributes;
        var text = new StringBuilder();
        text.AppendLine($"渲染器  {RenderingServer.GetCurrentRenderingMethod()} / {RenderingServer.GetCurrentRenderingDriverName()}");
        text.AppendLine($"GPU  {RenderingServer.GetVideoAdapterName()}");
        Vector2 size = viewport.GetVisibleRect().Size;
        text.AppendLine($"视口  {size.X:0} × {size.Y:0}  ·  3D 比例 {viewport.Scaling3DScale:P0} / {viewport.Scaling3DMode}");
        text.AppendLine($"抗锯齿  {viewport.Msaa3D}  ·  TAA {On(viewport.UseTaa)}  ·  屏幕 AA {viewport.ScreenSpaceAA}");
        text.AppendLine($"VSync  {DisplayServer.WindowGetVsyncMode()}  ·  FPS 上限 {(Engine.MaxFps == 0 ? "无限制" : Engine.MaxFps.ToString())}");
        text.AppendLine($"近 1 秒峰值  Process {Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.00} ms  ·  Physics {Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000:0.00} ms");
        text.AppendLine($"Draw Calls  场景 {Draws(viewport, Viewport.RenderInfoType.Visible)} / 阴影 {Draws(viewport, Viewport.RenderInfoType.Shadow)} / UI {Draws(viewport, Viewport.RenderInfoType.Canvas)}");
        text.AppendLine($"全局  {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame):N0} 个对象 / {Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame):N0} 个图元");
        text.AppendLine($"显存分配  {Memory(Performance.Monitor.RenderVideoMemUsed):0.0} MiB  ·  纹理 {Memory(Performance.Monitor.RenderTextureMemUsed):0.0} / 缓冲 {Memory(Performance.Monitor.RenderBufferMemUsed):0.0} MiB");
        text.AppendLine();
        text.AppendLine($"当前相机  {camera?.GetPath().ToString() ?? "无 3D 相机"}");
        text.AppendLine($"环境来源  {(camera?.Environment != null ? "相机覆盖" : "World3D / WorldEnvironment")}");
        text.AppendLine($"合成器来源  {(camera?.Compositor != null ? "相机覆盖" : "WorldEnvironment")}");
        if (attributes is CameraAttributesPhysical physical)
        {
            text.AppendLine($"物理曝光  ISO {physical.ExposureSensitivity:0} / f{physical.ExposureAperture:0.0} / 1/{physical.ExposureShutterSpeed:0.##} s  ·  补偿 {physical.ExposureMultiplier:0.###}  ·  自动曝光 {On(physical.AutoExposureEnabled)}");
        }
        if (environment != null)
        {
            text.AppendLine($"Godot Tonemap  {environment.TonemapMode}  ·  Exposure {environment.TonemapExposure:0.###}");
            text.AppendLine($"Godot Glow {On(environment.GlowEnabled)}  ·  Adjustment {On(environment.AdjustmentEnabled)} / {environment.AdjustmentColorCorrection?.GetClass() ?? "无 LUT"}");
            text.AppendLine($"SSAO {On(environment.SsaoEnabled)}  ·  SSIL {On(environment.SsilEnabled)}  ·  SSR {On(environment.SsrEnabled)}");
            text.AppendLine($"SDFGI {On(environment.SdfgiEnabled)}  ·  Fog {On(environment.FogEnabled)}");
            if (environment.Sky?.SkyMaterial is ShaderMaterial sky && sky.Shader?.ResourcePath == "res://assets/shaders/environment/day_night_sky.gdshader")
                text.AppendLine($"天空 IBL  {sky.GetShaderParameter("ibl_strength").AsDouble():0.000}（天空环境光，不改变天空背景）");
        }
        text.AppendLine();
        text.AppendLine("合成器阶段");
        int relinkCount = 0;
        if (compositor != null)
        {
            var effects = compositor.CompositorEffects;
            using var effectsOwner = (Godot.Collections.Array)effects;
            foreach (CompositorEffect effect in effects)
            {
                if (effect == null) continue;
                string name = effect.ResourceName;
                if (string.IsNullOrEmpty(name) && effect is RelinkPostProcessEffect named)
                    name = named.Pass == RelinkPostProcessEffect.PassKind.ColorGrade ? "Relink · HDR LUT" : "Relink · Film + Bloom";
                text.AppendLine($"  {name}  ·  {effect.EffectCallbackType} / {On(effect.Enabled)}");
                if (effect is not RelinkPostProcessEffect relink || relink.Profile == null) continue;
                relinkCount++;
                var post = relink.Profile;
                text.AppendLine($"    Profile {On(post.Active)} / Shader {(relink.HasShaderError ? "错误" : "正常")} / {(relink.Enabled && post.Active ? relink.GetRenderStatus() : "已关闭")}");
                if (relink.Pass == RelinkPostProcessEffect.PassKind.ColorGrade)
                    text.AppendLine($"    近 LUT {Lut(post.NearLut)} / 远 LUT {Lut(post.FarLut)}  ·  强度 {post.LutStrength:0.00}  ·  距离 {post.DepthRanges}");
                else text.AppendLine($"    Film 输入 {post.Exposure:0.00}  ·  Bloom {On(post.BloomEnabled)} / {post.BloomIntensity:0.###}  ·  阈值 {post.BloomThreshold:0.###}");
            }
        }
        if (relinkCount == 0) text.AppendLine("  当前没有 Relink 后处理");
        if (environment != null && RelinkPostProcessEffect.HasActiveFilm(compositor))
        {
            bool conflict = environment.TonemapMode != Godot.Environment.ToneMapper.Linear
                || !Mathf.IsEqualApprox(environment.TonemapExposure, 1) || environment.GlowEnabled || environment.AdjustmentEnabled;
            text.AppendLine(conflict ? "⚠ 后处理冲突：Relink Film 与原生 Tonemap / Exposure / Glow / Adjustment 叠加。" : "后处理检查：正常；Relink Film → Godot Linear / sRGB 输出。");
        }
        if (GodotObject.IsInstanceValid(_world) && camera?.Compositor != null && !ReferenceEquals(camera.Compositor, _world.Compositor))
            text.AppendLine("⚠ 相机覆盖了关卡合成器；检查相机环境与后处理配置。");
        text.AppendLine("FPS / Monitor 数值可能延迟一秒；帧处理时间来自 Godot Monitor，帧间隔包含等待，不代表 GPU 耗时。统计包含 GM 界面开销。");
        return text.ToString();
    }

    private static string On(bool value) => value ? "开" : "关";
    private static int Draws(Viewport viewport, Viewport.RenderInfoType type) => viewport.GetRenderInfo(type, Viewport.RenderInfo.DrawCallsInFrame);
    private static double Memory(Performance.Monitor monitor) => Performance.GetMonitor(monitor) / (1024.0 * 1024.0);
    private static string Lut(Texture3D lut) => lut == null ? "缺失" : $"{lut.GetWidth()}³ HDR";

    public override void OnDestroy()
    {
        if (GodotObject.IsInstanceValid(Owner)) Owner.RemoveMeta("rendering_gm");
        _world = null;
        _page = null;
        _fps = null;
        _details = null;
    }
}
