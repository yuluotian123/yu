using System;
using System.Collections.Generic;
using Framework;
using Godot;

namespace GameLogic;

/// <summary>EC 环境控制器。世界时钟是全局的，灯光与可变材质归当前环境实例所有。</summary>
[GlobalClass]
public partial class DayNightEnvironmentComponent3D : Component3D
{
    public override int Priority => ComponentPriority.VFX;
    [Export] public TimeOfDayProfile Profile { get; set; }
    [Export] public NodePath EnvironmentPath { get; set; } = new("WorldEnvironment");
    [Export] public NodePath SunPath { get; set; } = new("Sun");
    [Export] public NodePath MoonPath { get; set; } = new("Moon");
    [Export] public NodePath CloudsPath { get; set; } = new("OriginalClouds/BaseClouds");
    [Export] public NodePath CloudRootPath { get; set; } = new("OriginalClouds");
    [Export] public NodePath HighCloudsPath { get; set; } = new("OriginalClouds/HighClouds");
    [Export] public NodePath SecondaryCloudsPath { get; set; } = new("OriginalClouds/SecondaryClouds");
    [Export] public bool LightSceneSprites { get; set; } = true;

    private ITimeOfDayModule _time;
    private WorldEnvironment _world;
    private DirectionalLight3D _sun;
    private DirectionalLight3D _moon;
    private Godot.Environment _environment;
    private CameraAttributesPhysical _exposure;
    private ShaderMaterial _sky;
    private readonly List<(MeshInstance3D Mesh, ShaderMaterial Material, ReferenceCloudTimeline Timeline)> _cloudLayers = new();
    private Node3D _cloudRoot;
    private TimeOfDayProfile _profile;
    private readonly Dictionary<SpriteBase3D, bool> _spriteFlags = new();
    private double _lastHours = double.NaN;
    private double _elapsed;
    private double _lastCloudHours = double.NaN;
    private bool _dirty = true;

    public override void OnInit()
    {
        _world = Owner.GetNodeOrNull<WorldEnvironment>(EnvironmentPath);
        _sun = Owner.GetNodeOrNull<DirectionalLight3D>(SunPath);
        _moon = Owner.GetNodeOrNull<DirectionalLight3D>(MoonPath);
        _profile = Profile;
        _cloudRoot = Owner.GetNodeOrNull<Node3D>(CloudRootPath);
        if (_world?.Environment?.Sky?.SkyMaterial is not ShaderMaterial ||
            _world.CameraAttributes is not CameraAttributesPhysical || _sun == null || _moon == null || _profile == null)
        {
            GD.PushError("[DayNightEnvironment] Missing WorldEnvironment / sky shader / Sun / Moon / Profile.");
            return;
        }
        // 节点与材质均来自场景，资源隔离由场景的 Local To Scene 标记完成。
        _environment = _world.Environment;
        _sky = (ShaderMaterial)_environment.Sky.SkyMaterial;
        _exposure = (CameraAttributesPhysical)_world.CameraAttributes;
        // 日月方向在渲染帧更新，不能再由物理帧插值，否则阴影会反复回退。
        _sun.PhysicsInterpolationMode = Node.PhysicsInterpolationModeEnum.Off;
        _moon.PhysicsInterpolationMode = Node.PhysicsInterpolationModeEnum.Off;
        _time = ModuleSystem.GetModule<ITimeOfDayModule>();
        _time.Attach(Owner, _profile);
        _time.Clock.Changed += OnClockChanged;
        Owner.SetMeta("time_of_day", this);
        ConfigureSky();
        if (LightSceneSprites)
        {
            foreach (Node node in (Owner.GetParent() ?? Owner).FindChildren("*", "SpriteBase3D", true, false))
            {
                if (node is not SpriteBase3D sprite) continue;
                _spriteFlags[sprite] = sprite.Shaded;
                sprite.Shaded = true;
            }
        }
        ApplyEnvironment();
    }

    public override void OnUpdate(double delta)
    {
        if (_time == null) return;
        // 阴影投影方向逐帧取世界时钟，天空辐照、雾和曝光仍按配置频率刷新。
        WorldTimeState state = _time.Clock.State;
        _profile.ApplyLightDirections(_sun, _moon, state.Hour, state.LunarPhase);
        // 投影切换独立于时钟暂停，不把正交背景构图带到透视相机。
        UpdateSkyProjection();
        // 云形逐帧更新；不受环境光 10 Hz 刷新频率限制。
        UpdateCloudMotion();
        _elapsed += delta;
        if (!_dirty && (_elapsed < 1.0 / Mathf.Clamp(_profile.VisualUpdatesPerSecond, 1, 30) || _lastHours == _time.Clock.TotalHours)) return;
        ApplyEnvironment();
    }

