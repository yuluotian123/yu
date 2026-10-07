using System;
using Godot;

namespace GameLogic;

[GlobalClass]
[Tool]
public partial class TimeOfDayProfile : Resource
{
    [ExportGroup("Calendar Defaults")]
    [Export(PropertyHint.Range, "1,864000,1")] public double DayLengthSeconds { get; set; } = 1200;
    [Export(PropertyHint.Range, "0,23.99,0.01")] public double StartHour { get; set; } = 12;
    [Export(PropertyHint.Range, "0,1000000,1")] public int StartDay { get; set; }
    [Export(PropertyHint.Range, "1,10000,0.01")] public double LunarPeriodDays { get; set; } = 29.53059;
    [Export(PropertyHint.Range, "0,1,0.01")] public double StartLunarPhase { get; set; } = 0.5;
    [Export] public bool StartPaused { get; set; }

    [ExportGroup("Scene Environment Controls")]
    /// <summary>开启时将 Profile 的雾参数写入 Environment。默认关闭，可直接在 WorldEnvironment 的 Environment 中调整雾，编辑器和运行时均保留场景值。</summary>
    [Export] public bool DriveFogFromProfile { get; set; } = false;
    /// <summary>开启时由 Profile 控制 SSAO、Adjustment 开关和饱和度。默认关闭，这些静态效果直接在 Environment 中编辑。</summary>
    [Export] public bool DrivePostEffectsFromProfile { get; set; } = false;
    /// <summary>开启时按昼夜自动改变天空背景亮度。关闭后可直接修改 Environment 的 Background Intensity，系统不会重写。</summary>
    [Export] public bool DriveSkyBrightnessFromProfile { get; set; } = true;
    /// <summary>开启时按昼夜自动曝光。关闭后 WorldEnvironment 的 Camera Attributes 可直接手动调整，系统不会重写曝光、快门或光圈。</summary>
    [Export] public bool DriveExposureFromProfile { get; set; } = true;

    [ExportGroup("Sun and Moon")]
    [Export(PropertyHint.Range, "10,89,0.1")] public float NoonElevation { get; set; } = 75;
    [Export(PropertyHint.Range, "-180,180,0.1")] public float Azimuth { get; set; } = -25;
    [Export(PropertyHint.Range, "0,200000,100")] public float SunLux { get; set; } = 120000;
    [Export(PropertyHint.Range, "0,10,0.01")] public float FullMoonLux { get; set; } = 0.3f;
    /// <summary>主光反弹贡献；由 SDFGI 产生间接光，不用自发光抬亮整个材质。</summary>
    [Export(PropertyHint.Range, "0,2,0.05")] public float SunIndirectEnergy { get; set; } = .75f;
    [Export(PropertyHint.Range, "0,2,0.05")] public float MoonIndirectEnergy { get; set; } = 1f;
    [Export(PropertyHint.Range, "0.1,5,0.01")] public float SunDiameterDegrees { get; set; } = 1.8f;
    [Export(PropertyHint.Range, "0.1,5,0.01")] public float MoonDiameterDegrees { get; set; } = 3.0f;

    [ExportGroup("Cartoon Lighting")]
    /// <summary>正午太阳色温（K）：越低越暖金，越高越偏白；日出日落仍平滑过渡到 3000K。</summary>
    [Export(PropertyHint.Range, "3000,7500,50")] public float NoonTemperature { get; set; } = 5600;
    /// <summary>正午保留微暖白光，避免色温、染色与材质亮部三次叠加黄色。</summary>
    [Export] public Color NoonSunTint { get; set; } = new(1f, .99f, .96f);
    /// <summary>阴影光源角度，与天空太阳大小独立。0 使用清晰的 PCF 阴影；大于 0 开启 PCSS，数值越大半影越宽、越容易显出采样噪点。</summary>
    [Export(PropertyHint.Range, "0,2,0.01")] public float ShadowAngularDistance { get; set; } = 0;
    /// <summary>固定阴影边缘过滤宽度。搭配高质量 PCF 小幅柔化边缘，不使用大面积随机半影。</summary>
    [Export(PropertyHint.Range, "0,2,0.05")] public float ShadowFilterBlur { get; set; } = 1f;
    /// <summary>投影深度偏移；过大会使横梁和脚底阴影脱离物体。与法线偏移一起调整。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float ShadowBias { get; set; } = .05f;
    /// <summary>沿表面法线的阴影偏移；小值保留细结构，避免倾斜表面的边界变形。</summary>
    [Export(PropertyHint.Range, "0,4,0.05")] public float ShadowNormalBias { get; set; } = .5f;
    /// <summary>近景阴影覆盖距离（米）；越大覆盖越远，但近景精度越低。</summary>
    [Export(PropertyHint.Range, "10,200,1")] public float ShadowDistance { get; set; } = 55;
    /// <summary>第一段覆盖到角色及近景建筑，避免默认 5.5 米以内的空区域独占最高精度。</summary>
    [Export(PropertyHint.Range, "0.01,0.9,0.01")] public float ShadowSplit1 { get; set; } = .2f;
    [Export(PropertyHint.Range, "0.02,0.95,0.01")] public float ShadowSplit2 { get; set; } = .4f;
    [Export(PropertyHint.Range, "0.03,0.99,0.01")] public float ShadowSplit3 { get; set; } = .7f;
    /// <summary>仅 DrivePostEffectsFromProfile 开启时控制 SSAO；否则直接编辑 Environment 的 SSAO。</summary>
    [Export] public bool ContactOcclusionEnabled { get; set; } = false;
    /// <summary>仅 DrivePostEffectsFromProfile 开启时控制饱和度；否则直接编辑 Environment 的 Adjustment。1 为原色。</summary>
    [Export(PropertyHint.Range, "0.5,1.5,0.01")] public float ColorSaturation { get; set; } = 1.08f;

