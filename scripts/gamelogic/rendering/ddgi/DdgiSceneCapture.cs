using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

namespace GameLogic;

internal readonly record struct DdgiSettings(bool Enabled, bool EnvironmentSky,
    Vector3 Origin, Vector3I Counts, float Spacing, int Rays, int Budget, float History,
    float MaxDistance, Vector3 Sky, float Energy, float NormalBias, float ViewBias)
{
    public int Total => Counts.X * Counts.Y * Counts.Z;
    public static DdgiSettings From(GDictionary d) => new(d["enabled"].AsBool(),
        d.ContainsKey("environment_sky") && d["environment_sky"].AsBool(),
        d["origin"].AsVector3(), d["counts"].AsVector3I(), d["spacing"].AsSingle(),
        d["rays"].AsInt32(), d["budget"].AsInt32(), d["history"].AsSingle(),
        d["max_distance"].AsSingle(), d["sky"].AsVector3(), d["energy"].AsSingle(),
        d["normal_bias"].AsSingle(), d["view_bias"].AsSingle());
}

internal sealed record DdgiGeometry(string Key, byte[] Positions, int VertexCount, byte[] Indices, int IndexCount, byte[] Triangles);
internal readonly record struct DdgiInstance(string Key, Transform3D Transform, int Material);
internal sealed class DdgiPacket
{
    public DdgiSettings Settings;
    public float SkyEnergy = float.NaN;
    public readonly Dictionary<string, DdgiGeometry> Geometry = new();
    public readonly List<DdgiInstance> Instances = new();
    public byte[] Materials = System.Array.Empty<byte>(), Lights = System.Array.Empty<byte>(), Emitters = System.Array.Empty<byte>();
    public readonly List<string> TextureKeys = new() { "white" }, DataKeys = new() { "white" };
    public readonly List<byte[]> TextureData = new(), DataTextures = new();
    public readonly Dictionary<string, string> Warnings = new();
    public readonly Dictionary<string, int> MaterialCounts = new();
}

internal static class DdgiBytes
{
    public static byte[] Of<T>(T[] values) where T : unmanaged => MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
    public static void Vector(BinaryWriter writer, Vector3 value, float w = 0)
    { writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z); writer.Write(w); }
    public static void Color(BinaryWriter writer, Color value)
    { writer.Write(value.R); writer.Write(value.G); writer.Write(value.B); writer.Write(value.A); }
}

/// <summary>Main-thread capture produces managed immutable snapshots; nodes never cross to RD.</summary>
internal sealed class DdgiSceneCapture
{
    public const int TextureSize = 128, MaxTextures = 32, MaterialStride = 192, TriangleStride = 176;
    private const string Town = "res://assets/shaders/town/town_toon.gdshader";
    private const string Ground = "res://assets/shaders/town/town_ground.gdshader";
    private const string Plant = "res://assets/shaders/town/plant_painterly.gdshader";
    private const string Leaf = "res://assets/shaders/town/leaf_article.gdshader";
    private sealed record Watch(Resource Resource, Action Changed);
    private readonly Dictionary<ulong, int> _versions = new();
    private readonly Dictionary<ulong, Watch> _watched = new();
    private readonly Dictionary<string, DdgiGeometry> _meshes = new();
    private readonly Dictionary<string, byte[]> _textures = new();
    private readonly HashSet<ulong> _usedResources = new();

    private string Version(Resource resource)
    {
        ulong id = resource.GetInstanceId();
        _usedResources.Add(id);
        if (!_versions.ContainsKey(id))
        {
            _versions[id] = 0;
            Action changed = () => _versions[id] = _versions.GetValueOrDefault(id) + 1;
            _watched[id] = new(resource, changed);
            resource.Changed += changed;
        }
        return $"{id}:{_versions[id]}";
    }

