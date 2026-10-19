using System;
using System.Collections.Generic;
using Framework;
using Godot;

namespace GameLogic;

/// <summary>管理场景独立的天气表面。湿润、积水、积雪随游戏小时形成和消退。</summary>
[Tool, GlobalClass]
public partial class WeatherSurfaceComponent3D : Component3D
{
    public override int Priority => ComponentPriority.VFX - 2;
    /// <summary>雨雪表面效果的作用范围；配置复用同宿主的天气 Profile，退出时恢复原材质。</summary>
    [Export] public NodePath SurfaceRootPath { get; set; } = new("../RPGTown");
    public float Wetness { get; private set; }
    public float Puddles { get; private set; }
    public float Snow { get; private set; }
    public int MaterialCount => _materials.Count;
    internal IEnumerable<ShaderMaterial> RuntimeMaterials => _materials;
    private WeatherComponent3D _weather;
    private WorldClock _clock;
    private double _lastHours;
    private readonly List<(MeshInstance3D Mesh, int Surface, Material Original)> _bindings = new();
    private readonly Dictionary<Material, Material> _copies = new();
    private readonly List<ShaderMaterial> _materials = new();
    private readonly List<(StandardMaterial3D Material, Color Albedo, float Roughness, float Specular, ShaderMaterial SnowLayer, Material OriginalNextPass)> _standard = new();

    public override void OnInit()
    {
        _weather = Owner.GetComponent<WeatherComponent3D>();
        var scope = Owner.GetNodeOrNull<Node>(SurfaceRootPath);
        if (_weather?.Profile == null || scope == null)
        {
            GD.PushWarning("[WeatherSurfaces] Missing Weather component / SurfaceRootPath.");
            return;
        }
        _clock = ModuleSystem.GetModule<ITimeOfDayModule>().Clock;
        _lastHours = _clock.TotalHours;
        Wetness = Puddles = Snow = 0;
        if (scope is MeshInstance3D rootMesh) BindMesh(rootMesh);
        var meshes = scope.FindChildren("*", "MeshInstance3D", true, false);
        using var meshesOwner = (Godot.Collections.Array)meshes;
        foreach (Node node in meshes)
            if (node is MeshInstance3D mesh) BindMesh(mesh);
        Owner.SetMeta("weather_surfaces", this);
        ApplyMaterials();
    }

    private void BindMesh(MeshInstance3D mesh)
    {
        if (mesh.MaterialOverride != null)
        {
            var copy = CopyMaterial(mesh.MaterialOverride);
            if (copy == null) return;
            _bindings.Add((mesh, -1, mesh.MaterialOverride));
            mesh.MaterialOverride = copy;
            return;
        }
        if (mesh.Mesh == null) return;
        for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
        {
            var copy = CopyMaterial(mesh.GetActiveMaterial(surface));
            if (copy == null) continue;
            _bindings.Add((mesh, surface, mesh.GetSurfaceOverrideMaterial(surface)));
            mesh.SetSurfaceOverrideMaterial(surface, copy);
        }
    }

    private Material CopyMaterial(Material source)
    {
        if (source == null) return null;
        if (_copies.TryGetValue(source, out var existing)) return existing;
        if (source is ShaderMaterial shader)
        {
            string path = shader.Shader?.ResourcePath;
            if (path != "res://assets/shaders/town/town_toon.gdshader"
                && path != "res://assets/shaders/town/leaf_article.gdshader"
                && path != "res://assets/shaders/town/plant_painterly.gdshader") return null;
            var copy = (ShaderMaterial)shader.Duplicate();
            _copies[source] = copy;
            _materials.Add(copy);
            return copy;
        }
        // 透明玻璃、水、无光照特效保持各自材质；标准不透明材质保留纹理和法线。
        if (source is StandardMaterial3D standard && standard.Transparency == BaseMaterial3D.TransparencyEnum.Disabled
            && standard.ShadingMode != BaseMaterial3D.ShadingModeEnum.Unshaded)
        {
            var copy = (StandardMaterial3D)standard.Duplicate();
            // 原生材质保留贴图/法线和 PBR 参数；追加按朝上法线覆盖的独立雪层。
            var snowLayer = new ShaderMaterial
            {
                Shader = GD.Load<Shader>("res://assets/shaders/weather/snow_overlay.gdshader"),
                NextPass = copy.NextPass
            };
            _copies[source] = copy;
            _materials.Add(snowLayer);
            _standard.Add((copy, copy.AlbedoColor, copy.Roughness, copy.MetallicSpecular, snowLayer, copy.NextPass));
            return copy;
        }
        return null;
    }

    public override void OnUpdate(double delta)
    {
        if (_clock == null) return;
        // 回退时不反向积分含水量；保留当前状态，向前推进后继续演化。
        double hours = Math.Max(0, _clock.TotalHours - _lastHours);
        _lastHours = _clock.TotalHours;
        var profile = _weather.Profile;
        float intensity = Mathf.Clamp(profile.Intensity, 0, 1);
        float rain = _weather.RainAmount * intensity;
        float snowfall = _weather.SnowAmount * intensity;
        float snowTarget = profile.SnowCoverEnabled ? snowfall : 0;
        float beforeSnow = Snow;
        Snow = Approach(Snow, snowTarget, hours, snowTarget > Snow
            ? profile.SnowBuildHours : profile.SnowMeltHours / (1 + rain * 2));
        float meltWater = profile.SnowCoverEnabled ? Math.Max(0, beforeSnow - Snow) * .7f : 0;
        float wetTarget = profile.WetSurfacesEnabled ? Mathf.Clamp(rain + snowfall * Mathf.Clamp(profile.SnowWetness, 0, 1), 0, 1) : 0;
        float puddleTarget = profile.WetSurfacesEnabled ? rain : 0;
        Wetness = Approach(Wetness, wetTarget, hours, wetTarget > Wetness ? profile.WettingHours : profile.DryingHours);
        if (profile.WetSurfacesEnabled) Wetness = Mathf.Clamp(Wetness + meltWater, 0, 1);
        Puddles = Approach(Puddles, puddleTarget, hours, puddleTarget > Puddles ? profile.PuddleBuildHours : profile.PuddleDryingHours);
        ApplyMaterials();
    }

