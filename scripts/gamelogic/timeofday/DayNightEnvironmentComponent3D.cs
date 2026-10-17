using System;
using System.Collections.Generic;
using Framework;
using Godot;

namespace GameLogic;

/// <summary>EC 环境控制器。世界时钟是全局的，灯光与可变材质归当前环境实例所有。</summary>
[Tool, GlobalClass]
public partial class DayNightEnvironmentComponent3D : Component3D, ISerializationListener
{
    // 环境叠加层在刷新前恢复基准值，刷新后再应用，不改写昼夜 Profile。
    public event Action EnvironmentApplying;
    public event Action EnvironmentApplied;
    public override int Priority => ComponentPriority.VFX;
    /// <summary>昼夜视觉与初始日历配置；世界时钟优先使用已载入存档。</summary>
    [Export] public TimeOfDayProfile Profile { get; set; }
    /// <summary>相对 EnvironmentRig 的 WorldEnvironment 路径；天气组件共用此节点。</summary>
    [Export] public NodePath EnvironmentPath { get; set; } = new("WorldEnvironment");
    /// <summary>太阳节点路径；方向随时钟更新，其他光源参数由独立驱动开关决定。</summary>
    [Export] public NodePath SunPath { get; set; } = new("Sun");
    /// <summary>月亮节点路径；天气组件共用同一光源。</summary>
    [Export] public NodePath MoonPath { get; set; } = new("Moon");
    /// <summary>底层云网格路径，配对 Profile.CloudTimeline。</summary>
    [Export] public NodePath CloudsPath { get; set; } = new("OriginalClouds/BaseClouds");
    /// <summary>所有云层的共同旋转父节点，整体旋转只在此处应用。</summary>
    [Export] public NodePath CloudRootPath { get; set; } = new("OriginalClouds");
    /// <summary>高层云网格路径，配对 Profile.HighCloudTimeline。</summary>
    [Export] public NodePath HighCloudsPath { get; set; } = new("OriginalClouds/HighClouds");
    /// <summary>第二层云网格路径，配对 Profile.SecondaryCloudTimeline。</summary>
    [Export] public NodePath SecondaryCloudsPath { get; set; } = new("OriginalClouds/SecondaryClouds");
    /// <summary>按 Profile.StartDay / StartHour 预览天空、日月与云；关闭后不再刷新编辑器预览。</summary>
    [Export] public bool PreviewInEditor { get; set; } = true;

    // 同宿主天气组件复用已解析的节点，避免两套路径指向不同的环境或灯光。
    internal WorldEnvironment SceneWorld => _world;
    internal DirectionalLight3D SunLight => _sun;
    internal DirectionalLight3D MoonLight => _moon;

    private ulong _editorHostId;
    private SkyCloudRadiance _editorCloudRadiance;
    private readonly SkyCloudRadiance _cloudRadiance = new();
    private double _cloudRadianceElapsed;
    private const string EditorPreviewMeta = "yu_environment_preview";