    private DdgiGeometry Geometry(Mesh mesh, int surface)
    {
        string key = $"{Version(mesh)}:{surface}";
        if (_meshes.TryGetValue(key, out var cached)) return cached;
        if (mesh is ArrayMesh arrayMesh)
        {
            if (arrayMesh.SurfaceGetPrimitiveType(surface) != Mesh.PrimitiveType.Triangles) return null;
        }
        else if (mesh is not PrimitiveMesh || mesh is PointMesh) return null;
        using var arrays = mesh.SurfaceGetArrays(surface);
        Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        int[] indices = arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil
            ? Enumerable.Range(0, vertices.Length).ToArray() : arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        if (indices.Length == 0) indices = Enumerable.Range(0, vertices.Length).ToArray();
        if (vertices.Length == 0 || indices.Length == 0 || indices.Length % 3 != 0) return null;
        Vector2[] uv = arrays[(int)Mesh.ArrayType.TexUV].VariantType == Variant.Type.Nil
            ? System.Array.Empty<Vector2>() : arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
        Vector3[] normals = arrays[(int)Mesh.ArrayType.Normal].VariantType == Variant.Type.Nil
            ? System.Array.Empty<Vector3>() : arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
        Color[] colors = arrays[(int)Mesh.ArrayType.Color].VariantType == Variant.Type.Nil
            ? System.Array.Empty<Color>() : arrays[(int)Mesh.ArrayType.Color].AsColorArray();
        using var stream = new MemoryStream(indices.Length / 3 * TriangleStride);
        using var writer = new BinaryWriter(stream);
        for (int t = 0; t < indices.Length; t += 3)
        {
            for (int v = 0; v < 3; v++) DdgiBytes.Vector(writer, vertices[indices[t + v]], 1);
            for (int v = 0; v < 3; v++)
            { Vector2 point = indices[t + v] < uv.Length ? uv[indices[t + v]] : Vector2.Zero; writer.Write(point.X); writer.Write(point.Y); }
            writer.Write(0f); writer.Write(0f);
            Vector3 face = (vertices[indices[t + 2]] - vertices[indices[t]]).Cross(vertices[indices[t + 1]] - vertices[indices[t]]).Normalized();
            for (int v = 0; v < 3; v++) DdgiBytes.Vector(writer, indices[t + v] < normals.Length ? normals[indices[t + v]] : face);
            for (int v = 0; v < 3; v++) DdgiBytes.Color(writer, indices[t + v] < colors.Length ? colors[indices[t + v]] : Colors.White);
        }
        var record = new DdgiGeometry(key, DdgiBytes.Of(vertices), vertices.Length, DdgiBytes.Of(indices), indices.Length, stream.ToArray());
        _meshes[key] = record;
        return record;
    }

    private int Texture(Texture2D texture, DdgiPacket packet, bool linear = false)
    {
        if (texture == null) return 0;
        string key = Version(texture);
        var keys = linear ? packet.DataKeys : packet.TextureKeys;
        var data = linear ? packet.DataTextures : packet.TextureData;
        int index = keys.IndexOf(key);
        if (index >= 0) return index;
        if (keys.Count >= MaxTextures)
        { packet.Warnings["texture_limit"] = "Too many GI textures; affected surfaces are excluded."; return -1; }
        if (!_textures.TryGetValue(key, out byte[] pixels))
        {
            using var original = texture.GetImage();
            if (original == null || original.IsEmpty())
            { packet.Warnings["unreadable_texture"] = "A GI texture has no CPU image; affected surfaces are excluded."; return -1; }
            using var image = (Image)original.Duplicate();
            if (image.IsCompressed() && image.Decompress() != Error.Ok) return -1;
            image.Convert(Image.Format.Rgba8);
            image.ClearMipmaps();
            image.Resize(TextureSize, TextureSize, Image.Interpolation.Bilinear);
            pixels = image.GetData();
            _textures[key] = pixels;
        }
        keys.Add(key); data.Add(pixels);
        return keys.Count - 1;
    }

    private static T Param<[MustBeVariant] T>(ShaderMaterial material, string name, T fallback)
    {
        using Variant value = material.GetShaderParameter(name);
        return value.VariantType == Variant.Type.Nil ? fallback : value.As<T>();
    }