    private void ConfigureSky()
    {
        _lastCloudHours = double.NaN;
        _sky.SetShaderParameter("day_zenith", _profile.DayZenith);
        _sky.SetShaderParameter("day_horizon", _profile.DayHorizon);
        _sky.SetShaderParameter("day_ground", _profile.DayGround);
        _sky.SetShaderParameter("sunset_horizon", _profile.SunsetHorizon);
        _sky.SetShaderParameter("night_zenith", _profile.NightZenith);
        _sky.SetShaderParameter("night_horizon", _profile.NightHorizon);
        _sky.SetShaderParameter("sun_radius", Mathf.DegToRad(Mathf.Clamp(_profile.SunDiameterDegrees, .1f, 5) * .5f));
        _sky.SetShaderParameter("moon_radius", Mathf.DegToRad(Mathf.Clamp(_profile.MoonDiameterDegrees, .1f, 5) * .5f));
        _sky.SetShaderParameter("star_intensity", Mathf.Max(0, _profile.StarIntensity));
        // Refresh bindings as well as uniforms when the Inspector replaces a Timeline resource.
        _cloudLayers.Clear();
        BindCloudLayer(CloudsPath, _profile.CloudTimeline);
        BindCloudLayer(HighCloudsPath, _profile.HighCloudTimeline);
        BindCloudLayer(SecondaryCloudsPath, _profile.SecondaryCloudTimeline);
        _sun.LightAngularDistance = _profile.ShadowAngularDistance;
        _moon.LightAngularDistance = _profile.ShadowAngularDistance;
        // 曝光统一在 ApplyLighting 中按 DriveExposureFromProfile 开关应用。
    }

    private void UpdateSkyProjection()
    {
        var camera = Owner.GetViewport().GetCamera3D();
        if (camera == null) return;
        bool ortho = camera.Projection == Camera3D.ProjectionType.Orthogonal;
        float fov = ortho ? Mathf.Clamp(_profile.OrthographicSkyFov, 30, 100) : 0;
        Vector3 rotation = ortho ? new Vector3(Mathf.DegToRad(_profile.OrthographicSkyPitchDegrees), 0, 0) : Vector3.Zero;
        if (!Mathf.IsEqualApprox(_environment.SkyCustomFov, fov)) _environment.SkyCustomFov = fov;
        if (!_environment.SkyRotation.IsEqualApprox(rotation)) _environment.SkyRotation = rotation;
        foreach (var layer in _cloudLayers)
        {
            layer.Material.SetShaderParameter("horizon_fade", _profile.CloudHorizonFade);
            layer.Material.SetShaderParameter("sky_custom_fov", fov);
            layer.Material.SetShaderParameter("sky_pitch", rotation.X);
        }
    }

    public void ApplyEnvironment()
    {
        if (_time == null || !GodotObject.IsInstanceValid(_world)) return;
        UpdateSkyProjection();
        WorldTimeState state = _time.Clock.State;
        _profile.ApplyAtmosphere(_environment, _sky, state.Hour);
        Vector3 sun = CelestialMath.Direction(state.Hour, 0, _profile.NoonElevation, _profile.Azimuth);
        Vector3 moon = CelestialMath.Direction(state.Hour, state.LunarPhase, _profile.NoonElevation, _profile.Azimuth);
        float daylight = CelestialMath.Smooth(-.16f, .25f, sun.Y);
        float directMoon = CelestialMath.Smooth(0, .18f, moon.Y) * (float)state.MoonIllumination;
        _profile.ApplyLighting(_environment, _sun, _moon, _exposure, state.Hour, state.LunarPhase);
        _sky.SetShaderParameter("sun_direction", sun);
        _sky.SetShaderParameter("moon_direction", moon);
        _sky.SetShaderParameter("daylight", daylight);
        foreach (var layer in _cloudLayers)
        {
            layer.Material.SetShaderParameter("sun_direction", sun);
            layer.Material.SetShaderParameter("moon_direction", moon);
        }
        _sky.SetShaderParameter("twilight", (1 - CelestialMath.Smooth(.03f, .45f, Mathf.Abs(sun.Y))) * CelestialMath.Smooth(-.22f, .04f, sun.Y));
        _sky.SetShaderParameter("moon_visibility", CelestialMath.Smooth(-.015f, .03f, moon.Y));
        _sky.SetShaderParameter("stars_visibility", (1 - CelestialMath.Smooth(-.20f, -.04f, sun.Y)) * (1 - directMoon * .5f));
        _sky.SetShaderParameter("star_rotation", (float)(state.Hour / 24 * Math.Tau));
        UpdateCloudMotion();
        _sky.SetShaderParameter("moon_light", directMoon);
        _lastHours = _time.Clock.TotalHours;
        _elapsed = 0;
        _dirty = false;
    }

