using System;
using Godot;

namespace GameLogic;

[GlobalClass]
[Tool]
public partial class TimeOfDayProfile : Resource
{
    [ExportGroup("Calendar Defaults")]
    /// <summary>一个游戏日对应的现实秒数；仅初始化世界时钟时使用，已有存档优先。</summary>
    [Export(PropertyHint.Range, "1,864000,1")] public double DayLengthSeconds { get; set; } = 1200;
    /// <summary>新世界初始小时，也用于编辑器预览；运行中通过世界时钟或 F8 改时间。</summary>
    [Export(PropertyHint.Range, "0,23.99,0.01")] public double StartHour { get; set; } = 12;
    /// <summary>新世界初始日期，从 0 开始；也用于编辑器云动画取样。</summary>
    [Export(PropertyHint.Range, "0,1000000,1")] public int StartDay { get; set; }
    /// <summary>月相周期的游戏天数；仅初始化世界时钟时使用，已有存档优先。</summary>
    [Export(PropertyHint.Range, "1,10000,0.01")] public double LunarPeriodDays { get; set; } = 29.53059;
    /// <summary>初始和编辑器预览月相：0 新月、0.25 上弦、0.5 满月、0.75 下弦。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public double StartLunarPhase { get; set; } = 0.5;
    /// <summary>新世界是否暂停时钟；运行中的暂停状态由世界时钟持有。</summary>
    [Export] public bool StartPaused { get; set; }

    [ExportGroup("Scene Environment Controls")]
    /// <summary>显式开启后驱动雾的数值参数；雾的开关和模式始终由 Environment 决定。</summary>
    [Export] public bool DriveFogFromProfile { get; set; } = false;
    /// <summary>仅驱动原生 Adjustment / Glow 的数值，不改效果开关；Relink 泛光使用独立驱动。</summary>
    [Export] public bool DrivePostEffectsFromProfile { get; set; } = false;
    /// <summary>按昼夜曲线更新 Relink 泛光；F8 手动调节泛光后关闭此项，防止下一帧覆盖。</summary>
    [Export] public bool DriveRelinkBloomFromProfile { get; set; } = false;
    /// <summary>开启时按昼夜自动改变天空背景亮度。关闭后可直接修改 Environment 的 Background Intensity，系统不会重写。</summary>
    [Export] public bool DriveSkyBrightnessFromProfile { get; set; } = false;
    /// <summary>驱动物理相机 ISO、光圈、快门和补偿数值；自动曝光开关始终由 CameraAttributes 决定。</summary>
    [Export] public bool DriveExposureFromProfile { get; set; } = false;
    /// <summary>显式开启后驱动日月亮度、颜色和阴影参数；能量倍率和 GI 设置始终保留灯光自身配置。</summary>
    [Export] public bool DriveLightSettingsFromProfile { get; set; } = false;
    /// <summary>显式开启后随相机投影调整天空 FOV 和旋转；默认保留 Environment 的手动配置。</summary>
    [Export] public bool DriveSkyProjectionFromProfile { get; set; } = false;

    [ExportGroup("Sun and Moon")]
    /// <summary>太阳正午仰角，单位度；持续决定日月轨道和昼夜插值。</summary>
    [Export(PropertyHint.Range, "10,89,0.1")] public float NoonElevation { get; set; } = 75;
    /// <summary>日月轨道的水平朝向，单位度；持续更新日月方向。</summary>
    [Export(PropertyHint.Range, "-180,180,0.1")] public float Azimuth { get; set; } = -25;
    /// <summary>正午太阳照度，单位 lux；仅灯光驱动开启时应用。</summary>
    [Export(PropertyHint.Range, "0,200000,100")] public float SunLux { get; set; } = 120000;
    /// <summary>满月最高照度，单位 lux；启用灯光驱动后按月相和高度衰减。</summary>
    [Export(PropertyHint.Range, "0,10,0.01")] public float FullMoonLux { get; set; } = 0.3f;
    /// <summary>月光染色，仅灯光驱动开启时应用。</summary>
    [Export] public Color MoonTint { get; set; } = Colors.White;
    /// <summary>天空太阳盘直径，单位度；阴影软硬由独立的 ShadowAngularDistance 控制。</summary>
    [Export(PropertyHint.Range, "0.1,5,0.01")] public float SunDiameterDegrees { get; set; } = 1.8f;
    /// <summary>天空月盘直径，单位度；不改变月亮光源照度。</summary>
    [Export(PropertyHint.Range, "0.1,5,0.01")] public float MoonDiameterDegrees { get; set; } = 3.0f;