    private byte[] MaterialData(Material source, DdgiPacket packet)
    {
        Color color = Colors.White, snowColor = Colors.White;
        Vector3 emission = Vector3.Zero, average = Vector3.One;
        Vector4 uv = new(1, 1, 0, 0), weather = Vector4.Zero, puddles = Vector4.Zero,
            foliage = Vector4.Zero, lower = Vector4.Zero, upper = Vector4.Zero;
        float metallic = 0, cutoff = -1, saturation = 1, detail = 1;
        bool repeat = true, useTexture = false, vertexColor = false;
        int kind = 0, orm = -1;
        Texture2D texture = null;
        bool physical = ProjectSettings.GetSetting("rendering/lights_and_shadows/use_physical_light_units", false).AsBool();
        if (source is StandardMaterial3D standard)
        {
            if (standard.Transparency != BaseMaterial3D.TransparencyEnum.Disabled && standard.Transparency != BaseMaterial3D.TransparencyEnum.AlphaScissor) return null;
            if (standard.VertexColorUseAsAlbedo || standard.UsePointSize || standard.FixedSize || standard.Uv1Triplanar || standard.MetallicTexture != null || standard.DetailEnabled)
            { packet.Warnings["unsupported_standard"] = "Unsupported StandardMaterial3D features need a GI proxy."; return null; }
            color = standard.AlbedoColor.SrgbToLinear();
            texture = standard.AlbedoTexture; useTexture = texture != null;
            metallic = standard.Metallic; repeat = standard.TextureRepeat;
            uv = new(standard.Uv1Scale.X, standard.Uv1Scale.Y, standard.Uv1Offset.X, standard.Uv1Offset.Y);
            if (standard.Transparency == BaseMaterial3D.TransparencyEnum.AlphaScissor) cutoff = standard.AlphaScissorThreshold;
            if (standard.EmissionEnabled)
            {
                if (standard.EmissionTexture != null)
                { packet.Warnings["emission_texture"] = "Emission textures need a GI proxy."; return null; }
                Color e = standard.Emission.SrgbToLinear();
                emission = new Vector3(e.R, e.G, e.B) * standard.EmissionEnergyMultiplier * (physical ? standard.EmissionIntensity : 1);
            }
        }
        else if (source is ShaderMaterial material && material.Shader != null)
        {
            string path = material.Shader.ResourcePath;
            if (path == Town || path == Ground)
            {
                kind = 1;
                color = Param(material, "base_color", Colors.White).SrgbToLinear();
                useTexture = Param(material, "use_texture", false);
                if (useTexture) texture = Param<Texture2D>(material, "albedo_texture", null);
                metallic = Param(material, "surface_metallic", 0f);
                Vector2 scale = Param(material, "uv_scale", Vector2.One), offset = Param(material, "uv_offset", Vector2.Zero);
                uv = new(scale.X, scale.Y, offset.X, offset.Y);
                Vector3 e = Param(material, "emission_color", Vector3.Zero);
                emission = e * Param(material, "emission_intensity", 1f);
                average = Param(material, "texture_average", new Vector3(.4f, .4f, .4f));
                detail = Param(material, "texture_detail", .62f);
                saturation = Param(material, "color_saturation", 1.02f);
                vertexColor = Param(material, "use_vertex_color", false);
                if (Param(material, "use_orm_texture", false))
                { orm = Texture(Param<Texture2D>(material, "orm_texture", null), packet, true); if (orm < 0) return null; }
                if (Param(material, "emission_luminance_units", false)) emission /= Mathf.Max(e.Dot(new(.2126f, .7152f, .0722f)), 1e-6f);
            }
            else if (path == Plant || path == Leaf)
            {
                kind = path == Plant ? 2 : 3; repeat = false;
                useTexture = kind == 3 || Param(material, "use_leaf_texture", false);
                vertexColor = !useTexture;
                if (useTexture) { texture = Param<Texture2D>(material, "leaf_texture", null); cutoff = Param(material, "alpha_cutoff", kind == 3 ? .5f : .52f); }
                if (kind == 2)
                    foliage = new(Param(material, "palette_strength", .72f), Param(material, "color_saturation", 1.02f), Param(material, "albedo_value", .92f), useTexture ? Param(material, "canopy_normal_blend", .76f) : 0);
                else
                {
                    foliage = new(Param(material, "texture_detail", 0f), Param(material, "normal_roundness", .68f), 0, 0);
                    Vector3 low = Param(material, "lower_color", new Vector3(.110f, .230f, .150f)), high = Param(material, "upper_color", new Vector3(.270f, .460f, .180f));
                    lower = new(low.X, low.Y, low.Z, Param(material, "gradient_strength", .60f));
                    upper = new(high.X, high.Y, high.Z, Param(material, "vertex_color_strength", .40f));
                    if (Mathf.Abs(Param(material, "wind_strength", .018f)) > 0)
                        packet.Warnings["leaf_rest_pose"] = "Leaf GI uses alpha-tested rest-pose geometry; vertex wind is not traced.";
                }
            }
            else
            {
                packet.Warnings["unsupported_shader"] = "Custom shaders require the explicit DDGI material protocol or an opaque proxy.";
                return null;
            }
            if (kind > 0)
            {
                weather = new(Param(material, "weather_wetness", 0f), Param(material, "weather_puddles", 0f), Param(material, "weather_snow", 0f), Param(material, "weather_darkening", .24f));
                puddles = new(Param(material, "weather_puddles_enabled", false) ? 1 : 0, Param(material, "weather_puddle_coverage", .55f), Param(material, "weather_puddle_scale", .85f), 0);
                snowColor = Param(material, "weather_snow_color", new Color(.91f, .95f, 1)).SrgbToLinear();
            }
        }
        else if (source != null) return null;
        int layer = Texture(texture, packet);
        if (layer < 0) return null;
        string protocol = new[] { "standard", "town", "plant", "leaf" }[kind];
        packet.MaterialCounts[protocol] = packet.MaterialCounts.GetValueOrDefault(protocol) + 1;
        return DdgiBytes.Of(new float[] { color.R, color.G, color.B, color.A, emission.X, emission.Y, emission.Z, Mathf.Clamp(metallic, 0, 1), uv.X, uv.Y, uv.Z, uv.W,
            cutoff, layer, useTexture ? 1 : 0, repeat ? 1 : 0, average.X, average.Y, average.Z, saturation, detail, vertexColor ? 1 : 0, kind, orm,
            weather.X, weather.Y, weather.Z, weather.W, puddles.X, puddles.Y, puddles.Z, puddles.W, snowColor.R, snowColor.G, snowColor.B, snowColor.A,
            foliage.X, foliage.Y, foliage.Z, foliage.W, lower.X, lower.Y, lower.Z, lower.W, upper.X, upper.Y, upper.Z, upper.W });
    }