    public override void OnEditorUpdate(GameObject3D host)
    {
        var editor = EditorInterface.Singleton;
        var scene = editor?.GetEditedSceneRoot();
        if (!PreviewInEditor || scene == null || (scene != host && !scene.IsAncestorOf(host)))
        {
            RestoreEditorCameras(host.GetInstanceId());
            return;
        }
        var world = host.GetNodeOrNull<WorldEnvironment>(EnvironmentPath);
        var sun = host.GetNodeOrNull<DirectionalLight3D>(SunPath);
        var moon = host.GetNodeOrNull<DirectionalLight3D>(MoonPath);
        if (Profile == null || world?.Environment?.Sky?.SkyMaterial is not ShaderMaterial sky
            || sun == null || moon == null)
        {
            RestoreEditorCameras(host.GetInstanceId());
            return;
        }
        if (_editorHostId != 0 && _editorHostId != host.GetInstanceId()) RestoreEditorCameras(_editorHostId);
        _editorHostId = host.GetInstanceId();
        double hour = Profile.StartHour;
        Profile.ApplyAtmosphere(world.Environment, sky, hour);
        Profile.ApplyLighting(world.Environment, sun, moon, world.CameraAttributes as CameraAttributesPhysical, hour, Profile.StartLunarPhase);
        Profile.ApplyRelinkPostProcessing(world.Compositor, hour);
        Profile.ApplySky(sky, hour, Profile.StartLunarPhase);
        Camera3D sceneCamera = null;
        var cameras = scene.FindChildren("*", "Camera3D", true, false);
        using (var camerasOwner = (Godot.Collections.Array)cameras)
            foreach (Node node in cameras)
                if (node is Camera3D camera && camera.GetViewport() == host.GetViewport()
                    && (sceneCamera == null || camera.Current)) sceneCamera = camera;
        bool ortho = sceneCamera?.Projection == Camera3D.ProjectionType.Orthogonal;
        float fov = world.Environment.SkyCustomFov;
        Vector3 rotation = world.Environment.SkyRotation;
        if (Profile.DriveSkyProjectionFromProfile)
        {
            fov = ortho ? Mathf.Clamp(Profile.OrthographicSkyFov, 30, 100) : 0;
            rotation = ortho ? new Vector3(Mathf.DegToRad(Profile.OrthographicSkyPitchDegrees), 0, 0) : Vector3.Zero;
            if (!Mathf.IsEqualApprox(world.Environment.SkyCustomFov, fov)) world.Environment.SkyCustomFov = fov;
            if (!world.Environment.SkyRotation.IsEqualApprox(rotation)) world.Environment.SkyRotation = rotation;
        }
        Vector4 cloudTime = Profile.SampleCloudTime(Profile.StartDay * 24d + hour);
        var pivot = host.GetNodeOrNull<Node3D>(CloudRootPath);
        if (pivot != null && Profile.CloudRotationTimeline != null)
            pivot.Rotation = new Vector3(0, Mathf.DegToRad(-Profile.CloudRotationTimeline.Sample("GroupYaw", cloudTime.X)), 0);
        PreviewCloud(host, CloudsPath, Profile.CloudTimeline, cloudTime, fov, rotation.X);
        PreviewCloud(host, HighCloudsPath, Profile.HighCloudTimeline, cloudTime, fov, rotation.X);
        PreviewCloud(host, SecondaryCloudsPath, Profile.SecondaryCloudTimeline, cloudTime, fov, rotation.X);
        var previewSources = new[] { host.GetNodeOrNull<MeshInstance3D>(CloudsPath),
            host.GetNodeOrNull<MeshInstance3D>(HighCloudsPath), host.GetNodeOrNull<MeshInstance3D>(SecondaryCloudsPath) };
        _editorCloudRadiance ??= new SkyCloudRadiance();
        if (!_editorCloudRadiance.Matches(sky, previewSources)) _editorCloudRadiance.Bind(host, sky, previewSources);
        _editorCloudRadiance.Refresh();
        // 原生编辑器相机可覆盖关卡环境和曝光；显式预览同一条渲染链，退出预览时恢复。
        for (int index = 0; index < 4; index++)
        {
            var camera = editor.GetEditorViewport3D(index)?.GetCamera3D();
            if (camera == null || camera.Owner != null) continue;
            // 原始覆盖放在原生相机元数据中，C# 热重载后仍可恢复；不写入场景文件。
            using var metadata = camera.HasMeta(EditorPreviewMeta) ? camera.GetMeta(EditorPreviewMeta) : default;
            using var state = metadata.VariantType == Variant.Type.Dictionary ? metadata.AsGodotDictionary()
                : new Godot.Collections.Dictionary { ["environment"] = camera.Environment,
                    ["compositor"] = camera.Compositor };
            state["owner"] = host.GetInstanceId();
            state["preview_environment"] = world.Environment;
            state["preview_compositor"] = world.Compositor;
            camera.SetMeta(EditorPreviewMeta, state);
            if (camera.Environment != world.Environment) camera.Environment = world.Environment;
            if (camera.Compositor != world.Compositor) camera.Compositor = world.Compositor;
        }
    }

    private void PreviewCloud(GameObject3D host, NodePath path, ReferenceCloudTimeline timeline, Vector4 time, float fov, float pitch)
    {
        var mesh = host.GetNodeOrNull<MeshInstance3D>(path);
        if (mesh == null) return;
        mesh.Visible = Profile.CloudsEnabled;
        if (timeline == null || mesh.MaterialOverride is not ShaderMaterial material) return;
        material.SetShaderParameter("horizon_fade", Profile.CloudHorizonFade);
        material.SetShaderParameter("sky_custom_fov", fov);
        material.SetShaderParameter("sky_pitch", pitch);
        Profile.ApplyCloudMaterial(material, timeline, time);
    }

    private void RestoreEditorCameras(ulong ownerId)
    {
        _editorCloudRadiance?.Release();
        _editorCloudRadiance = null;
        var editor = EditorInterface.Singleton;
        if (editor == null) return;
        for (int index = 0; index < 4; index++)
        {
            var camera = editor.GetEditorViewport3D(index)?.GetCamera3D();
            if (camera == null || !camera.HasMeta(EditorPreviewMeta)) continue;
            using var metadata = camera.GetMeta(EditorPreviewMeta);
            using var state = metadata.AsGodotDictionary();
            if (state["owner"].AsUInt64() != ownerId) continue;
            if (camera.Environment == state["preview_environment"].AsGodotObject()) camera.Environment = state["environment"].As<Godot.Environment>();
            if (state.ContainsKey("attributes") && camera.Attributes == null) camera.Attributes = state["attributes"].As<CameraAttributes>();
            if (camera.Compositor == state["preview_compositor"].AsGodotObject()) camera.Compositor = state["compositor"].As<Compositor>();
            camera.RemoveMeta(EditorPreviewMeta);
        }
        if (_editorHostId == ownerId) _editorHostId = 0;
    }

