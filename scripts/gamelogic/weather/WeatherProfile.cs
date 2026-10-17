using Godot;

namespace GameLogic;

[GlobalClass]
public partial class WeatherProfile : Resource
{
    [ExportGroup("Schedule (Game Hours)")]
    /// <summary>确定性天气种子；相同日历、种子和权重产生相同天气。</summary>
    [Export] public int Seed { get; set; } = 2026;
    /// <summary>自动天气抽取间隔，单位游戏小时；可连续抽到相同天气。</summary>
    [Export(PropertyHint.Range, "0.1,48,0.1")] public double PeriodHours { get; set; } = 4;
    /// <summary>切换时长，单位游戏小时；暂停时手动切换立即生效。</summary>
    [Export(PropertyHint.Range, "0,2,0.01")] public double TransitionHours { get; set; } = .08;
    /// <summary>自动晴天相对权重；权重无需相加为 100，全为 0 时晴天。</summary>
    [Export(PropertyHint.Range, "0,100,1")] public float ClearWeight { get; set; } = 45;
    /// <summary>自动阴天相对权重；0 表示不自动抽到阴天。</summary>
    [Export(PropertyHint.Range, "0,100,1")] public float OvercastWeight { get; set; } = 25;
    /// <summary>自动雨天相对权重；不影响 F8 手动选择。</summary>
    [Export(PropertyHint.Range, "0,100,1")] public float RainWeight { get; set; } = 25;
    /// <summary>自动雪天相对权重；0 表示不自动抽到雪天。</summary>
    [Export(PropertyHint.Range, "0,100,1")] public float SnowWeight { get; set; } = 5;

    [ExportGroup("Atmosphere")]
    /// <summary>阴天天空和云的覆盖程度，0 晴朗、1 完全阴云。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float OvercastAmount { get; set; } = .6f;
    /// <summary>雨天阴云覆盖程度，与降水量独立。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float RainOvercastAmount { get; set; } = .9f;
    /// <summary>雪天阴云覆盖程度，与积雪量独立。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float SnowOvercastAmount { get; set; } = .75f;
    /// <summary>完全阴云时日月照度的衰减比例；仅天气灯光驱动开启时生效。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float DirectLightLoss { get; set; } = .72f;
    /// <summary>阴云对自定义天空的衰减比例；不改 Environment.BackgroundIntensity。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float SkyLightLoss { get; set; } = .35f;
    /// <summary>全量降水叠加的雾密度；仅天气雾驱动开启时生效，不开启原生雾。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float PrecipitationFog { get; set; } = .18f;
    /// <summary>全量降水的 Depth 雾距离比例；仅天气雾驱动开启时生效，越小能见度越低。</summary>
    [Export(PropertyHint.Range, "0.1,1,0.01")] public float FogDistanceScale { get; set; } = .55f;

    [ExportGroup("Precipitation")]
    /// <summary>降水水平风向，使用 X/Z 分量；Y 不参与，云旋转由昼夜配置控制。</summary>
    [Export] public Vector3 Wind { get; set; } = new(1.5f, 0, .4f);
    /// <summary>雨雪总强度，同时影响粒子数量、湿润、积水、积雪的目标量。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float Intensity { get; set; } = .8f;
    /// <summary>单个雨滴的可见度；与粒子数量和地表含水量独立。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float RainOpacity { get; set; } = .42f;
    /// <summary>发射区相对跟随目标的高度，单位米。</summary>
    [Export(PropertyHint.Range, "1,30,0.5")] public float EmissionHeight { get; set; } = 8;