    [ExportGroup("Cartoon Lighting")]
    /// <summary>正午太阳色温（K）：越低越暖金，越高越偏白；日出日落仍平滑过渡到 3000K。 仅灯光驱动开启时应用。</summary>
    [Export(PropertyHint.Range, "3000,7500,50")] public float NoonTemperature { get; set; } = 5600;
    /// <summary>正午保留微暖白光，避免色温、染色与材质亮部三次叠加黄色。 仅灯光驱动开启时应用。</summary>
    [Export] public Color NoonSunTint { get; set; } = new(1f, .99f, .96f);
    /// <summary>阴影光源角度，与天空太阳大小独立。0 使用清晰的 PCF 阴影；大于 0 开启 PCSS，数值越大半影越宽、越容易显出采样噪点。 仅灯光驱动开启时应用。</summary>
    [Export(PropertyHint.Range, "0,2,0.01")] public float ShadowAngularDistance { get; set; } = 0;
    /// <summary>固定阴影边缘过滤宽度。搭配高质量 PCF 小幅柔化边缘，不使用大面积随机半影。 仅灯光驱动开启时应用。</summary>
    [Export(PropertyHint.Range, "0,2,0.05")] public float ShadowFilterBlur { get; set; } = 1f;
    /// <summary>投影深度偏移；过大会使横梁和脚底阴影脱离物体。与法线偏移一起调整。 仅灯光驱动开启时应用。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float ShadowBias { get; set; } = .05f;
    /// <summary>沿表面法线的阴影偏移；小值保留细结构，避免倾斜表面的边界变形。 仅灯光驱动开启时应用。</summary>
    [Export(PropertyHint.Range, "0,4,0.05")] public float ShadowNormalBias { get; set; } = .5f;
    /// <summary>近景阴影覆盖距离（米）；越大覆盖越远，但近景精度越低。 仅灯光驱动开启时应用。</summary>
    [Export(PropertyHint.Range, "10,200,1")] public float ShadowDistance { get; set; } = 55;
    /// <summary>第一段覆盖到角色及近景建筑，避免默认 5.5 米以内的空区域独占最高精度。 仅灯光驱动开启时应用。</summary>
    [Export(PropertyHint.Range, "0.01,0.9,0.01")] public float ShadowSplit1 { get; set; } = .2f;
    /// <summary>第二段级联阴影的距离比例；仅灯光驱动开启时应用，限制在第一段之后。</summary>
    [Export(PropertyHint.Range, "0.02,0.95,0.01")] public float ShadowSplit2 { get; set; } = .4f;
    /// <summary>第三段级联阴影的距离比例；仅灯光驱动开启时应用，限制在第二段之后。</summary>
    [Export(PropertyHint.Range, "0.03,0.99,0.01")] public float ShadowSplit3 { get; set; } = .7f;
    [ExportGroup("Day and Night Post Effects")]
    /// <summary>白天饱和度；仅原生后处理驱动开启时应用，1 为原色。</summary>
    [Export(PropertyHint.Range, "0.5,1.5,0.01")] public float ColorSaturation { get; set; } = 1.08f;

