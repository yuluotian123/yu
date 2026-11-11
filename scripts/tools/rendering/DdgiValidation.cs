using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GameLogic;
using Godot;

/// <summary>Regression checks for cached inputs, bounded relighting and local GPU history.</summary>
public partial class DdgiValidation : Node
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private async Task Frames(int count)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    private static float Albedo(DdgiPacket packet) => BitConverter.ToSingle(packet.Materials, 0);

    private void CheckCaptureAndAtlas()
    {
        using var profile = new DdgiProfile { Origin = new(-55.75f, -2.85f, -53), ProbeCounts = new(32, 8, 30), Spacing = 3.65f };
        using var values = profile.Snapshot();
        var settings = DdgiSettings.From(values);
        var fitted = DdgiEffect.FitAtlas(settings, 16384);
        Check(fitted.Counts.X * fitted.Counts.Z * 18 <= 16384, "Atlas exceeds device limit");
        var requestedBounds = new Aabb(settings.Origin, (Vector3)(settings.Counts - Vector3I.One) * settings.Spacing);
        var fittedBounds = new Aabb(fitted.Origin, (Vector3)(fitted.Counts - Vector3I.One) * fitted.Spacing);
        Check(fittedBounds.Grow(.0001f).Encloses(requestedBounds), "Device fallback lost world coverage");
        Check(DdgiEffect.FitAtlas(settings, 32768) == settings, "Capable GPU changed its authored layout");

        var scene = new Node3D(); AddChild(scene);
        using var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/town/town_toon.gdshader") };
        material.SetShaderParameter("base_color", new Color(.8f, .6f, .4f));
        using var mesh = new BoxMesh();
        var box = new MeshInstance3D { Mesh = mesh, MaterialOverride = material }; scene.AddChild(box);
        var capture = new DdgiSceneCapture();
        try
        {
            var original = capture.Capture(scene, settings);
            var cached = capture.Capture(scene, settings);
            Check(cached.ReusedScene && ReferenceEquals(original.Materials, cached.Materials), "Static scene was repacked");
            material.SetShaderParameter("base_color", new Color(.2f, .3f, .4f));
            material.EmitChanged();
            var changed = capture.Capture(scene, settings);
            Check(Albedo(changed) < Albedo(original) * .2f, "Explicit material change was not captured immediately");
            material.SetShaderParameter("base_color", new Color(.9f, .3f, .4f));
            for (int i = 0; i < 15; i++) changed = capture.Capture(scene, settings);
            Check(Albedo(changed) > Albedo(original), "Unsignalled uniform change escaped fallback polling");
            box.Position = new(2, 0, 0);
            changed = capture.Capture(scene, settings);
            Check(changed.Instances[0].Transform.Origin.X == 2, "Cached scene missed a rigid transform");
            box.Visible = false;
            Check(capture.Capture(scene, settings).Instances.Count == 0, "Cached scene missed visibility change");
            box.Visible = true;
            Check(capture.Capture(scene, settings).Instances.Count == 1, "Hidden mesh did not rejoin capture");
        }
        finally { capture.Clear(); scene.Free(); }
        GD.Print("DDGI_CAPTURE_ATLAS_PASS");
    }

    private static async Task<byte[]> Epochs(DdgiEffect effect)
    {
        // Test-only GPU readback. No debug readback API is exposed by the production effect.
        var atlas = (Dictionary<string, Rid>)typeof(DdgiEffect).GetField("_atlas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(effect)!;
        Rid rid = atlas["epochs"];
        var completion = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        RenderingServer.CallOnRenderThread(Callable.From(() =>
        {
            try { completion.SetResult(RenderingServer.GetRenderingDevice().TextureGetData(rid, 0)); }
            catch (Exception error) { completion.SetException(error); }
        }));
        return await completion.Task;
    }

    private async Task CheckGpu()
    {
        using var gi = new DdgiEffect();
        using var profile = new DdgiProfile { Origin = new(-15, 1, -15), ProbeCounts = new(16, 4, 16), Spacing = 2,
            ProbesPerFrame = 64, RaysPerProbe = 32, UseEnvironmentSky = false, StableSampleDirections = true };
        using var compositor = new Compositor();
        compositor.CompositorEffects = new Godot.Collections.Array<CompositorEffect> { gi };
        var viewport = new SubViewport { Size = new(96, 96), OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        var scene = new Node3D();
        using var environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color };
        scene.AddChild(new WorldEnvironment { Environment = environment, Compositor = compositor });
        scene.AddChild(new Camera3D { Position = new(0, 3, 8), Current = true });
        using var mesh = new BoxMesh();
        var box = new MeshInstance3D { Mesh = mesh, Position = new(0, 1, 0) }; scene.AddChild(box);
        viewport.AddChild(scene); AddChild(viewport);
        void Capture()
        { using var settings = profile.Snapshot(); gi.CaptureScene(scene, settings); }
        try
        {
            Capture(); await Frames(40);
            using var before = gi.GetStatus();
            Check(before["state"].AsString() == "running", "DDGI did not start");
            byte[] epochsBefore = await Epochs(gi);
            int globalEpoch = before["visibility_epoch"].AsInt32();
            int lightEpoch = before["lighting_epoch"].AsInt32();
            for (int frame = 0; frame < 10; frame++)
            {
                box.Position += new Vector3(.02f, 0, 0); Capture(); await Frames(1);
            }
            await Frames(2);
            using var moving = gi.GetStatus();
            Check(moving["visibility_epoch"].AsInt32() == globalEpoch && moving["lighting_epoch"].AsInt32() == lightEpoch,
                "Local movement invalidated global history");
            int dirty = moving["local_invalidated_probes"].AsInt32();
            Check(dirty > 0 && dirty < 1024, "Movement did not invalidate a local subset");
            byte[] epochsAfter = await Epochs(gi);
            Check(BitConverter.ToUInt32(epochsBefore, 0) != 0 && epochsBefore.AsSpan(0, 8).SequenceEqual(epochsAfter.AsSpan(0, 8)),
                "Far probe history was reset");
            bool resetNearProbe = false;
            for (int i = 0; i < epochsAfter.Length; i += 8) resetNearProbe |= BitConverter.ToUInt32(epochsAfter, i + 4) == 0;
            Check(resetNearProbe, "GPU local invalidation never reset near-probe epochs");
            GD.Print($"DDGI_LOCAL_HISTORY_PASS dirty={dirty}/1024");

            profile.SkyRadiance = new Color(.8f, .4f, .2f); Capture();
            for (int frame = 0; frame < 20; frame++)
            {
                await Frames(1);
                using var status = gi.GetStatus();
                Check(status["updated_probes"].AsInt32() <= profile.ProbesPerFrame, "Relight exceeded probe budget");
            }
            profile.Enabled = false; Capture(); await Frames(3);
            using var disabled = gi.GetStatus();
            Check(disabled["state"].AsString() == "disabled" && !disabled["native_receiver"].AsBool()
                && disabled["capture_cpu_ms"].AsDouble() == 0 && disabled["gpu_update_ms"].AsDouble() == 0
                && disabled["updated_probes"].AsInt32() == 0, "Disabled DDGI retained activity/capture");
            int updates = disabled["updates"].AsInt32();
            await Frames(3);
            using var stillDisabled = gi.GetStatus();
            Check(stillDisabled["updates"].AsInt32() == updates, "Disabled DDGI still dispatched probes");
            profile.Enabled = true; Capture(); await Frames(3);
            using var resumed = gi.GetStatus();
            Check(resumed["state"].AsString() == "running" && resumed["updates"].AsInt32() > updates, "DDGI did not resume");
            GD.Print("DDGI_BUDGET_DISABLE_PASS");
        }
        finally
        {
            gi.ReleaseResources(); viewport.QueueFree(); await Frames(4);
        }
    }

    public override async void _Ready()
    {
        int exitCode = 0;
        try { CheckCaptureAndAtlas(); await CheckGpu(); GD.Print("DDGI_REGRESSION_PASS"); }
        catch (Exception error) { GD.PushError("DDGI_REGRESSION_FAILED " + error); exitCode = 1; }
        GetTree().Quit(exitCode);
    }
}
