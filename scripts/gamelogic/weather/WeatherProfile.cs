using Godot;

namespace GameLogic;

[GlobalClass]
public partial class WeatherProfile : Resource
{
    [ExportGroup("Schedule (Game Hours)")]
    [Export] public int Seed { get; set; } = 2026;
    [Export(PropertyHint.Range, "0.1,48,0.1")] public double PeriodHours { get; set; } = 4;
    [Export(PropertyHint.Range, "0,2,0.01")] public double TransitionHours { get; set; } = .08;
    [Export(PropertyHint.Range, "0,100,1")] public float ClearWeight { get; set; } = 45;
    [Export(PropertyHint.Range, "0,100,1")] public float OvercastWeight { get; set; } = 25;
    [Export(PropertyHint.Range, "0,100,1")] public float RainWeight { get; set; } = 25;
    [Export(PropertyHint.Range, "0,100,1")] public float SnowWeight { get; set; } = 5;

    [ExportGroup("Atmosphere")]
    [Export(PropertyHint.Range, "0,1,0.01")] public float OvercastAmount { get; set; } = .6f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float RainOvercastAmount { get; set; } = .9f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float SnowOvercastAmount { get; set; } = .75f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float DirectLightLoss { get; set; } = .72f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float SkyLightLoss { get; set; } = .35f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float PrecipitationFog { get; set; } = .18f;
    [Export(PropertyHint.Range, "0.1,1,0.01")] public float FogDistanceScale { get; set; } = .55f;

    [ExportGroup("Precipitation")]
    [Export] public Vector3 Wind { get; set; } = new(1.5f, 0, .4f);
    [Export(PropertyHint.Range, "0,1,0.01")] public float Intensity { get; set; } = .8f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float RainOpacity { get; set; } = .42f;
    [Export(PropertyHint.Range, "1,30,0.5")] public float EmissionHeight { get; set; } = 8;

    [ExportGroup("Wet Surfaces (Game Hours)")]
    [Export] public bool WetSurfacesEnabled { get; set; } = true;
    [Export(PropertyHint.Range, "0.01,8,0.01")] public double WettingHours { get; set; } = .15;
    [Export(PropertyHint.Range, "0.01,24,0.1")] public double DryingHours { get; set; } = 1.5;
    [Export(PropertyHint.Range, "0.01,8,0.01")] public double PuddleBuildHours { get; set; } = .6;
    [Export(PropertyHint.Range, "0.01,24,0.1")] public double PuddleDryingHours { get; set; } = 3;
    [Export(PropertyHint.Range, "0,1,0.01")] public float SnowWetness { get; set; } = .55f;
    [Export(PropertyHint.Range, "0,0.5,0.01")] public float WetDarkening { get; set; } = .24f;
    [Export(PropertyHint.Range, "0.02,0.6,0.01")] public float WetRoughness { get; set; } = .14f;
    [Export(PropertyHint.Range, "0.02,0.3,0.005")] public float PuddleRoughness { get; set; } = .045f;
    /// <summary>沿用 Godot 非金属 specular 语义；0.354 约为物理水的 F0，默认稍增强以适配卡通路面。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float WetSpecular { get; set; } = .6f;
    [Export(PropertyHint.Range, "0,0.8,0.01")] public float PuddleCoverage { get; set; } = .62f;
    [Export(PropertyHint.Range, "0.1,4,0.05")] public float PuddleScale { get; set; } = .85f;

    [ExportGroup("Rain Ripples")]
    [Export] public bool RainRipplesEnabled { get; set; } = true;
    [Export(PropertyHint.Range, "0,0.3,0.01")] public float RippleStrength { get; set; } = .14f;
    [Export(PropertyHint.Range, "0.5,4,0.1")] public float RippleScale { get; set; } = 1.7f;
    [Export(PropertyHint.Range, "0.2,3,0.1")] public float RippleSpeed { get; set; } = 1.35f;

    [ExportGroup("Snow Cover (Game Hours)")]
    [Export] public bool SnowCoverEnabled { get; set; } = true;
    [Export(PropertyHint.Range, "0.01,8,0.01")] public double SnowBuildHours { get; set; } = .45;
    [Export(PropertyHint.Range, "0.01,24,0.1")] public double SnowMeltHours { get; set; } = 3;
    [Export] public Color SnowColor { get; set; } = new(.91f, .95f, 1);
    [Export(PropertyHint.Range, "0.4,1,0.01")] public float SnowRoughness { get; set; } = .86f;

    public WeatherKind Sample(long period) => WeatherSchedule.Sample(period, Seed, ClearWeight, OvercastWeight, RainWeight, SnowWeight);
}
