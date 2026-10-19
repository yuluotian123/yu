using System;
using System.Collections.Generic;
using Framework;
using Godot;

namespace GameLogic;

/// <summary>环境宿主上的天气层；使用昼夜基准值，避免乘法和雾强度逐帧累积。</summary>
[Tool, GlobalClass]
public partial class WeatherComponent3D : Component3D
{
    public override int Priority => ComponentPriority.VFX - 1;
    /// <summary>统一的天气、降水和地表效果配置；初始化时复制，GM 调整只影响当前环境。</summary>
    [Export] public WeatherProfile Profile { get; set; }
    /// <summary>按世界日历选择天气；关闭后使用初始天气或最近一次手动选择。</summary>
    [Export] public bool Automatic { get; set; } = true;
    /// <summary>显式开启后叠加降水雾参数；Environment 的雾开关和模式保持手动控制。</summary>
    [Export] public bool DriveFogFromWeather { get; set; } = false;
    /// <summary>显式开启后按阴云程度衰减日月照度。</summary>
    [Export] public bool DriveLightsFromWeather { get; set; } = false;
    /// <summary>关闭自动天气时的初始天气；自动模式由种子和绝对游戏时间决定。</summary>
    [Export] public WeatherKind InitialWeather { get; set; } = WeatherKind.Clear;
    /// <summary>包含 Rain / Snow 粒子节点的降水场景路径，相对 EnvironmentRig。</summary>
    [Export] public NodePath PrecipitationPath { get; set; } = new("Precipitation");
    /// <summary>降水发射区跟随目标；缺失时依次使用当前相机和环境宿主的位置。</summary>
    [Export] public NodePath FollowTargetPath { get; set; } = new("../Player");

    public WeatherKind TargetWeather { get; private set; }
    public float TransitionProgress { get; private set; } = 1;
    public float RainAmount => _blend.Y;
    public float SnowAmount => _blend.Z;
    private WorldClock _clock;
    private DayNightEnvironmentComponent3D _dayNight;
    private Godot.Environment _environment;
    private DirectionalLight3D _sun, _moon;
    private ShaderMaterial _sky, _rainDraw, _snowDraw;
    private Node3D _precipitation, _follow;
    private GpuParticles3D _rain, _snow;
    private ParticleProcessMaterial _rainProcess, _snowProcess;
    private readonly List<ShaderMaterial> _cloudMaterials = new();
    private bool _hasBaseline;
    private float _baseFogDensity, _baseFogBegin, _baseFogEnd, _baseSun, _baseMoon;
    // X=阴云，Y=雨，Z=雪；跨天气切换时对当前混合结果插值。
    private Vector3 _blend, _manualFrom;
    private double _manualElapsed, _lastHours;

    public override void OnInit()
    {
        _dayNight = Owner.GetComponent<DayNightEnvironmentComponent3D>();
        _environment = _dayNight?.SceneWorld?.Environment;
        _sun = _dayNight?.SunLight;
        _moon = _dayNight?.MoonLight;
        _precipitation = Owner.GetNodeOrNull<Node3D>(PrecipitationPath);
        _rain = _precipitation?.GetNodeOrNull<GpuParticles3D>("Rain");
        _snow = _precipitation?.GetNodeOrNull<GpuParticles3D>("Snow");
        if (Profile == null || _dayNight == null || _environment?.Sky?.SkyMaterial is not ShaderMaterial sky
            || _sun == null || _moon == null || _rain == null || _snow == null)
        {
            GD.PushError("[Weather] Missing Profile / day-night component / environment / lights / precipitation scene.");
            return;
        }
        _sky = sky;
        Profile = (WeatherProfile)Profile.Duplicate();
        _clock = ModuleSystem.GetModule<ITimeOfDayModule>().Clock;
        _follow = Owner.GetNodeOrNull<Node3D>(FollowTargetPath);
        _rainDraw = (ShaderMaterial)_rain.DrawPass1.SurfaceGetMaterial(0);
        _snowDraw = (ShaderMaterial)_snow.DrawPass1.SurfaceGetMaterial(0);
        _rainProcess = (ParticleProcessMaterial)_rain.ProcessMaterial;
        _snowProcess = (ParticleProcessMaterial)_snow.ProcessMaterial;
        _rain.Visible = _snow.Visible = true;
        _cloudMaterials.Clear();
        foreach (var path in new[] { _dayNight.CloudsPath, _dayNight.HighCloudsPath, _dayNight.SecondaryCloudsPath })
            if (Owner.GetNodeOrNull<MeshInstance3D>(path)?.MaterialOverride is ShaderMaterial material)
                _cloudMaterials.Add(material);
        _dayNight.EnvironmentApplying += RestoreBaseline;
        _dayNight.EnvironmentApplied += CaptureAndApply;
        _clock.Changed += OnClockChanged;
        CaptureBaseline();
        _lastHours = _clock.TotalHours;
        TargetWeather = InitialWeather;
        _blend = Values(InitialWeather);
        if (Automatic) SampleAutomatic();
        FollowPrecipitation();
        ApplyVisuals();
        Owner.SetMeta("weather", this);
    }