    private static float Approach(float current, float target, double hours, double duration)
    {
        double period = double.IsFinite(duration) ? Math.Clamp(duration, .01, 24) : 1;
        return Mathf.Lerp(current, target, (float)(1 - Math.Exp(-hours / period)));
    }

    /// <summary>调试预览；下一次推进游戏时间后继续自然演化。</summary>
    public void SetSurfaceState(float wetness, float puddles)
    {
        if (!float.IsFinite(wetness) || !float.IsFinite(puddles)) throw new ArgumentOutOfRangeException(nameof(wetness));
        Wetness = Mathf.Clamp(wetness, 0, 1);
        Puddles = Mathf.Clamp(puddles, 0, 1);
        ApplyMaterials();
    }

    /// <summary>独立的积雪预览，不改写雨天含水量或共享材质。</summary>
    public void SetSnowState(float amount)
    {
        if (!float.IsFinite(amount)) throw new ArgumentOutOfRangeException(nameof(amount));
        Snow = Mathf.Clamp(amount, 0, 1);
        ApplyMaterials();
    }

    private void ApplyMaterials()
    {
        if (_clock == null) return;
        var profile = _weather.Profile;
        float wetness = profile.WetSurfacesEnabled ? Wetness : 0;
        float puddles = profile.WetSurfacesEnabled ? Puddles : 0;
        float seconds = (float)(_clock.TotalHours * 50 % 1024);
        foreach (var material in _materials)
        {
            material.SetShaderParameter("weather_wetness", wetness);
            material.SetShaderParameter("weather_puddles", puddles);
            material.SetShaderParameter("weather_puddle_coverage", Mathf.Clamp(profile.PuddleCoverage, 0, .8f));
            material.SetShaderParameter("weather_puddle_scale", Mathf.Clamp(profile.PuddleScale, .1f, 4));
            material.SetShaderParameter("weather_darkening", Mathf.Clamp(profile.WetDarkening, 0, .5f));
            material.SetShaderParameter("weather_wet_roughness", Mathf.Clamp(profile.WetRoughness, .02f, .6f));
            material.SetShaderParameter("weather_puddle_roughness", Mathf.Clamp(profile.PuddleRoughness, .02f, .3f));
            material.SetShaderParameter("weather_wet_specular", Mathf.Clamp(profile.WetSpecular, 0, 1));
            material.SetShaderParameter("weather_rain", _weather.RainAmount * Mathf.Clamp(profile.Intensity, 0, 1));
            material.SetShaderParameter("weather_ripple_strength", profile.RainRipplesEnabled ? Mathf.Clamp(profile.RippleStrength, 0, .3f) : 0);
            material.SetShaderParameter("weather_ripple_scale", Mathf.Clamp(profile.RippleScale, .5f, 4));
            material.SetShaderParameter("weather_ripple_speed", Mathf.Clamp(profile.RippleSpeed, .2f, 3));
            material.SetShaderParameter("weather_snow", profile.SnowCoverEnabled ? Snow : 0);
            material.SetShaderParameter("weather_snow_color", profile.SnowColor);
            material.SetShaderParameter("weather_snow_roughness", Mathf.Clamp(profile.SnowRoughness, .4f, 1));
            material.SetShaderParameter("weather_time", seconds);
        }
        foreach (var entry in _standard)
        {
            entry.Material.NextPass = profile.SnowCoverEnabled && Snow > .001f ? entry.SnowLayer : entry.OriginalNextPass;
            float film = (1 - Mathf.Exp(-4 * wetness)) / (1 - Mathf.Exp(-4));
            float factor = 1 - wetness * Mathf.Clamp(profile.WetDarkening, 0, .5f);
            entry.Material.AlbedoColor = new Color(entry.Albedo.R * factor, entry.Albedo.G * factor, entry.Albedo.B * factor, entry.Albedo.A);
            entry.Material.Roughness = Mathf.Lerp(entry.Roughness, Mathf.Min(entry.Roughness, Mathf.Clamp(profile.WetRoughness, .02f, .6f)), film);
            entry.Material.MetallicSpecular = Mathf.Lerp(entry.Specular, Mathf.Clamp(profile.WetSpecular, 0, 1), film);
        }
    }

    public override void OnDestroy()
    {
        foreach (var entry in _bindings)
        {
            if (!GodotObject.IsInstanceValid(entry.Mesh)) continue;
            if (entry.Surface < 0) entry.Mesh.MaterialOverride = entry.Original;
            else entry.Mesh.SetSurfaceOverrideMaterial(entry.Surface, entry.Original);
        }
        _bindings.Clear();
        _copies.Clear();
        _materials.Clear();
        _standard.Clear();
        _clock = null;
        _weather = null;
        Wetness = Puddles = Snow = 0;
        if (GodotObject.IsInstanceValid(Owner)) Owner.RemoveMeta("weather_surfaces");
    }
}
