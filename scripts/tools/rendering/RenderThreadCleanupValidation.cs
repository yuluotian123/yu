using System;
using System.Threading;
using System.Threading.Tasks;
using GameLogic;
using Godot;

/// <summary>Exercises compositor buffers through viewport destruction and managed collection.</summary>
public partial class RenderThreadCleanupValidation : Node
{
    private partial class ErrorLog : Logger
    {
        public int Errors;

        public override void _LogError(string function, string file, int line, string code, string rationale,
            bool editorNotify, int errorType, Godot.Collections.Array<ScriptBacktrace> scriptBacktraces)
        {
            if (errorType != (int)ErrorType.Warning) Interlocked.Increment(ref Errors);
        }
    }

    public override async void _Ready()
    {
        using var log = new ErrorLog();
        OS.AddLogger(log);
        int exitCode = 0;
        try
        {
            Engine.MaxFps = 60;
            await RunCase("relink_bloom", true, false, true);
            await RunCase("relink_no_bloom", true, false, false);
            await RunCase("ddgi", false, true, false);
            await RunCase("combined", true, true, true);
            Collect();
            await Frames(4);
            if (log.Errors != 0) throw new InvalidOperationException($"Observed {log.Errors} engine errors.");
            GD.Print("RENDER_THREAD_CLEANUP_PASS");
        }
        catch (Exception error)
        {
            GD.PushError("RENDER_THREAD_CLEANUP_FAILED " + error);
            exitCode = 1;
        }
        finally
        {
            OS.RemoveLogger(log);
            GetTree().Quit(exitCode);
        }
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private async Task RunCase(string label, bool relink, bool ddgi, bool bloom)
    {
        using var source = ResourceLoader.Load<RelinkPostProcessProfile>("res://assets/resources/rendering/relink_post/profile.tres");
        using var profile = (RelinkPostProcessProfile)source.Duplicate();
        profile.BloomEnabled = bloom;
        using var grade = new RelinkPostProcessEffect { Pass = RelinkPostProcessEffect.PassKind.ColorGrade, Profile = profile };
        using var film = new RelinkPostProcessEffect { Profile = profile, PixelGridHeight = 32 };
        using var gi = new DdgiEffect();
        using var giProfile = new DdgiProfile { ProbeCounts = new(2, 2, 2), RaysPerProbe = 32, ProbesPerFrame = 8 };
        using var compositor = new Compositor();
        var effects = new Godot.Collections.Array<CompositorEffect>();
        using var effectsOwner = (Godot.Collections.Array)effects;
        if (ddgi) effects.Add(gi);
        if (relink) { effects.Add(grade); effects.Add(film); }
        compositor.CompositorEffects = effects;

        for (int cycle = 0; cycle < 3; cycle++)
        {
            var viewport = new SubViewport
            {
                Size = new(96, 96), OwnWorld3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                Msaa3D = cycle == 1 ? Viewport.Msaa.Msaa4X : Viewport.Msaa.Disabled
            };
            var scene = new Node3D();
            using var environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new(.2f, .3f, .4f)
            };
            scene.AddChild(new WorldEnvironment { Environment = environment, Compositor = compositor });
            scene.AddChild(new Camera3D { Position = new(0, 0, 3), Current = true });
            using var mesh = new BoxMesh();
            scene.AddChild(new MeshInstance3D { Mesh = mesh });
            viewport.AddChild(scene);
            AddChild(viewport);
            if (ddgi)
            {
                using var settings = giProfile.Snapshot();
                gi.CaptureScene(scene, settings);
            }
            await Frames(12);
            if (relink && (grade.GetRenderStatus().StartsWith("尚未") || film.GetRenderStatus() != "已执行"))
                throw new InvalidOperationException($"{label}: Relink did not render: {grade.GetRenderStatus()} / {film.GetRenderStatus()}");
            if (ddgi)
            {
                using var status = gi.GetStatus();
                if (status["state"].AsString() != "running") throw new InvalidOperationException($"{label}: DDGI did not render: {status}");
                gi.FreezeUpdates = true;
            }
            viewport.Size = new(128, 80);
            await Frames(4);
            viewport.QueueFree();
            await Frames(4);
            // Keep effects alive, as in an editor inspector, while the viewport is gone.
            Collect();
            await Frames(4);
            gi.FreezeUpdates = false;
            grade.ReleaseGpuResources();
            film.ReleaseGpuResources();
            await Frames(4);
        }
        gi.ReleaseResources();
        await Frames(4);
        GD.Print("RENDER_THREAD_CLEANUP_CASE " + label);
    }
}
