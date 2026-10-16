using System.Collections.Generic;
using Godot;

namespace GameLogic;

/// <summary>由昼夜 EC 组件管理的云辐照采样；仅绘制原有云层，不采样关卡几何。</summary>
internal sealed class SkyCloudRadiance
{
    private static readonly string[] Parameters =
    {
        "cloud_map", "noise_map", "noise_map_st", "noise_time", "cloud_sdf", "weather_overcast",
        "bound_atlas_cell", "cloud_color_a", "cloud_color_b", "cloud_color_c", "cloud_color_d",
        "cloud_edge_color", "sun_direction", "moon_direction", "sun_moon", "horizon_fade"
    };
    private static readonly Vector3[] Directions =
        { Vector3.Right, Vector3.Left, Vector3.Up, Vector3.Down, Vector3.Back, Vector3.Forward };
    private readonly List<(MeshInstance3D Source, ShaderMaterial Copy, MeshInstance3D Mesh)> _layers = new();
    private readonly List<SubViewport> _faces = new();
    private readonly List<Texture2Drd> _textures = new();
    private RefCounted _textureCopy;
    private Node _root;
    private ShaderMaterial _sky;
    private bool _enabled;
    private int _revision;

    public bool Matches(ShaderMaterial sky, IReadOnlyList<MeshInstance3D> sources)
    {
        if (!GodotObject.IsInstanceValid(_root) || _sky != sky) return false;
        int index = 0;
        foreach (var source in sources)
        {
            if (source?.Mesh == null || source.MaterialOverride is not ShaderMaterial) continue;
            if (index >= _layers.Count || _layers[index++].Source != source) return false;
        }
        return index == _layers.Count;
    }

    public void Bind(Node host, ShaderMaterial sky, IEnumerable<MeshInstance3D> sources)
    {
        Release();
        // ViewportTexture 是 Local To Scene 资源，复制场景时会解析临时节点路径。
        // 天空只接收不含节点路径的独立 GPU 副本，视口保留源纹理的所有权。
        sky.SetShaderParameter("cloud_radiance_enabled", false);
        sky.SetShaderParameter("cloud_radiance_revision", 0f);
        for (int face = 0; face < 6; face++) sky.SetShaderParameter("cloud_radiance_" + face, default(Variant));
        // C# 编辑器热重载后，原生内部节点可能比旧组件实例存活得更久。
        var previous = host.GetNodeOrNull<Node>("SkyCloudRadiance");
        if (previous != null && previous.HasMeta("yu_cloud_radiance_capture"))
        {
            previous.Name = "SkyCloudRadianceRetired";
            previous.QueueFree();
        }
        _sky = sky;
        _root = new Node { Name = "SkyCloudRadiance" };
        _root.SetMeta("yu_cloud_radiance_capture", true);
        host.AddChild(_root, false, Node.InternalMode.Back);
        // 一个独立世界，六个方向；没有关卡、灯光、GI、后处理或物理相机曝光。
        var world = new World3D { Environment = new Godot.Environment
            { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = Colors.Black } };
        using var sourceTextures = new Godot.Collections.Array();
        using var targetTextures = new Godot.Collections.Array();
        for (int face = 0; face < 6; face++)
        {
            var viewport = new SubViewport
            {
                Name = "Face" + face, Size = new Vector2I(256, 256), World3D = world,
                TransparentBg = true, UseHdr2D = true, GuiDisableInput = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled
            };
            _root.AddChild(viewport);
            var camera = new Camera3D
            {
                Fov = 90, Near = .1f, Far = 2000, Position = new Vector3(0, -14, 0),
                Basis = Basis.LookingAt(Directions[face], face == 2 ? Vector3.Back : face == 3 ? Vector3.Forward : Vector3.Up)
            };
            viewport.AddChild(camera);
            _faces.Add(viewport);
            _textures.Add(new Texture2Drd());
            sourceTextures.Add(viewport.GetTexture().GetRid());
            targetTextures.Add(_textures[face]);
            // 临时 GPU 纹理只进入渲染服务器，不进入 ShaderMaterial 的可序列化参数缓存。
            RenderingServer.MaterialSetParam(sky.GetRid(), "cloud_radiance_" + face, _textures[face].GetRid());
        }
        using (var script = ResourceLoader.Load<GDScript>("res://scripts/gamelogic/rendering/cloud_radiance_copy.gd"))
        {
            using var instance = script.New();
            _textureCopy = instance.As<RefCounted>();
            _textureCopy.Set("sources", sourceTextures);
            _textureCopy.Set("targets", targetTextures);
        }
        foreach (var source in sources)
        {
            if (source?.Mesh == null || source.MaterialOverride is not ShaderMaterial material) continue;
            var copy = (ShaderMaterial)material.Duplicate();
            copy.SetShaderParameter("sky_custom_fov", 0f);
            copy.SetShaderParameter("sky_pitch", 0f);
            // 六个视口共用这个世界，云网格只需放入一次。
            var mesh = new MeshInstance3D
            {
                Name = source.Name, Mesh = source.Mesh, MaterialOverride = copy,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                GIMode = GeometryInstance3D.GIModeEnum.Disabled, ExtraCullMargin = 16384
            };
            _faces[0].AddChild(mesh);
            _layers.Add((source, copy, mesh));
        }
        Refresh(true);
    }