    /// <summary>启用各自驱动后按昼夜插值；关闭时统一使用白天端点，不控制原生效果开关。</summary>
    [Export] public bool BlendNightPostEffects { get; set; }
    /// <summary>夜间原生 Adjustment 饱和度；原生后处理驱动和昼夜混合均开启时使用。</summary>
    [Export(PropertyHint.Range, "0.5,1.5,0.01")] public float NightColorSaturation { get; set; } = 1.1f;
    /// <summary>白天原生 Adjustment 对比度；仅原生后处理驱动开启时应用。</summary>
    [Export(PropertyHint.Range, "0.5,1.5,0.005")] public float DayContrast { get; set; } = 1.04f;
    /// <summary>夜间原生 Adjustment 对比度；原生后处理驱动和昼夜混合均开启时使用。</summary>
    [Export(PropertyHint.Range, "0.5,1.5,0.005")] public float NightContrast { get; set; } = 1.015f;
    /// <summary>仅 DriveRelinkBloomFromProfile 开启时控制 Relink.BloomEnabled；原生 Glow 开关独立。</summary>
    [Export] public bool GlowEnabled { get; set; } = true;
    /// <summary>白天泛光强度端点；原生 Glow 和 Relink 泛光可分别选择由此驱动。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float DayGlowIntensity { get; set; } = .04f;
    /// <summary>夜间泛光强度端点；仅昼夜后处理混合开启时参与插值。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float NightGlowIntensity { get; set; } = .26f;
    /// <summary>白天高亮提取阈值；越大参与泛光的区域越少。</summary>
    [Export(PropertyHint.Range, "0.1,4,0.05")] public float DayGlowThreshold { get; set; } = 1.8f;
    /// <summary>夜间高亮提取阈值；仅昼夜后处理混合开启时参与插值。</summary>
    [Export(PropertyHint.Range, "0.1,4,0.05")] public float NightGlowThreshold { get; set; } = .9f;
    /// <summary>少量低于高光阈值的柔光；白天固定为 0，避免泛白。</summary>
    [Export(PropertyHint.Range, "0,0.2,0.005")] public float NightBloom { get; set; } = .025f;

    // 日月方向持续更新；其他原生参数只在对应驱动开关开启时写入。
    public void ApplyLighting(Godot.Environment environment, DirectionalLight3D sunLight,
        DirectionalLight3D moonLight, CameraAttributesPhysical exposure, double hour, double phase)
    {
        Vector3 sun = CelestialMath.Direction(hour, 0, NoonElevation, Azimuth);
        Vector3 moon = CelestialMath.Direction(hour, phase, NoonElevation, Azimuth);
        float day = CelestialMath.Smooth(-.16f, .25f, sun.Y);
        float linearShutter = Mathf.Lerp(Mathf.Pow(2, NightExposureEv), Mathf.Pow(2, DayExposureEv), day) / 256f;
        float shutter = SampleExposureShutter(sun.Y, linearShutter);
        float noon = CelestialMath.Smooth(0, .35f, sun.Y);
        float moonLightAmount = CelestialMath.Smooth(0, .18f, moon.Y) * (float)((1 - Math.Cos(phase * Math.Tau)) * .5);
        ApplyLightDirection(sunLight, sun);
        ApplyLightDirection(moonLight, moon);
        if (DriveLightSettingsFromProfile)
        {
            sunLight.Visible = sun.Y > 0;
            moonLight.Visible = moon.Y > 0 && moonLightAmount > .001f;
            sunLight.LightIntensityLux = Mathf.Max(0, SunLux) * CelestialMath.Smooth(0, .25f, sun.Y);
            sunLight.LightTemperature = Mathf.Lerp(3000, Mathf.Clamp(NoonTemperature, 3000, 7500), noon);
            sunLight.LightColor = Colors.White.Lerp(NoonSunTint, noon);
            moonLight.LightIntensityLux = Mathf.Max(0, FullMoonLux) * moonLightAmount;
            moonLight.LightTemperature = 8500;
            moonLight.LightColor = MoonTint;
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
        }
        // 静态场景效果默认归 Environment 所有；仅显式开启时才由 Profile 驱动。
        if (DrivePostEffectsFromProfile)
        {
            var post = SamplePostEffects(day);
            environment.AdjustmentSaturation = post.Saturation;
            environment.AdjustmentContrast = post.Contrast;
            environment.GlowIntensity = post.Intensity;
            environment.GlowHdrThreshold = post.Threshold;
            environment.GlowBloom = post.Floor;
        }
        if (DriveSkyBrightnessFromProfile)
        {
            float intensity = Mathf.Lerp(Mathf.Max(.001f, NightSkyIntensity), Mathf.Max(1, DaySkyIntensity), day);
            if (sun.Y < 0)
            {
                float horizon = Mathf.Lerp(Mathf.Max(.001f, NightSkyIntensity), Mathf.Max(1, DaySkyIntensity), CelestialMath.Smooth(-.16f, .25f, 0));
                float night = 1 - CelestialMath.Smooth(-.16f, 0, sun.Y);
                // Twilight luminance falls with solar elevation, independently of camera exposure.
                intensity = Mathf.Exp(Mathf.Lerp(Mathf.Log(horizon), Mathf.Log(Mathf.Max(.001f, NightSkyIntensity)), night));
            }
            environment.BackgroundIntensity = intensity;
        }
        if (DriveExposureFromProfile && exposure != null)
        {
            exposure.ExposureSensitivity = 100;
            exposure.ExposureAperture = 16;
            exposure.ExposureMultiplier = Mathf.Max(.01f, ExposureMultiplier);
            exposure.ExposureShutterSpeed = shutter;
        }
    }