    public override void OnEditorExit(GameObject3D host)
    {
        RestoreEditorCameras(host.GetInstanceId());
    }

    public override void OnEditorPreSave(GameObject3D host) => OnEditorExit(host);
    public void OnBeforeSerialize() { if (Engine.IsEditorHint()) RestoreEditorCameras(_editorHostId); }
    public void OnAfterDeserialize() { }

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
    private double _lastHours = double.NaN;
    private double _elapsed;
    private double _lastCloudHours = double.NaN;
    private bool _dirty = true;

    public override void OnInit()
    {
        _world = Owner.GetNodeOrNull<WorldEnvironment>(EnvironmentPath);
        _sun = Owner.GetNodeOrNull<DirectionalLight3D>(SunPath);
        _moon = Owner.GetNodeOrNull<DirectionalLight3D>(MoonPath);
        // 保持编辑器配置与当前环境的实时调节分离，时间线资源只读共享。
        Profile = Profile == null ? null : (TimeOfDayProfile)Profile.Duplicate();
        _profile = Profile;
        _cloudRoot = Owner.GetNodeOrNull<Node3D>(CloudRootPath);
        if (_world?.Environment?.Sky?.SkyMaterial is not ShaderMaterial ||
            _sun == null || _moon == null || _profile == null)
        {
            GD.PushError("[DayNightEnvironment] Missing WorldEnvironment / sky shader / Sun / Moon / Profile.");
            return;
        }
        // 节点与材质均来自场景，资源隔离由场景的 Local To Scene 标记完成。
        _environment = _world.Environment;
        RelinkPostProcessEffect.MakeProfilesLocal(_world.Compositor);
        _sky = (ShaderMaterial)_environment.Sky.SkyMaterial;
        _exposure = _world.CameraAttributes as CameraAttributesPhysical;
        // 日月方向在渲染帧更新，不能再由物理帧插值，否则阴影会反复回退。
        _sun.PhysicsInterpolationMode = Node.PhysicsInterpolationModeEnum.Off;
        _moon.PhysicsInterpolationMode = Node.PhysicsInterpolationModeEnum.Off;
        _time = ModuleSystem.GetModule<ITimeOfDayModule>();
        _time.Attach(Owner, _profile);
        _time.Clock.Changed += OnClockChanged;
        Owner.SetMeta("time_of_day", this);
        ConfigureSky();
        ApplyEnvironment();
    }

    public override void OnUpdate(double delta)
    {
        if (_time == null) return;
        // 阴影方向逐帧更新；快进时环境也必须跟上游戏时间，避免 10 Hz 光强台阶。
        WorldTimeState state = _time.Clock.State;
        _profile.ApplyLightDirections(_sun, _moon, state.Hour, state.LunarPhase);
        // 投影切换独立于时钟暂停，不把正交背景构图带到透视相机。
        UpdateSkyProjection();
        // 云形逐帧更新；不受环境光 10 Hz 刷新频率限制。
        UpdateCloudMotion();
        _cloudRadianceElapsed += delta;
        if (_cloudRadianceElapsed >= 1.0 / Mathf.Clamp(_profile.VisualUpdatesPerSecond, 1, 30))
        {
            _cloudRadiance.Refresh();
            _cloudRadianceElapsed = 0;
        }
        _elapsed += delta;
        bool timeAdvanced = Math.Abs(_time.Clock.TotalHours - _lastHours) >= .002;
        if (!_dirty && ((_elapsed < 1.0 / Mathf.Clamp(_profile.VisualUpdatesPerSecond, 1, 30) && !timeAdvanced) || _lastHours == _time.Clock.TotalHours)) return;
        ApplyEnvironment();
    }

    private void ConfigureSky()
    {
        _lastCloudHours = double.NaN;
        // Refresh bindings as well as uniforms when the Inspector replaces a Timeline resource.
        _cloudLayers.Clear();
        BindCloudLayer(CloudsPath, _profile.CloudTimeline);
        BindCloudLayer(HighCloudsPath, _profile.HighCloudTimeline);
        BindCloudLayer(SecondaryCloudsPath, _profile.SecondaryCloudTimeline);
        _cloudRadiance.Bind(Owner, _sky, _cloudLayers.ConvertAll(layer => layer.Mesh));
        // 曝光统一在 ApplyLighting 中按 DriveExposureFromProfile 开关应用。
    }