    public void Refresh(bool force = false)
    {
        if (!GodotObject.IsInstanceValid(_root) || !GodotObject.IsInstanceValid(_sky)) return;
        bool changed = force;
        bool texturesReady = true;
        foreach (Texture2Drd texture in _textures) texturesReady &= texture.TextureRdRid.IsValid;
        bool enabled = false;
        foreach (var layer in _layers)
        {
            var sourceMaterial = GodotObject.IsInstanceValid(layer.Source) ? layer.Source.MaterialOverride as ShaderMaterial : null;
            bool visible = sourceMaterial != null && layer.Source.IsVisibleInTree();
            enabled |= visible;
            if (layer.Mesh.Visible != visible)
            {
                layer.Mesh.Visible = visible;
                changed = true;
            }
            if (sourceMaterial == null) continue;
            if (layer.Copy.Shader != sourceMaterial.Shader)
            {
                layer.Copy.Shader = sourceMaterial.Shader;
                layer.Copy.SetShaderParameter("sky_custom_fov", 0f);
                layer.Copy.SetShaderParameter("sky_pitch", 0f);
                changed = true;
            }
            foreach (string name in Parameters)
            {
                using var value = sourceMaterial.GetShaderParameter(name);
                using var old = layer.Copy.GetShaderParameter(name);
                if (value.Equals(old)) continue;
                layer.Copy.SetShaderParameter(name, value);
                changed = true;
            }
            if (!layer.Mesh.Transform.IsEqualApprox(layer.Source.GlobalTransform) || layer.Mesh.Mesh != layer.Source.Mesh)
            {
                layer.Mesh.Transform = layer.Source.GlobalTransform;
                layer.Mesh.Mesh = layer.Source.Mesh;
                changed = true;
            }
        }
        enabled &= texturesReady;
        if (enabled != _enabled)
        {
            _enabled = enabled;
            RenderingServer.MaterialSetParam(_sky.GetRid(), "cloud_radiance_enabled", enabled);
            changed = true;
        }
        if (!changed) return;
        foreach (var face in _faces) face.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        _textureCopy?.Call("request_copy");
        RenderingServer.MaterialSetParam(_sky.GetRid(), "cloud_radiance_revision", (float)(_revision++ % 1024));
    }

    public void Release()
    {
        if (GodotObject.IsInstanceValid(_sky))
        {
            RenderingServer.MaterialSetParam(_sky.GetRid(), "cloud_radiance_enabled", false);
            RenderingServer.MaterialSetParam(_sky.GetRid(), "cloud_radiance_revision", 0f);
            for (int face = 0; face < 6; face++)
                RenderingServer.MaterialSetParam(_sky.GetRid(), "cloud_radiance_" + face, default(Variant));
        }
        // 原生回调持有包装器，先解绑，再在渲染线程清理包装与自有复制纹理。
        _textureCopy?.Call("release");
        _textureCopy?.Dispose();
        _textureCopy = null;
        _textures.Clear();
        if (GodotObject.IsInstanceValid(_root))
        {
            // 保存通知仍在遍历父节点的子树，不能同步 RemoveChild。
            // 内部节点没有 Owner，不会写入场景；QueueFree 在遍历结束后释放。
            _root.QueueFree();
        }
        _layers.Clear();
        _faces.Clear();
        _root = null;
        _sky = null;
        _enabled = false;
        _revision = 0;
    }
}