    private MeshInstance3D BindCloudLayer(NodePath path, ReferenceCloudTimeline timeline)
    {
        var mesh = Owner.GetNodeOrNull<MeshInstance3D>(path);
        if (mesh == null) return null;
        mesh.Visible = _profile.CloudsEnabled;
        if (mesh.MaterialOverride is ShaderMaterial material && timeline != null)
            _cloudLayers.Add((mesh, material, timeline));
        else GD.PushWarning($"[DayNightEnvironment] Missing cloud material or Timeline: {path}");
        return mesh;
    }

    private void UpdateCloudMotion()
    {
        if (_lastCloudHours == _time.Clock.TotalHours) return;
        _lastCloudHours = _time.Clock.TotalHours;
        // WorldClock is the only time source. Day length changes the clock rate, not the cloud position.
        Vector4 cloudTime = _profile.SampleCloudTime(_time.Clock.TotalHours);
        float rotationPhase = cloudTime.X;
        float phase = cloudTime.Y;
        var rotation = _profile.CloudRotationTimeline;
        // Unity's +Y yaw becomes -Y in Godot. Meshes and pivots are authored in the scene.
        // All layers inherit one wind rotation; additional child yaw doubled their speed and distorted parallax.
        if (_cloudRoot != null && rotation != null)
            _cloudRoot.Rotation = new Vector3(0, Mathf.DegToRad(-rotation.Sample("GroupYaw", rotationPhase)), 0);
        // Source Timeline 0/1/2/3 seconds = sunrise/noon/sunset/midnight.
        float dayPhase = cloudTime.W;
        foreach (var layer in _cloudLayers)
        {
            var material = layer.Material;
            var timeline = layer.Timeline;
            // Keep the source noise formula, with its phase sampled from the same game calendar.
            material.SetShaderParameter("noise_time", cloudTime.Z);
            material.SetShaderParameter("cloud_sdf", timeline.Sample("_Cloud_SDF_TSb", phase, .003f));
            material.SetShaderParameter("cloud_color_a", timeline.SampleColor("_CloudColorA", dayPhase));
            material.SetShaderParameter("cloud_color_b", timeline.SampleColor("_CloudColorB", dayPhase));
            material.SetShaderParameter("cloud_color_c", timeline.SampleColor("_CloudColorC", dayPhase));
            material.SetShaderParameter("cloud_color_d", timeline.SampleColor("_CloudColorD", dayPhase));
            material.SetShaderParameter("cloud_edge_color", timeline.SampleColor("_Cloud_edgeColor", dayPhase));
            material.SetShaderParameter("sun_moon", timeline.Sample("_SunMoon", dayPhase));
        }
    }

    // Graph 组件动作和远程 Inspector 都可调用这些方法。
    public void RefreshProfile() { if (_time != null) { ConfigureSky(); ApplyEnvironment(); } }
    public void SetTimeOfDay(double hour) { if (_time != null) SetDate(_time.Clock.State.Day, hour); }
    public void SetDate(int day, double hour) { _time?.Clock.SetDate(day, hour); ApplyEnvironment(); }
    public void AdvanceHours(double hours) { _time?.Clock.AdvanceHours(hours); ApplyEnvironment(); }
    public void SetLunarPhase(double phase) { _time?.Clock.SetLunarPhase(phase); ApplyEnvironment(); }
    public void SetPaused(bool paused) { if (_time != null) _time.Clock.Paused = paused; }
    public void SetSpeed(double speed) { if (_time != null) _time.Clock.Speed = speed; }
    public Godot.Collections.Dictionary GetTimeState()
    {
        if (_time == null) return new();
        var state = _time.Clock.State;
        return new() { ["day"] = state.Day, ["hour"] = state.Hour, ["lunar_phase"] = state.LunarPhase,
            ["moon_illumination"] = state.MoonIllumination, ["phase_name"] = state.Phase.ToString(),
            ["paused"] = _time.Clock.Paused, ["speed"] = _time.Clock.Speed };
    }

    private void OnClockChanged(WorldTimeState _) => _dirty = true;

    public override void OnDestroy()
    {
        if (_time != null)
        {
            _time.Clock.Changed -= OnClockChanged;
            _time.Detach(Owner);
        }
        foreach (var entry in _spriteFlags)
            if (GodotObject.IsInstanceValid(entry.Key)) entry.Key.Shaded = entry.Value;
        _spriteFlags.Clear();
        if (GodotObject.IsInstanceValid(Owner)) Owner.RemoveMeta("time_of_day");
        _time = null;
        _world = null;
        _sky = null;
        _cloudLayers.Clear();
        _cloudRoot = null;
        _environment = null;
        _exposure = null;
        _lastHours = double.NaN;
        _lastCloudHours = double.NaN;
        _dirty = true;
    }
}