    // 编辑器预览与运行时共用，防止运行后被旧色温或太阳盘大小覆盖。
    public void ApplyLighting(Godot.Environment environment, DirectionalLight3D sunLight,
        DirectionalLight3D moonLight, CameraAttributesPhysical exposure, double hour, double phase)
    {
        Vector3 sun = CelestialMath.Direction(hour, 0, NoonElevation, Azimuth);
        Vector3 moon = CelestialMath.Direction(hour, phase, NoonElevation, Azimuth);
        float day = CelestialMath.Smooth(-.16f, .25f, sun.Y);
        float noon = CelestialMath.Smooth(0, .35f, sun.Y);
        float moonLightAmount = CelestialMath.Smooth(0, .18f, moon.Y) * (float)((1 - Math.Cos(phase * Math.Tau)) * .5);
        ApplyLightDirection(sunLight, sun);
        ApplyLightDirection(moonLight, moon);
        sunLight.Visible = sun.Y > 0;
        moonLight.Visible = moon.Y > 0 && moonLightAmount > .001f;
        sunLight.LightIntensityLux = Mathf.Max(0, SunLux) * CelestialMath.Smooth(0, .25f, sun.Y);
        sunLight.LightTemperature = Mathf.Lerp(3000, Mathf.Clamp(NoonTemperature, 3000, 7500), noon);
        sunLight.LightColor = Colors.White.Lerp(NoonSunTint, noon);
        moonLight.LightIntensityLux = Mathf.Max(0, FullMoonLux) * moonLightAmount;
        moonLight.LightTemperature = 8500;
        sunLight.LightIndirectEnergy = Mathf.Clamp(SunIndirectEnergy, 0, 2);
        moonLight.LightIndirectEnergy = Mathf.Clamp(MoonIndirectEnergy, 0, 2);
        sunLight.LightAngularDistance = moonLight.LightAngularDistance = Mathf.Clamp(ShadowAngularDistance, 0, 2);
        sunLight.ShadowBlur = moonLight.ShadowBlur = Mathf.Clamp(ShadowFilterBlur, 0, 2);
        sunLight.ShadowBias = moonLight.ShadowBias = Mathf.Clamp(ShadowBias, 0, 1);
        sunLight.ShadowNormalBias = moonLight.ShadowNormalBias = Mathf.Clamp(ShadowNormalBias, 0, 4);
        sunLight.DirectionalShadowMaxDistance = moonLight.DirectionalShadowMaxDistance = Mathf.Clamp(ShadowDistance, 10, 200);
        sunLight.DirectionalShadowMode = moonLight.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits;
        sunLight.DirectionalShadowBlendSplits = moonLight.DirectionalShadowBlendSplits = true;
        sunLight.DirectionalShadowSplit1 = moonLight.DirectionalShadowSplit1 = Mathf.Clamp(ShadowSplit1, .01f, .9f);
        sunLight.DirectionalShadowSplit2 = moonLight.DirectionalShadowSplit2 = Mathf.Clamp(ShadowSplit2, sunLight.DirectionalShadowSplit1 + .01f, .95f);
        sunLight.DirectionalShadowSplit3 = moonLight.DirectionalShadowSplit3 = Mathf.Clamp(ShadowSplit3, sunLight.DirectionalShadowSplit2 + .01f, .99f);
        // 静态场景效果默认归 Environment 所有；仅显式开启时才由 Profile 驱动。
        if (DrivePostEffectsFromProfile)
        {
            environment.SsaoEnabled = ContactOcclusionEnabled;
            environment.AdjustmentEnabled = true;
            environment.AdjustmentSaturation = Mathf.Clamp(ColorSaturation, .5f, 1.5f);
        }
        if (DriveSkyBrightnessFromProfile)
            environment.BackgroundIntensity = Mathf.Lerp(Mathf.Max(.001f, NightSkyIntensity), Mathf.Max(1, DaySkyIntensity), day);
        if (DriveExposureFromProfile)
        {
            exposure.AutoExposureEnabled = false;
            exposure.ExposureSensitivity = 100;
            exposure.ExposureAperture = 16;
            exposure.ExposureMultiplier = Mathf.Max(.01f, ExposureMultiplier);
            // 在曝光分母上插值，避免晨昏闪白；保持完整昼夜动态范围。
            exposure.ExposureShutterSpeed = Mathf.Lerp(Mathf.Pow(2, NightExposureEv), Mathf.Pow(2, DayExposureEv), day) / 256f;
        }
    }

