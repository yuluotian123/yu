using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace GameLogic;

/// <summary>渲染诊断和当前合成器的 Relink 实时调节。</summary>
[Tool, GlobalClass]
public partial class RenderingGmComponent3D : Component3D
{
    public override int Priority => ComponentPriority.VFX;
    [Export] public NodePath WorldEnvironmentPath { get; set; } = new("../EnvironmentRig/WorldEnvironment");
    private Control _page;
    private Label _fps;
    private RichTextLabel _details;
    private Label _relinkHint;
    private CheckButton _relinkActive, _relinkBloom, _relinkSky, _relinkAutomatic;
    private SpinBox _relinkLut, _relinkExposure, _relinkIntensity, _relinkThreshold, _relinkFloor;
    private WorldEnvironment _world;
    private ulong _lastRefresh;
    private readonly List<(GodotObject Source, StringName Signal, Callable Callback)> _bindings = new();

    public override void OnInit()
    {
        _page = Owner.GetNode<Control>("%RenderingPage");
        _fps = Owner.GetNode<Label>("%RenderFps");
        _details = Owner.GetNode<RichTextLabel>("%RenderingText");
        _relinkHint = Owner.GetNode<Label>("%RelinkHint");
        _relinkActive = Owner.GetNode<CheckButton>("%RelinkActive");
        _relinkBloom = Owner.GetNode<CheckButton>("%RelinkBloom");
        _relinkSky = Owner.GetNode<CheckButton>("%RelinkSky");
        _relinkAutomatic = Owner.GetNode<CheckButton>("%RelinkAutomatic");
        _relinkLut = Owner.GetNode<SpinBox>("%RelinkLut");
        _relinkExposure = Owner.GetNode<SpinBox>("%RelinkExposure");
        _relinkIntensity = Owner.GetNode<SpinBox>("%RelinkIntensity");
        _relinkThreshold = Owner.GetNode<SpinBox>("%RelinkThreshold");
        _relinkFloor = Owner.GetNode<SpinBox>("%RelinkFloor");
        Bind(_relinkActive, BaseButton.SignalName.Toggled, Callable.From<bool>(value => EditRelink(post => post.Active = value)));
        Bind(_relinkBloom, BaseButton.SignalName.Toggled, Callable.From<bool>(value => EditRelink(post => post.BloomEnabled = value, manualBloom: true)));
        Bind(_relinkAutomatic, BaseButton.SignalName.Toggled, Callable.From<bool>(value =>
        {
            var controller = ResolveDayNight();
            if (controller?.Profile != null)
            {
                controller.Profile.DriveRelinkBloomFromProfile = value;
                using var state = controller.GetTimeState();
                controller.Profile.ApplyRelinkPostProcessing(ResolveCompositor(), state["hour"].AsDouble());
            }
            Refresh();
        }));
        Bind(_relinkSky, BaseButton.SignalName.Toggled, Callable.From<bool>(value => EditRelink(post => post.GradeSky = value)));
        Bind(_relinkLut, Godot.Range.SignalName.ValueChanged, Callable.From<double>(value => EditRelink(post => post.LutStrength = (float)value)));
        Bind(_relinkExposure, Godot.Range.SignalName.ValueChanged, Callable.From<double>(value => EditRelink(post => post.Exposure = (float)value)));
        Bind(_relinkIntensity, Godot.Range.SignalName.ValueChanged, Callable.From<double>(value => EditRelink(post => post.BloomIntensity = (float)value, manualBloom: true)));
        Bind(_relinkThreshold, Godot.Range.SignalName.ValueChanged, Callable.From<double>(value => EditRelink(post => post.BloomThreshold = (float)value, manualBloom: true)));
        Bind(_relinkFloor, Godot.Range.SignalName.ValueChanged, Callable.From<double>(value => EditRelink(post => post.BloomFloor = (float)value, manualBloom: true)));
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
        RefreshRelink();
    }

    private Compositor ResolveCompositor()
    {
        _world = Owner.GetNodeOrNull<WorldEnvironment>(WorldEnvironmentPath);
        var compositor = Owner.GetViewport().GetCamera3D()?.Compositor ?? _world?.Compositor;
        RelinkPostProcessEffect.MakeProfilesLocal(compositor);
        return compositor;
    }

    private static List<RelinkPostProcessProfile> GetRelinkProfiles(Compositor compositor)
    {
        var profiles = new List<RelinkPostProcessProfile>();
        if (compositor == null) return profiles;
        var ids = new HashSet<ulong>();
        var effects = compositor.CompositorEffects;
        using var effectsOwner = (Godot.Collections.Array)effects;
        foreach (CompositorEffect effect in effects)
            if (effect is RelinkPostProcessEffect relink && relink.Profile != null && ids.Add(relink.Profile.GetInstanceId()))
                profiles.Add(relink.Profile);
        return profiles;
    }