    private float SampleExposureShutter(float sunHeight, float linearShutter)
    {
        if (sunHeight >= 0) return linearShutter;
        float horizonDay = CelestialMath.Smooth(-.16f, .25f, 0);
        float horizonShutter = Mathf.Lerp(Mathf.Pow(2, NightExposureEv), Mathf.Pow(2, DayExposureEv), horizonDay) / 256f;
        float night = 1 - CelestialMath.Smooth(-.16f, 0, sunHeight);
        // 对数域均匀跨越多档曝光，避免满月在暮光最后一分钟被突然放大数十倍。
        return Mathf.Pow(2, Mathf.Lerp(Mathf.Log(horizonShutter * 256) / Mathf.Log(2), NightExposureEv, night)) / 256f;
    }

    public void ApplyCloudMaterial(ShaderMaterial material, ReferenceCloudTimeline timeline, Vector4 cloudTime)
    {
        material.SetShaderParameter("noise_time", cloudTime.Z);
        material.SetShaderParameter("cloud_sdf", timeline.Sample("_Cloud_SDF_TSb", cloudTime.Y, .003f));
        material.SetShaderParameter("cloud_color_a", timeline.SampleColor("_CloudColorA", cloudTime.W));
        material.SetShaderParameter("cloud_color_b", timeline.SampleColor("_CloudColorB", cloudTime.W));
        material.SetShaderParameter("cloud_color_c", timeline.SampleColor("_CloudColorC", cloudTime.W));
        material.SetShaderParameter("cloud_color_d", timeline.SampleColor("_CloudColorD", cloudTime.W));
        material.SetShaderParameter("cloud_edge_color", timeline.SampleColor("_Cloud_edgeColor", cloudTime.W));
        material.SetShaderParameter("sun_moon", timeline.Sample("_SunMoon", cloudTime.W));
    }

    public void ApplyRelinkPostProcessing(Compositor compositor, double hour)
    {
        if (compositor == null || !DriveRelinkBloomFromProfile) return;
        float day = CelestialMath.Smooth(-.16f, .25f, CelestialMath.Direction(hour, 0, NoonElevation, Azimuth).Y);
        var values = SamplePostEffects(day);
        var effects = compositor.CompositorEffects;
        using var effectsOwner = (Godot.Collections.Array)effects;
        foreach (CompositorEffect effect in effects)
        {
            if (effect is not RelinkPostProcessEffect relink || relink.Profile == null) continue;
            var post = relink.Profile;
            post.BloomEnabled = GlowEnabled;
            post.BloomIntensity = values.Intensity;
            post.BloomThreshold = values.Threshold;
            post.BloomFloor = values.Floor;
        }
    }