    // 单独逐帧更新投影方向；编辑器和主动跳时仍由 ApplyLighting 应用全部光照。
    public void ApplyLightDirections(DirectionalLight3D sunLight, DirectionalLight3D moonLight, double hour, double phase)
    {
        ApplyLightDirection(sunLight, CelestialMath.Direction(hour, 0, NoonElevation, Azimuth));
        ApplyLightDirection(moonLight, CelestialMath.Direction(hour, phase, NoonElevation, Azimuth));
    }

    private static void ApplyLightDirection(DirectionalLight3D light, Vector3 direction)
    {
        Basis basis = Basis.LookingAt(-direction, Vector3.Up);
        // 暂停和固定编辑器预览时不反复标记灯光变换脏。
        if (light.Basis != basis) light.Basis = basis;
    }

    [ExportGroup("Sky")]
    [Export(PropertyHint.Range, "30,100,0.1")] public float OrthographicSkyFov { get; set; } = 65;
    [Export(PropertyHint.Range, "-80,80,0.1")] public float OrthographicSkyPitchDegrees { get; set; } = -33;
    [Export] public Color DayZenith { get; set; } = new(0.025f, 0.15f, 0.65f);
    [Export] public Color DayHorizon { get; set; } = new(0.12f, 0.58f, 1.0f);
    [Export] public Color DayGround { get; set; } = new(0.30f, 0.66f, 0.88f);
    [Export] public Color SunsetHorizon { get; set; } = new(1f, 0.44f, 0.22f);
    [Export] public Color NightZenith { get; set; } = new(0.07f, 0.11f, 0.29f);
    [Export] public Color NightHorizon { get; set; } = new(0.20f, 0.25f, 0.44f);
    [Export(PropertyHint.Range, "1,100000,1")] public float DaySkyIntensity { get; set; } = 26000;
    [Export(PropertyHint.Range, "0.001,100,0.001")] public float NightSkyIntensity { get; set; } = 0.08f;
    [Export(PropertyHint.Range, "0,3,0.01")] public float StarIntensity { get; set; } = 1;

    [ExportGroup("Atmosphere and Aerial Perspective")]
    [Export(PropertyHint.Range, "0,1,0.01")] public float HorizonHaze { get; set; } = 0.45f;
    [Export(PropertyHint.Range, "0.05,0.8,0.01")] public float HorizonSoftness { get; set; } = 0.3f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float MieStrength { get; set; } = 0.18f;
    [Export(PropertyHint.Range, "0,0.95,0.01")] public float MieAnisotropy { get; set; } = 0.76f;
    [Export] public bool FogEnabled { get; set; } = true;
    [Export(PropertyHint.Range, "0,500,0.5")] public float FogStart { get; set; } = 24f;
    [Export(PropertyHint.Range, "1,2000,1")] public float FogEnd { get; set; } = 180f;
    [Export(PropertyHint.Range, "0.1,4,0.05")] public float FogCurve { get; set; } = 1.15f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float FogOpacity { get; set; } = .65f;
    [Export] public float FogHeight { get; set; } = -9f;
    [Export(PropertyHint.Range, "0,0.5,0.001")] public float FogHeightDensity { get; set; } = 0.025f;
    [Export(PropertyHint.Range, "0,0.5,0.01")] public float CloudHorizonFade { get; set; } = 0.22f;