    public override void OnUpdate(double delta)
    {
        if (_clock == null) return;
        double elapsedHours = Math.Abs(_clock.TotalHours - _lastHours);
        _lastHours = _clock.TotalHours;
        if (Automatic) SampleAutomatic();
        else if (TransitionProgress < 1)
        {
            _manualElapsed += elapsedHours;
            TransitionProgress = Progress(_manualElapsed, Duration);
            _blend = _manualFrom.Lerp(Values(TargetWeather), Smooth(TransitionProgress));
        }
        // 跟随发射区，已发出的世界空间粒子不会被相机/角色移动拖走。
        FollowPrecipitation();
        float speed = delta > 0 ? (float)Math.Clamp(elapsedHours * _clock.DayLengthSeconds / 24 / delta, 0, 8) : 0;
        _rain.SpeedScale = _snow.SpeedScale = speed;
        ApplyVisuals();
    }

    private double Period => double.IsFinite(Profile.PeriodHours) ? Math.Clamp(Profile.PeriodHours, .1, 48) : 4;
    private double Duration => double.IsFinite(Profile.TransitionHours) ? Math.Clamp(Profile.TransitionHours, 0, Period) : .08;

    private void SampleAutomatic()
    {
        long period = (long)Math.Floor(_clock.TotalHours / Period);
        TargetWeather = Profile.Sample(period);
        TransitionProgress = Progress(_clock.TotalHours - period * Period, Duration);
        _blend = Values(Profile.Sample(Math.Max(0, period - 1))).Lerp(Values(TargetWeather), Smooth(TransitionProgress));
    }

    private Vector3 Values(WeatherKind kind) => kind switch
    {
        WeatherKind.Overcast => new(Mathf.Clamp(Profile.OvercastAmount, 0, 1), 0, 0),
        WeatherKind.Rain => new(Mathf.Clamp(Profile.RainOvercastAmount, 0, 1), 1, 0),
        WeatherKind.Snow => new(Mathf.Clamp(Profile.SnowOvercastAmount, 0, 1), 0, 1),
        _ => Vector3.Zero
    };

    public void SetWeather(int kind, bool immediate = false)
    {
        if (kind < 0 || kind > (int)WeatherKind.Snow) throw new ArgumentOutOfRangeException(nameof(kind));
        if (_clock == null) return;
        Automatic = false;
        TargetWeather = (WeatherKind)kind;
        _manualFrom = _blend;
        _manualElapsed = 0;
        _lastHours = _clock.TotalHours;
        TransitionProgress = immediate || _clock.Paused || _clock.Speed == 0 || Owner.GetTree().Paused || Duration == 0 ? 1 : 0;
        if (TransitionProgress == 1) _blend = Values(TargetWeather);
        ApplyVisuals();
    }

    public void SetAutomatic(bool enabled)
    {
        if (_clock == null) return;
        if (!enabled) { SetWeather((int)TargetWeather); return; }
        Automatic = true;
        SampleAutomatic();
        ApplyVisuals();
    }

    public Godot.Collections.Dictionary GetWeatherState() => new()
    {
        ["automatic"] = Automatic, ["target"] = (int)TargetWeather, ["name"] = TargetWeather.ToString(),
        ["transition"] = TransitionProgress, ["overcast"] = _blend.X, ["rain"] = _blend.Y, ["snow"] = _blend.Z,
        ["next_change_hours"] = _clock == null ? 0 : Period - _clock.TotalHours % Period
    };

    private void OnClockChanged(WorldTimeState _)
    {
        if (Automatic) { SampleAutomatic(); ApplyVisuals(); }
    }

    private void FollowPrecipitation()
    {
        var camera = Owner.GetViewport().GetCamera3D();
        Vector3 focus = GodotObject.IsInstanceValid(_follow) ? _follow.GlobalPosition : camera?.GlobalPosition ?? Owner.GlobalPosition;
        _precipitation.GlobalPosition = focus + Vector3.Up * Mathf.Clamp(Profile.EmissionHeight, 1, 30);
    }

