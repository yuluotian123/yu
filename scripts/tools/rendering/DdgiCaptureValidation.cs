using System;
using System.Linq;
using GameLogic;
using Godot;

/// <summary>Validates shared-material capture and next-frame material changes.</summary>
public partial class DdgiCaptureValidation : RefCounted
{
    public int Run(Node parent)
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }

        var capture = new DdgiSceneCapture();
        var root = new Node3D { Name = "CaptureValidation" };
        parent.AddChild(root);
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/town/town_toon.gdshader") };
        material.SetShaderParameter("base_color", new Color(.6f, .3f, .2f));
        var mesh = new BoxMesh();
        var first = new MeshInstance3D { Mesh = mesh, MaterialOverride = material };
        var second = new MeshInstance3D { Mesh = mesh, MaterialOverride = material, Position = Vector3.Right * 2 };
        root.AddChild(first);
        root.AddChild(second);
        var profile = new DdgiProfile();
        using var configuration = profile.Snapshot();
        var settings = DdgiSettings.From(configuration);
        try
        {
            var a = capture.Capture(root, settings);
            Check(a.Instances.Count == 2 && a.Geometry.Count == 1, "Independent instances retain shared geometry.");
            Check(a.MaterialCounts["town"] == 2, "Protocol counts include every shared surface.");
            Check(a.Materials.Length == 2 * DdgiSceneCapture.MaterialStride, "Each surface retains a material record.");
            Check(a.Materials.AsSpan(0, DdgiSceneCapture.MaterialStride).SequenceEqual(a.Materials.AsSpan(DdgiSceneCapture.MaterialStride)), "Shared material records are identical.");
            var b = capture.Capture(root, settings);
            Check(a.Materials.SequenceEqual(b.Materials) && a.Instances.SequenceEqual(b.Instances), "An unchanged snapshot remains byte-identical.");
            material.SetShaderParameter("base_color", new Color(.2f, .5f, .7f));
            var c = capture.Capture(root, settings);
            Check(!b.Materials.SequenceEqual(c.Materials), "Color edits reach the next snapshot.");
            Check(c.Materials.AsSpan(0, DdgiSceneCapture.MaterialStride).SequenceEqual(c.Materials.AsSpan(DdgiSceneCapture.MaterialStride)), "Color edits reach both shared surfaces.");
            material.SetShaderParameter("weather_wetness", .7f);
            var d = capture.Capture(root, settings);
            Check(!c.Materials.SequenceEqual(d.Materials), "Weather uniforms reach the next snapshot.");
            using var image = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
            image.Fill(Colors.White);
            using var texture = ImageTexture.CreateFromImage(image);
            material.SetShaderParameter("use_texture", true);
            material.SetShaderParameter("albedo_texture", texture);
            var e = capture.Capture(root, settings);
            Check(e.TextureKeys.Count == 2 && e.TextureData.Count == 2, "Shared textures occupy one layer.");
            Check(!d.Materials.SequenceEqual(e.Materials), "Texture edits reach the next snapshot.");
            first.MaterialOverride = null;
            second.MaterialOverride = null;
            var f = capture.Capture(root, settings);
            Check(f.Instances.Count == 2 && f.MaterialCounts["standard"] == 2, "Null materials use the standard fallback for every surface.");
            using var unsupportedShader = new Shader { Code = "shader_type spatial;" };
            using var unsupported = new ShaderMaterial { Shader = unsupportedShader };
            first.MaterialOverride = unsupported;
            second.MaterialOverride = unsupported;
            var g = capture.Capture(root, settings);
            Check(g.Instances.Count == 0 && g.Warnings.ContainsKey("unsupported_shader"), "Unsupported shared materials preserve exclusion and warnings.");
            GD.Print($"DDGI_CAPTURE_VALIDATION: {checks} checks passed");
            return checks;
        }
        finally
        {
            capture.Clear();
            root.Free();
            material.Dispose();
            mesh.Dispose();
            profile.Dispose();
        }
    }
}