    private void UpdateSkyProjection()
    {
        var camera = Owner.GetViewport().GetCamera3D();
        if (camera == null) return;
        bool ortho = camera.Projection == Camera3D.ProjectionType.Orthogonal;
        float fov = _environment.SkyCustomFov;
        Vector3 rotation = _environment.SkyRotation;
        if (_profile.DriveSkyProjectionFromProfile)
        {
            fov = ortho ? Mathf.Clamp(_profile.OrthographicSkyFov, 30, 100) : 0;
            rotation = ortho ? new Vector3(Mathf.DegToRad(_profile.OrthographicSkyPitchDegrees), 0, 0) : Vector3.Zero;
            if (!Mathf.IsEqualApprox(_environment.SkyCustomFov, fov)) _environment.SkyCustomFov = fov;
            if (!_environment.SkyRotation.IsEqualApprox(rotation)) _environment.SkyRotation = rotation;
        }
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
        EnvironmentApplying?.Invoke();
        UpdateSkyProjection();
        WorldTimeState state = _time.Clock.State;
        _profile.ApplyAtmosphere(_environment, _sky, state.Hour);
        Vector3 sun = CelestialMath.Direction(state.Hour, 0, _profile.NoonElevation, _profile.Azimuth);
        Vector3 moon = CelestialMath.Direction(state.Hour, state.LunarPhase, _profile.NoonElevation, _profile.Azimuth);
        _profile.ApplyLighting(_environment, _sun, _moon, _exposure, state.Hour, state.LunarPhase);
        _profile.ApplyRelinkPostProcessing(_world.Compositor, state.Hour);
        _profile.ApplySky(_sky, state.Hour, state.LunarPhase);
        foreach (var layer in _cloudLayers)
        {
            layer.Material.SetShaderParameter("sun_direction", sun);
            layer.Material.SetShaderParameter("moon_direction", moon);
        }
        UpdateCloudMotion();
        _lastHours = _time.Clock.TotalHours;
        _elapsed = 0;
        _dirty = false;
        EnvironmentApplied?.Invoke();
        // 天气层先更新原云材质，随后同一帧采样其最终颜色。
        _cloudRadiance.Refresh();
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
        var rotation = _profile.CloudRotationTimeline;
        // Unity's +Y yaw becomes -Y in Godot. Meshes and pivots are authored in the scene.
        // All layers inherit one wind rotation; additional child yaw doubled their speed and distorted parallax.
        if (_cloudRoot != null && rotation != null)
            _cloudRoot.Rotation = new Vector3(0, Mathf.DegToRad(-rotation.Sample("GroupYaw", rotationPhase)), 0);
        foreach (var layer in _cloudLayers)
            _profile.ApplyCloudMaterial(layer.Material, layer.Timeline, cloudTime);
    }

    // Graph 组件动作和远程 Inspector 都可调用这些方法。
    internal void RefreshCloudRadiance() => _cloudRadiance.Refresh();
    public void RefreshProfile() { if (_time != null) { ConfigureSky(); ApplyEnvironment(); } }
    public void SetTimeOfDay(double hour) { if (_time != null) SetDate(_time.Clock.State.Day, hour); }
    public void SetDate(int day, double hour) { _time?.Clock.SetDate(day, hour); ApplyEnvironment(); }
    public void AdvanceHours(double hours) { _time?.Clock.AdvanceHours(hours); ApplyEnvironment(); }
    public void SetLunarPhase(double phase) { _time?.Clock.SetLunarPhase(phase); ApplyEnvironment(); }
    public void SetPaused(bool paused) { if (_time != null) _time.Clock.Paused = paused; }
    public void SetSpeed(double speed) { if (_time != null) _time.Clock.Speed = speed; }
    public void SetRelinkPostEnabled(bool enabled)
    {
        if (_world?.Compositor == null) return;
        var effects = _world.Compositor.CompositorEffects;
        using var effectsOwner = (Godot.Collections.Array)effects;
        foreach (CompositorEffect effect in effects)
            if (effect is RelinkPostProcessEffect relink && relink.Profile != null) relink.Profile.Active = enabled;
        ApplyEnvironment();
    }
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
        _cloudRadiance.Release();
        _cloudRadianceElapsed = 0;
        if (GodotObject.IsInstanceValid(_world) && _world.Compositor != null)
        {
            var effects = _world.Compositor.CompositorEffects;
            using var effectsOwner = (Godot.Collections.Array)effects;
            foreach (CompositorEffect effect in effects)
                if (effect is RelinkPostProcessEffect relink)
                {
                    relink.ReleaseGpuResources();
                }
        }
        if (_time != null)
        {
            _time.Clock.Changed -= OnClockChanged;
            _time.Detach(Owner);
        }
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