    [ExportGroup("Wet Surfaces (Game Hours)")]
    /// <summary>水膜和积水效果开关，不关闭独立积雪效果。</summary>
    [Export] public bool WetSurfacesEnabled { get; set; } = true;
    /// <summary>湿润形成时间常数，单位游戏小时；经过一个时间常数变化约 63%。</summary>
    [Export(PropertyHint.Range, "0.01,8,0.01")] public double WettingHours { get; set; } = .15;
    /// <summary>停雨后的表面干燥时间常数，单位游戏小时。</summary>
    [Export(PropertyHint.Range, "0.01,24,0.1")] public double DryingHours { get; set; } = 1.5;
    /// <summary>积水形成时间常数，单位游戏小时；通常比湿润更慢。</summary>
    [Export(PropertyHint.Range, "0.01,8,0.01")] public double PuddleBuildHours { get; set; } = .6;
    /// <summary>停雨后的积水蒸发时间常数，单位游戏小时。</summary>
    [Export(PropertyHint.Range, "0.01,24,0.1")] public double PuddleDryingHours { get; set; } = 3;
    /// <summary>雪天相对于同强度雨天的湿润比例。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float SnowWetness { get; set; } = .55f;
    /// <summary>完全湿润时的底色压暗比例。</summary>
    [Export(PropertyHint.Range, "0,0.5,0.01")] public float WetDarkening { get; set; } = .24f;
    /// <summary>完全湿润时的目标粗糙度；保留原本更光滑的表面。</summary>
    [Export(PropertyHint.Range, "0.02,0.6,0.01")] public float WetRoughness { get; set; } = .14f;
    /// <summary>积水区域的目标粗糙度；越低反射越清晰。</summary>
    [Export(PropertyHint.Range, "0.02,0.3,0.005")] public float PuddleRoughness { get; set; } = .045f;
    /// <summary>沿用 Godot 非金属 specular 语义；0.354 约为物理水的 F0，默认稍增强以适配卡通路面。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float WetSpecular { get; set; } = .6f;
    /// <summary>积水可覆盖的局部面积比例；还受实际积水量和朝上法线限制。</summary>
    [Export(PropertyHint.Range, "0,0.8,0.01")] public float PuddleCoverage { get; set; } = .62f;
    /// <summary>世界坐标积水噪声密度，越大分布越密。</summary>
    [Export(PropertyHint.Range, "0.1,4,0.05")] public float PuddleScale { get; set; } = .85f;

    [ExportGroup("Rain Ripples")]
    /// <summary>雨滴水波扰动开关；雨量仍独立控制水膜和积水。</summary>
    [Export] public bool RainRipplesEnabled { get; set; } = true;
    /// <summary>雨滴波纹法线扰动强度；实际幅度再乘雨量。</summary>
    [Export(PropertyHint.Range, "0,0.3,0.01")] public float RippleStrength { get; set; } = .14f;
    /// <summary>世界坐标雨滴波纹密度，越大分布越密。</summary>
    [Export(PropertyHint.Range, "0.5,4,0.1")] public float RippleScale { get; set; } = 1.7f;
    /// <summary>雨滴波纹扩散倍率，使用游戏时间，暂停时保持不动。</summary>
    [Export(PropertyHint.Range, "0.2,3,0.1")] public float RippleSpeed { get; set; } = 1.35f;

    [ExportGroup("Snow Cover (Game Hours)")]
    /// <summary>积雪显示和堆积开关，不改变降雪粒子。</summary>
    [Export] public bool SnowCoverEnabled { get; set; } = true;
    /// <summary>积雪形成时间常数，单位游戏小时。</summary>
    [Export(PropertyHint.Range, "0.01,8,0.01")] public double SnowBuildHours { get; set; } = .45;
    /// <summary>积雪融化时间常数，单位游戏小时；雨天加速融化。</summary>
    [Export(PropertyHint.Range, "0.01,24,0.1")] public double SnowMeltHours { get; set; } = 3;
    /// <summary>积雪覆盖区域的底色。</summary>
    [Export] public Color SnowColor { get; set; } = new(.91f, .95f, 1);
    /// <summary>积雪覆盖区域粗糙度，默认形成柔和反射。</summary>
    [Export(PropertyHint.Range, "0.4,1,0.01")] public float SnowRoughness { get; set; } = .86f;

    public WeatherKind Sample(long period) => WeatherSchedule.Sample(period, Seed, ClearWeight, OvercastWeight, RainWeight, SnowWeight);
}