    // 两种后处理可选择共用这组昼夜端点，分别由独立驱动开关启用。
    private (float Saturation, float Contrast, float Intensity, float Threshold, float Floor) SamplePostEffects(float day)
    {
        float blend = BlendNightPostEffects ? day : 1;
        return (
            Mathf.Lerp(Mathf.Clamp(NightColorSaturation, .5f, 1.5f), Mathf.Clamp(ColorSaturation, .5f, 1.5f), blend),
            Mathf.Lerp(Mathf.Clamp(NightContrast, .5f, 1.5f), Mathf.Clamp(DayContrast, .5f, 1.5f), blend),
            Mathf.Lerp(Mathf.Clamp(NightGlowIntensity, 0, 1), Mathf.Clamp(DayGlowIntensity, 0, 1), blend),
            Mathf.Lerp(Mathf.Clamp(NightGlowThreshold, .1f, 4), Mathf.Clamp(DayGlowThreshold, .1f, 4), blend),
            Mathf.Clamp(NightBloom, 0, .2f) * (1 - blend));
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
    /// <summary>正交相机的天空等效视野角，单位度；仅天空投影驱动开启时应用。</summary>
    [Export(PropertyHint.Range, "30,100,0.1")] public float OrthographicSkyFov { get; set; } = 65;
    /// <summary>正交相机的天空俯仰角，单位度；仅天空投影驱动开启时应用。</summary>
    [Export(PropertyHint.Range, "-80,80,0.1")] public float OrthographicSkyPitchDegrees { get; set; } = -33;
    /// <summary>白天天顶颜色，持续写入自定义天空材质；与背景亮度独立。</summary>
    [Export] public Color DayZenith { get; set; } = new(0.025f, 0.15f, 0.65f);
    /// <summary>白天地平线颜色，持续写入自定义天空材质。</summary>
    [Export] public Color DayHorizon { get; set; } = new(0.12f, 0.58f, 1.0f);
    /// <summary>白天水平面以下的天空颜色，也参与环境辐照。</summary>
    [Export] public Color DayGround { get; set; } = new(0.30f, 0.66f, 0.88f);
    /// <summary>日出日落的地平线颜色，按太阳高度平滑混合。</summary>
    [Export] public Color SunsetHorizon { get; set; } = new(1f, 0.44f, 0.22f);
    /// <summary>夜间天顶颜色；不代替 Environment 的背景亮度。</summary>
    [Export] public Color NightZenith { get; set; } = new(0.07f, 0.11f, 0.29f);
    /// <summary>夜间地平线颜色，持续写入自定义天空材质。</summary>
    [Export] public Color NightHorizon { get; set; } = new(0.20f, 0.25f, 0.44f);
    /// <summary>白天背景亮度端点；仅天空亮度驱动开启时应用。</summary>
    [Export(PropertyHint.Range, "1,100000,1")] public float DaySkyIntensity { get; set; } = 26000;
    /// <summary>夜间背景亮度端点；仅天空亮度驱动开启时应用，晨昏使用对数过渡。</summary>
    [Export(PropertyHint.Range, "0.001,100,0.001")] public float NightSkyIntensity { get; set; } = 0.08f;
    /// <summary>天空星光强度，按天空材质的昼夜权重显示。</summary>
    [Export(PropertyHint.Range, "0,3,0.01")] public float StarIntensity { get; set; } = 1;
    /// <summary>天空月晕强度，不改变月光照度。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float MoonHaloStrength { get; set; }

    // 天空配色、月相与星光共用一处，编辑器跳时与游戏保持一致。
    public void ApplySky(ShaderMaterial sky, double hour, double phase)
    {
        Vector3 sun = CelestialMath.Direction(hour, 0, NoonElevation, Azimuth);
        Vector3 moon = CelestialMath.Direction(hour, phase, NoonElevation, Azimuth);
        float directMoon = CelestialMath.Smooth(0, .18f, moon.Y) * (float)((1 - Math.Cos(phase * Math.Tau)) * .5);
        // 天空辐照持续存在；昼夜亮度由 BackgroundIntensity 和曝光共同控制。
        sky.SetShaderParameter("ibl_strength", 1f);
        sky.SetShaderParameter("day_zenith", DayZenith);
        sky.SetShaderParameter("day_horizon", DayHorizon);
        sky.SetShaderParameter("day_ground", DayGround);
        sky.SetShaderParameter("sunset_horizon", SunsetHorizon);
        sky.SetShaderParameter("night_zenith", NightZenith);
        sky.SetShaderParameter("night_horizon", NightHorizon);
        sky.SetShaderParameter("sun_radius", Mathf.DegToRad(Mathf.Clamp(SunDiameterDegrees, .1f, 5) * .5f));
        sky.SetShaderParameter("moon_radius", Mathf.DegToRad(Mathf.Clamp(MoonDiameterDegrees, .1f, 5) * .5f));
        sky.SetShaderParameter("star_intensity", Mathf.Clamp(StarIntensity, 0, 3));
        sky.SetShaderParameter("moon_halo_strength", Mathf.Clamp(MoonHaloStrength, 0, 1));
        sky.SetShaderParameter("sun_direction", sun);
        sky.SetShaderParameter("moon_direction", moon);
        sky.SetShaderParameter("daylight", CelestialMath.Smooth(-.16f, .25f, sun.Y));
        sky.SetShaderParameter("twilight", (1 - CelestialMath.Smooth(.03f, .45f, Mathf.Abs(sun.Y))) * CelestialMath.Smooth(-.22f, .04f, sun.Y));
        sky.SetShaderParameter("moon_visibility", CelestialMath.Smooth(-.015f, .03f, moon.Y));
        sky.SetShaderParameter("stars_visibility", (1 - CelestialMath.Smooth(-.20f, -.04f, sun.Y)) * (1 - directMoon * .5f));
        sky.SetShaderParameter("star_rotation", (float)(hour / 24 * Math.Tau));
        sky.SetShaderParameter("moon_light", directMoon);
    }

    [ExportGroup("Atmosphere and Aerial Perspective")]
    /// <summary>自定义天空地平线雾霭强度，持续应用；与原生 Fog 开关独立。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float HorizonHaze { get; set; } = 0.45f;
    /// <summary>自定义天空地平线过渡宽度；不改变原生雾参数。</summary>
    [Export(PropertyHint.Range, "0.05,0.8,0.01")] public float HorizonSoftness { get; set; } = 0.3f;
    /// <summary>自定义天空太阳方向的米氏散射强度，持续应用。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float MieStrength { get; set; } = 0.18f;
    /// <summary>米氏散射前向集中程度，越大越集中在太阳附近。</summary>
    [Export(PropertyHint.Range, "0,0.95,0.01")] public float MieAnisotropy { get; set; } = 0.76f;
    /// <summary>白天雾起始距离，单位米；仅雾驱动开启时应用，Depth 模式生效。</summary>
    [Export(PropertyHint.Range, "0,500,0.5")] public float FogStart { get; set; } = 24f;
    /// <summary>白天雾结束距离，单位米；仅雾驱动开启时应用，限制在起始距离之后。</summary>
    [Export(PropertyHint.Range, "1,2000,1")] public float FogEnd { get; set; } = 180f;
    /// <summary>Depth 雾距离衰减曲线；仅雾驱动开启时应用。</summary>
    [Export(PropertyHint.Range, "0.1,4,0.05")] public float FogCurve { get; set; } = 1.15f;
    /// <summary>白天原生雾密度；仅雾驱动开启时应用，语义由 Environment.FogMode 决定。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float FogOpacity { get; set; } = .65f;
    /// <summary>高度雾参考高度，单位米；仅雾驱动开启时应用。</summary>
    [Export] public float FogHeight { get; set; } = -9f;
    /// <summary>白天高度雾密度；仅雾驱动开启时应用。</summary>
    [Export(PropertyHint.Range, "0,0.5,0.001")] public float FogHeightDensity { get; set; } = 0.025f;
    /// <summary>仅 DriveFogFromProfile 开启时生效；原 Fog 参数作为白天端点。</summary>
    [Export] public bool BlendNightFog { get; set; }
    /// <summary>夜间 Depth 雾起始距离，单位米；雾驱动和昼夜雾混合均开启时使用。</summary>
    [Export(PropertyHint.Range, "0,500,0.5")] public float NightFogStart { get; set; } = 14f;
    /// <summary>夜间 Depth 雾结束距离，单位米；雾驱动和昼夜雾混合均开启时使用。</summary>
    [Export(PropertyHint.Range, "1,2000,1")] public float NightFogEnd { get; set; } = 150f;
    /// <summary>夜间原生雾密度；雾驱动和昼夜雾混合均开启时使用。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float NightFogOpacity { get; set; } = .7f;
    /// <summary>夜间高度雾密度；雾驱动和昼夜雾混合均开启时使用。</summary>
    [Export(PropertyHint.Range, "0,0.5,0.001")] public float NightFogHeightDensity { get; set; } = .016f;
    /// <summary>原网格云靠近地平线时的淡出宽度；与原生雾独立。</summary>
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
        environment.FogDensity = Mathf.Clamp(BlendNightFog ? Mathf.Lerp(NightFogOpacity, FogOpacity, day) : FogOpacity, 0, 1);
        environment.FogDepthBegin = Mathf.Max(0, BlendNightFog ? Mathf.Lerp(NightFogStart, FogStart, day) : FogStart);
        environment.FogDepthEnd = Mathf.Max(environment.FogDepthBegin + 1, BlendNightFog ? Mathf.Lerp(NightFogEnd, FogEnd, day) : FogEnd);
        environment.FogDepthCurve = Mathf.Clamp(FogCurve, .1f, 4);
        environment.FogHeight = FogHeight;
        environment.FogHeightDensity = Mathf.Clamp(BlendNightFog ? Mathf.Lerp(NightFogHeightDensity, FogHeightDensity, day) : FogHeightDensity, 0, .5f);
    }