    private DayNightEnvironmentComponent3D ResolveDayNight()
    {
        if (_world?.Compositor == null) return null;
        var cameraCompositor = Owner.GetViewport().GetCamera3D()?.Compositor;
        if (cameraCompositor != null && cameraCompositor.GetInstanceId() != _world.Compositor.GetInstanceId()) return null;
        return (_world.GetParent() as GameObject3D)?.GetComponent<DayNightEnvironmentComponent3D>();
    }

    private void EditRelink(Action<RelinkPostProcessProfile> edit, bool manualBloom = false)
    {
        // 改当前相机实际使用的运行时资源；不写回资源文件，也不改原生 Environment。
        var profiles = GetRelinkProfiles(ResolveCompositor());
        if (profiles.Count == 0) return;
        var dayNight = ResolveDayNight()?.Profile;
        if (manualBloom && dayNight != null) dayNight.DriveRelinkBloomFromProfile = false;
        foreach (var profile in profiles) edit(profile);
        Refresh();
    }

    private void RefreshRelink()
    {
        var profiles = GetRelinkProfiles(ResolveCompositor());
        bool available = profiles.Count > 0;
        _relinkActive.Disabled = _relinkBloom.Disabled = _relinkSky.Disabled = !available;
        _relinkLut.Editable = _relinkExposure.Editable = _relinkIntensity.Editable = _relinkThreshold.Editable = _relinkFloor.Editable = available;
        _relinkHint.Text = available ? "Relink · 修改立即生效，仅用于本次运行。" : "当前相机 / 关卡没有 Relink 后处理。";
        var dayNight = ResolveDayNight()?.Profile;
        _relinkAutomatic.Disabled = !available || dayNight == null;
        _relinkAutomatic.SetPressedNoSignal(dayNight?.DriveRelinkBloomFromProfile == true);
        if (available && dayNight?.DriveRelinkBloomFromProfile == true)
            _relinkHint.Text += " 手动调节泛光将停止昼夜泛光驱动。";
        if (!available) return;
        var post = profiles[0];
        _relinkActive.SetPressedNoSignal(post.Active);
        _relinkBloom.SetPressedNoSignal(post.BloomEnabled);
        _relinkSky.SetPressedNoSignal(post.GradeSky);
        Sync(_relinkLut, post.LutStrength);
        Sync(_relinkExposure, post.Exposure);
        Sync(_relinkIntensity, post.BloomIntensity);
        Sync(_relinkThreshold, post.BloomThreshold);
        Sync(_relinkFloor, post.BloomFloor);
    }

    private static void Sync(SpinBox control, float value)
    {
        // 刷新诊断时保留尚未提交的键盘输入。
        if (!control.GetLineEdit().HasFocus()) control.SetValueNoSignal(value);
    }

    private void Bind(GodotObject source, StringName signal, Callable callback)
    {
        source.Connect(signal, callback);
        _bindings.Add((source, signal, callback));
    }

    public string BuildSnapshot()
    {
        var viewport = Owner.GetViewport();
        var camera = viewport.GetCamera3D();
        var world3D = viewport.FindWorld3D();
        var environment = camera?.Environment ?? world3D?.Environment ?? world3D?.FallbackEnvironment;
        var compositor = ResolveCompositor();
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
                if (effect is DdgiEffect ddgi)
                {
                    using var status = ddgi.GetStatus();
                    text.AppendLine($"    DDGI {status["state"]} · 实例 {status["instances"]} / BLAS {status["blas"]}");
                    if (status.ContainsKey("sky_source"))
                        text.AppendLine($"    探针 {status["probes"]} · 射线/更新 {status["rays_per_update"]} · 天空 {status["sky_source"]}");
                    if (status.ContainsKey("emissive_triangles"))
                        text.AppendLine($"    发光三角形 {status["emissive_triangles"]} · 发光面遮挡查询预算 {status["emissive_shadow_ray_budget"]}/更新");
                    if (status.ContainsKey("lighting_catch_up") && status["lighting_catch_up"].AsBool())
                        text.AppendLine($"    全局光照快速变化：当帧刷新 {status["updated_probes"]} 个探针");
                    if (status.ContainsKey("error")) text.AppendLine($"    {status["error"]}");
                }
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
            text.AppendLine(conflict ? "后处理：Relink Film 与原生 Tonemap / Exposure / Glow / Adjustment 同时启用。" : "后处理：Relink Film → Godot Linear / sRGB 输出。");
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
        foreach (var binding in _bindings)
            if (GodotObject.IsInstanceValid(binding.Source) && binding.Source.IsConnected(binding.Signal, binding.Callback))
                binding.Source.Disconnect(binding.Signal, binding.Callback);
        _bindings.Clear();
        if (GodotObject.IsInstanceValid(Owner)) Owner.RemoveMeta("rendering_gm");
        _world = null;
        _page = null;
        _fps = null;
        _details = null;
        _relinkHint = null;
        _relinkActive = _relinkBloom = _relinkSky = _relinkAutomatic = null;
        _relinkLut = _relinkExposure = _relinkIntensity = _relinkThreshold = _relinkFloor = null;
    }
}