    public DdgiPacket Capture(Node3D root, DdgiSettings settings)
    {
        var packet = new DdgiPacket { Settings = settings };
        _usedResources.Clear();
        byte[] white = new byte[TextureSize * TextureSize * 4]; System.Array.Fill(white, (byte)255);
        packet.TextureData.Add(white); packet.DataTextures.Add(white);
        using var materials = new MemoryStream(); using var lights = new MemoryStream(); using var lightWriter = new BinaryWriter(lights);
        using var emitters = new MemoryStream(); using var emitterWriter = new BinaryWriter(emitters);
        emitterWriter.Write(0); emitterWriter.Write(0); emitterWriter.Write(0); emitterWriter.Write(0);
        int emitterCount = 0;
        var pending = new Stack<Node>(); pending.Push(root);
        bool physical = ProjectSettings.GetSetting("rendering/lights_and_shadows/use_physical_light_units", false).AsBool();
        while (pending.TryPop(out Node node))
        {
            var children = node.GetChildren();
            using (var childrenOwner = (Godot.Collections.Array)children) foreach (Node child in children) pending.Push(child);
            if (node is MultiMeshInstance3D) packet.Warnings["multimesh"] = "MultiMesh instances need a GI proxy.";
            if (node is DirectionalLight3D light && light.IsVisibleInTree() && light.LightIndirectEnergy > 0)
            {
                Vector3 direction = light.GlobalBasis.Z.Normalized(); Color c = light.LightColor.SrgbToLinear();
                float energy = light.LightEnergy * light.LightIndirectEnergy * (light.LightNegative ? -1 : 1) * (physical ? light.LightIntensityLux : Mathf.Pi);
                DdgiBytes.Vector(lightWriter, direction); DdgiBytes.Vector(lightWriter, new Vector3(c.R, c.G, c.B) * energy);
            }
            if (node is not MeshInstance3D instance || !instance.IsVisibleInTree() || instance.Mesh == null) continue;
            bool deformed = instance.Skin != null || (!instance.Skeleton.IsEmpty && instance.GetNodeOrNull(instance.Skeleton) is Skeleton3D);
            if (instance.Mesh is ArrayMesh mesh)
                for (int shape = 0; shape < mesh.GetBlendShapeCount(); shape++) deformed |= Mathf.Abs(instance.GetBlendShapeValue(shape)) > .0001;
            if (deformed) { packet.Warnings["deformed_mesh"] = "Skinned or blend-shaped meshes need a static GI proxy."; continue; }
            if (Mathf.Abs(instance.GlobalBasis.Determinant()) < 1e-8) continue;
            for (int surface = 0; surface < instance.Mesh.GetSurfaceCount(); surface++)
            {
                Material material = instance.GetActiveMaterial(surface);
                if (!instance.GetMeta("ddgi_contribute", true).AsBool()) continue;
                byte[] bytes = MaterialData(material, packet);
                if (bytes == null) continue;
                var geometry = Geometry(instance.Mesh, surface);
                if (geometry == null) continue;
                packet.Geometry[geometry.Key] = geometry;
                int materialIndex = (int)materials.Length / MaterialStride;
                Transform3D transform = instance.GlobalTransform;
                packet.Instances.Add(new(geometry.Key, transform, materialIndex));
                materials.Write(bytes);
                var parameters = MemoryMarshal.Cast<byte, float>(bytes);
                if (parameters[12] >= 0 && Math.Max(parameters[4], Math.Max(parameters[5], parameters[6])) > 0)
                    packet.Warnings["alpha_emission"] = "Alpha-tested emission uses ray sampling; deterministic area integration currently supports opaque emitters.";
                if (parameters[12] < 0 && Math.Max(parameters[4], Math.Max(parameters[5], parameters[6])) > 0)
                {
                    var triangles = MemoryMarshal.Cast<byte, float>(geometry.Triangles);
                    for (int t = 0; t < triangles.Length; t += TriangleStride / sizeof(float))
                    {
                        Vector3 a = transform * new Vector3(triangles[t], triangles[t + 1], triangles[t + 2]);
                        Vector3 b = transform * new Vector3(triangles[t + 4], triangles[t + 5], triangles[t + 6]);
                        Vector3 c = transform * new Vector3(triangles[t + 8], triangles[t + 9], triangles[t + 10]);
                        if (transform.Basis.Determinant() < 0) (b, c) = (c, b);
                        if ((c - a).Cross(b - a).LengthSquared() < 1e-12) continue;
                        DdgiBytes.Vector(emitterWriter, a, materialIndex); DdgiBytes.Vector(emitterWriter, b); DdgiBytes.Vector(emitterWriter, c);
                        emitterCount++;
                    }
                }
            }
        }
        packet.Materials = materials.ToArray(); packet.Lights = lights.ToArray();
        emitters.Position = 0; emitterWriter.Write(emitterCount); packet.Emitters = emitters.ToArray();
        foreach (string key in _meshes.Keys.Where(k => !packet.Geometry.ContainsKey(k)).ToArray()) _meshes.Remove(key);
        var used = new HashSet<string>(packet.TextureKeys.Concat(packet.DataKeys));
        foreach (string key in _textures.Keys.Where(k => !used.Contains(k)).ToArray()) _textures.Remove(key);
        foreach (ulong id in _watched.Keys.Where(id => !_usedResources.Contains(id)).ToArray())
        {
            var watch = _watched[id];
            if (GodotObject.IsInstanceValid(watch.Resource)) watch.Resource.Changed -= watch.Changed;
            _watched.Remove(id); _versions.Remove(id);
        }
        return packet;
    }

    public void Clear()
    {
        foreach (var watch in _watched.Values)
            if (GodotObject.IsInstanceValid(watch.Resource)) watch.Resource.Changed -= watch.Changed;
        _watched.Clear(); _versions.Clear(); _meshes.Clear(); _textures.Clear();
    }
}