    [ExportGroup("Original Cloud Timeline")]
    /// <summary>三层网格云及其天空反射开关。</summary>
    [Export] public bool CloudsEnabled { get; set; } = true;
    /// <summary>底层云材质曲线；颜色、SDF、噪声分别使用对应时间相位。</summary>
    [Export] public ReferenceCloudTimeline CloudTimeline { get; set; }
    /// <summary>高层云材质曲线，共用世界时钟但独立取样。</summary>
    [Export] public ReferenceCloudTimeline HighCloudTimeline { get; set; }
    /// <summary>第二层云材质曲线；整体旋转由共同父节点处理。</summary>
    [Export] public ReferenceCloudTimeline SecondaryCloudTimeline { get; set; }
    /// <summary>云共同父节点的 GroupYaw 曲线，三层云通过父节点同步转动。</summary>
    [Export] public ReferenceCloudTimeline CloudRotationTimeline { get; set; }
    /// <summary>整体云旋转周期，单位游戏小时；默认日长 20 分钟时，1.6 小时对应原作 80 秒。</summary>
    [Export(PropertyHint.Range, "0.01,240,0.01")] public double CloudRotationPeriodHours { get; set; } = 1.6;
    /// <summary>云形 SDF 变化周期，单位游戏小时；独立于整体旋转。</summary>
    [Export(PropertyHint.Range, "0.01,240,0.01")] public double CloudEvolutionPeriodHours { get; set; } = 1.6;
    /// <summary>云纹理噪声周期，单位游戏小时；暂停、快进、回退均跟随世界时钟。</summary>
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

    [ExportGroup("Camera Exposure (Optional Driver)")]
    /// <summary>白天 EV100，越大越暗；仅曝光驱动开启时应用，ISO 固定 100、光圈固定 f/16。</summary>
    [Export(PropertyHint.Range, "-5,20,0.1")] public float DayExposureEv { get; set; } = 15.38f;
    /// <summary>夜间 EV100，越大越暗；仅曝光驱动开启时应用，晨昏平滑插值。</summary>
    [Export(PropertyHint.Range, "-5,20,0.1")] public float NightExposureEv { get; set; } = -1.97f;
    /// <summary>相机曝光补偿倍率；仅曝光驱动开启时应用，与 Relink.Exposure 独立。</summary>
    [Export(PropertyHint.Range, "0.1,4,0.01")] public float ExposureMultiplier { get; set; } = 1.2f;
    [ExportGroup("Update Quality")]
    /// <summary>环境数值与云辐照采样每秒最大刷新次数；日月方向和云形仍逐帧更新。</summary>
    [Export(PropertyHint.Range, "1,30,1")] public float VisualUpdatesPerSecond { get; set; } = 10;
}