    // Shared by the runtime EC and editor preview. Fog samples the same sky radiance,
    // so physical exposure and the day/night palette cannot produce a separate fog band.
    public void ApplyAtmosphere(Godot.Environment environment, ShaderMaterial sky, double hour)
    {
        Vector3 sun = CelestialMath.Direction(hour, 0, NoonElevation, Azimuth);
        float day = CelestialMath.Smooth(-.16f, .25f, sun.Y);
        float dusk = (1 - CelestialMath.Smooth(.03f, .45f, Mathf.Abs(sun.Y))) * CelestialMath.Smooth(-.22f, .04f, sun.Y);
        sky.SetShaderParameter("horizon_haze", Mathf.Clamp(HorizonHaze, 0, 1));
        sky.SetShaderParameter("horizon_softness", Mathf.Clamp(HorizonSoftness, .05f, .8f));
        sky.SetShaderParameter("mie_strength", Mathf.Clamp(MieStrength, 0, 1));
        sky.SetShaderParameter("mie_anisotropy", Mathf.Clamp(MieAnisotropy, 0, .95f));
        sky.SetShaderParameter("daylight", day);
        sky.SetShaderParameter("twilight", dusk);
        sky.SetShaderParameter("sun_direction", sun);
        if (!DriveFogFromProfile) return;
        environment.FogEnabled = FogEnabled;
        environment.FogMode = Godot.Environment.FogModeEnum.Depth;
        environment.FogDensity = Mathf.Clamp(FogOpacity, 0, 1);
        environment.FogDepthBegin = Mathf.Max(0, FogStart);
        environment.FogDepthEnd = Mathf.Max(environment.FogDepthBegin + 1, FogEnd);
        environment.FogDepthCurve = Mathf.Clamp(FogCurve, .1f, 4);
        environment.FogHeight = FogHeight;
        environment.FogHeightDensity = Mathf.Clamp(FogHeightDensity, 0, .5f);
        environment.FogAerialPerspective = 1;
        environment.FogSkyAffect = 0;
        // Sky already includes the directional lobe; adding direct sun again overexposes the mist.
        environment.FogSunScatter = 0;
    }

    [ExportGroup("Original Cloud Timeline")]
    [Export] public bool CloudsEnabled { get; set; } = true;
    [Export] public ReferenceCloudTimeline CloudTimeline { get; set; }
    [Export] public ReferenceCloudTimeline HighCloudTimeline { get; set; }
    [Export] public ReferenceCloudTimeline SecondaryCloudTimeline { get; set; }
    [Export] public ReferenceCloudTimeline CloudRotationTimeline { get; set; }
    /// <summary>Game hours per cycle. 1.6 hours retains the original 80-second motion at 20 minutes/day.</summary>
    [Export(PropertyHint.Range, "0.01,240,0.01")] public double CloudRotationPeriodHours { get; set; } = 1.6;
    [Export(PropertyHint.Range, "0.01,240,0.01")] public double CloudEvolutionPeriodHours { get; set; } = 1.6;
    [Export(PropertyHint.Range, "0.01,240,0.01")] public double CloudNoisePeriodHours { get; set; } = 8;

    /// <summary>Shared by gameplay and editor: rotation phase, SDF phase, Unity noise time, color phase.</summary>
    public Vector4 SampleCloudTime(double totalGameHours)
    {
        double hours = Math.Max(0, totalGameHours);
        return new Vector4(
            Cycle(hours, CloudRotationPeriodHours),
            Cycle(hours, CloudEvolutionPeriodHours),
            Cycle(hours, CloudNoisePeriodHours) * 20f,
            (float)((hours + 18) % 24 / 24));
    }

    private static float Cycle(double hours, double periodHours)
    {
        double period = Math.Clamp(periodHours, .01, 240);
        return (float)(hours % period / period);
    }

    // Read older serialized profiles; new saves use game-hour periods only.
    // Their legacy defaults used 1200 real seconds/day, i.e. 50 reference seconds/game hour.
    public override bool _Set(StringName property, Variant value)
    {
        if (property == "CloudRotationPeriodSeconds") { CloudRotationPeriodHours = value.AsDouble() / 50; return true; }
        if (property == "CloudEvolutionPeriodSeconds") { CloudEvolutionPeriodHours = value.AsDouble() / 50; return true; }
        if (property == "PreviewCloudAnimation") return true;
        return false;
    }

    [ExportGroup("Exposure and Quality")]
    [Export(PropertyHint.Range, "-5,20,0.1")] public float DayExposureEv { get; set; } = 15.38f;
    [Export(PropertyHint.Range, "-5,20,0.1")] public float NightExposureEv { get; set; } = -1.97f;
    [Export(PropertyHint.Range, "0.1,4,0.01")] public float ExposureMultiplier { get; set; } = 1.2f;
    [Export(PropertyHint.Range, "1,30,1")] public float VisualUpdatesPerSecond { get; set; } = 10;
}