    private void CaptureBaseline()
    {
        _baseFogDensity = _environment.FogDensity;
        _baseFogBegin = _environment.FogDepthBegin;
        _baseFogEnd = _environment.FogDepthEnd;
        _baseSun = _sun.LightIntensityLux;
        _baseMoon = _moon.LightIntensityLux;
        _hasBaseline = true;
    }

    private void RestoreBaseline()
    {
        if (!_hasBaseline || !GodotObject.IsInstanceValid(_environment)) return;
        if (DriveFogFromWeather)
        {
            _environment.FogDensity = _baseFogDensity;
            _environment.FogDepthBegin = _baseFogBegin;
            _environment.FogDepthEnd = _baseFogEnd;
        }
        if (DriveLightsFromWeather)
        {
            if (GodotObject.IsInstanceValid(_sun)) _sun.LightIntensityLux = _baseSun;
            if (GodotObject.IsInstanceValid(_moon)) _moon.LightIntensityLux = _baseMoon;
        }
    }

    private void CaptureAndApply() { CaptureBaseline(); if (IsActive) ApplyVisuals(); }

    private void ApplyVisuals()
    {
        if (!_hasBaseline) return;
        float precipitation = Mathf.Clamp(_blend.Y + _blend.Z, 0, 1);
        if (DriveLightsFromWeather)
        {
            float light = 1 - _blend.X * Mathf.Clamp(Profile.DirectLightLoss, 0, 1);
            _sun.LightIntensityLux = _baseSun * light;
            _moon.LightIntensityLux = _baseMoon * light;
        }
        if (DriveFogFromWeather)
        {
            _environment.FogDensity = Mathf.Clamp(_baseFogDensity + precipitation * Mathf.Clamp(Profile.PrecipitationFog, 0, 1), 0, 1);
            float distance = Mathf.Lerp(1, Mathf.Clamp(Profile.FogDistanceScale, .1f, 1), precipitation);
            _environment.FogDepthBegin = _baseFogBegin * distance;
            _environment.FogDepthEnd = Mathf.Max(_environment.FogDepthBegin + 1, _baseFogEnd * distance);
        }
        _sky.SetShaderParameter("weather_overcast", _blend.X);
        _sky.SetShaderParameter("weather_sky_loss", Mathf.Clamp(Profile.SkyLightLoss, 0, 1));
        SetCloudWeather(_blend.X);
        _rainProcess.Gravity = new Vector3(Profile.Wind.X, -1, Profile.Wind.Z);
        _snowProcess.Gravity = new Vector3(Profile.Wind.X * .2f, -.1f, Profile.Wind.Z * .2f);
        _rainDraw.SetShaderParameter("rain_opacity", Mathf.Clamp(Profile.RainOpacity, 0, 1));
        SetParticles(_rain, _rainDraw, _blend.Y * Mathf.Clamp(Profile.Intensity, 0, 1));
        SetParticles(_snow, _snowDraw, _blend.Z * Mathf.Clamp(Profile.Intensity, 0, 1));
    }

    private void SetCloudWeather(float overcast)
    {
        foreach (var material in _cloudMaterials)
            if (GodotObject.IsInstanceValid(material))
                material.SetShaderParameter("weather_overcast", overcast);
        _dayNight?.RefreshCloudRadiance();
    }

    private static void SetParticles(GpuParticles3D particles, ShaderMaterial material, float intensity)
    {
        particles.AmountRatio = intensity;
        particles.Emitting = intensity > .001f;
        material.SetShaderParameter("weather_intensity", intensity);
    }

    private static float Progress(double elapsed, double duration) => duration <= 0 ? 1 : (float)Math.Clamp(elapsed / duration, 0, 1);
    private static float Smooth(float t) => t * t * (3 - 2 * t);

    public override void OnDestroy()
    {
        if (_dayNight != null)
        {
            _dayNight.EnvironmentApplying -= RestoreBaseline;
            _dayNight.EnvironmentApplied -= CaptureAndApply;
        }
        if (_clock != null) _clock.Changed -= OnClockChanged;
        RestoreBaseline();
        if (GodotObject.IsInstanceValid(_sky)) _sky.SetShaderParameter("weather_overcast", 0f);
        if (_dayNight != null && GodotObject.IsInstanceValid(Owner)) SetCloudWeather(0);
        if (GodotObject.IsInstanceValid(_rain)) { _rain.Emitting = false; _rain.Visible = false; }
        if (GodotObject.IsInstanceValid(_snow)) { _snow.Emitting = false; _snow.Visible = false; }
        if (GodotObject.IsInstanceValid(Owner)) Owner.RemoveMeta("weather");
        _clock = null;
        _dayNight = null;
        _hasBaseline = false;
        _cloudMaterials.Clear();
    }
}
