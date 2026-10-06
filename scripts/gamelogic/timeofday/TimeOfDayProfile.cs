using Godot;

namespace GameLogic;

[GlobalClass]
public partial class TimeOfDayProfile : Resource
{
    [ExportGroup("Calendar Defaults")]
    [Export(PropertyHint.Range, "1,864000,1")] public double DayLengthSeconds { get; set; } = 1200;
    [Export(PropertyHint.Range, "0,23.99,0.01")] public double StartHour { get; set; } = 12;
    [Export(PropertyHint.Range, "0,1000000,1")] public int StartDay { get; set; }
    [Export(PropertyHint.Range, "1,10000,0.01")] public double LunarPeriodDays { get; set; } = 29.53059;
    [Export(PropertyHint.Range, "0,1,0.01")] public double StartLunarPhase { get; set; } = 0.5;
    [Export] public bool StartPaused { get; set; }

    [ExportGroup("Sun and Moon")]
    [Export(PropertyHint.Range, "10,89,0.1")] public float NoonElevation { get; set; } = 75;
    [Export(PropertyHint.Range, "-180,180,0.1")] public float Azimuth { get; set; } = -25;
    [Export(PropertyHint.Range, "0,200000,100")] public float SunLux { get; set; } = 100000;
    [Export(PropertyHint.Range, "0,10,0.01")] public float FullMoonLux { get; set; } = 0.3f;
    [Export(PropertyHint.Range, "0.1,5,0.01")] public float SunDiameterDegrees { get; set; } = 1.8f;
    [Export(PropertyHint.Range, "0.1,5,0.01")] public float MoonDiameterDegrees { get; set; } = 3.0f;

    [ExportGroup("Sky")]
    [Export(PropertyHint.Range, "30,100,0.1")] public float OrthographicSkyFov { get; set; } = 65;
    [Export(PropertyHint.Range, "-80,80,0.1")] public float OrthographicSkyPitchDegrees { get; set; } = -33;
    [Export] public Color DayZenith { get; set; } = new(0.025f, 0.15f, 0.65f);
    [Export] public Color DayHorizon { get; set; } = new(0.12f, 0.58f, 1.0f);
    [Export] public Color DayGround { get; set; } = new(0.30f, 0.66f, 0.88f);
    [Export] public Color SunsetHorizon { get; set; } = new(1f, 0.44f, 0.22f);
    [Export] public Color NightZenith { get; set; } = new(0.07f, 0.11f, 0.29f);
    [Export] public Color NightHorizon { get; set; } = new(0.20f, 0.25f, 0.44f);
    [Export(PropertyHint.Range, "1,100000,1")] public float DaySkyIntensity { get; set; } = 30000;
    [Export(PropertyHint.Range, "0.001,100,0.001")] public float NightSkyIntensity { get; set; } = 0.08f;
    [Export(PropertyHint.Range, "0,3,0.01")] public float StarIntensity { get; set; } = 1;

    [ExportGroup("Original Cloud Timeline")]
    [Export] public bool CloudsEnabled { get; set; } = true;
    [Export] public ReferenceCloudTimeline CloudTimeline { get; set; }
    /// <summary>4-second author Timeline at 0.05 speed; colors follow the existing 20-minute calendar.</summary>
    [Export(PropertyHint.Range, "4,240,1")] public float CloudEvolutionPeriodSeconds { get; set; } = 80;

    [ExportGroup("Exposure and Quality")]
    [Export(PropertyHint.Range, "-5,20,0.1")] public float DayExposureEv { get; set; } = 14.64f;
    [Export(PropertyHint.Range, "-5,20,0.1")] public float NightExposureEv { get; set; } = -3;
    [Export(PropertyHint.Range, "0.1,4,0.01")] public float ExposureMultiplier { get; set; } = 1.2f;
    [Export(PropertyHint.Range, "1,30,1")] public float VisualUpdatesPerSecond { get; set; } = 10;
}
