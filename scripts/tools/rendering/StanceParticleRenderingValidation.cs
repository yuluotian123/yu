using System;
using System.Threading.Tasks;
using GameLogic;
using Godot;

// Uses the forest compositor and actual particle scene, with a deterministic frozen burst.
public partial class StanceParticleRenderingValidation : Node3D
{
    private WorldEnvironment _world;
    public override void _Process(double delta) => RelinkPostProcessEffect.CaptureCharacterSprites(_world);

    public override async void _Ready()
    {
        try
        {
            var profile = GD.Load<RelinkPostProcessProfile>("res://assets/resources/rendering/relink_post/profile.tres");
            var grade = new RelinkPostProcessEffect { Pass = RelinkPostProcessEffect.PassKind.ColorGrade, Profile = profile };
            var film = new RelinkPostProcessEffect { Profile = profile, PixelGridHeight = 270 };
            _world = new WorldEnvironment
            {
                Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new(.08f, .12f, .16f), BackgroundIntensity = 1, TonemapMode = Godot.Environment.ToneMapper.Linear },
                Compositor = new Compositor { CompositorEffects = new Godot.Collections.Array<CompositorEffect> { grade, film } }
            };
            AddChild(_world);
            AddChild(new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 2.3f, Position = new(0, 0, 7), Current = true });
            var burst = GD.Load<PackedScene>("res://assets/scenes/vfx/stance_switch_particles.tscn").Instantiate<Node3D>();
            AddChild(burst);
            burst.Visible = true;
            var particles = burst.GetNode<CpuParticles3D>("Particles");
            particles.UseFixedSeed = true;
            particles.Seed = 41;
            particles.SpeedScale = 0;
            particles.Emitting = true;
            particles.RequestParticlesProcess(.08f);
            await Frames(8);
            string folder = "res://.godot/stance-particle-render";
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
            using var preserved = await Capture(folder + "/preserved.png");
            particles.RemoveFromGroup(RelinkPostProcessEffect.DetailParticleGroup);
            await Frames(3);
            using var filtered = await Capture(folder + "/filtered.png");
            film.PixelGridHeight = 0;
            await Frames(3);
            using var reference = await Capture(folder + "/reference.png");
            if (film.HasShaderError || grade.HasShaderError || film.GetRenderStatus() != "已执行")
                throw new InvalidOperationException("Forest compositor failed: " + film.GetRenderStatus());
            // Compare particle pixels against the same film/bloom without pixel reduction.
            double keptError = 0, filteredError = 0;
            int count = 0;
            for (int y = 0; y < reference.GetHeight(); y++)
                for (int x = 0; x < reference.GetWidth(); x++)
                {
                    Color sample = reference.GetPixel(x, y);
                    if (sample.R <= sample.B * 1.25f || sample.R < .3f) continue;
                    keptError += Difference(sample, preserved.GetPixel(x, y));
                    filteredError += Difference(sample, filtered.GetPixel(x, y));
                    count++;
                }
            GD.Print($"PARTICLE_RENDER: pixels={count}, preserved_error={keptError:F4}, filtered_error={filteredError:F4}, renderer={RenderingServer.GetCurrentRenderingMethod()}");
            if (count < 20 || keptError >= filteredError * .25)
                throw new InvalidOperationException("Particle details were not preserved through pixelation.");
            // A patterned wall in front must remain pixelated and fully hide the burst.
            var wallShader = new Shader { Code = "shader_type spatial; render_mode unshaded; void fragment(){ float stripe=step(.5,fract(UV.x*151.0+UV.y*97.0)); ALBEDO=mix(vec3(.03,.15,.3),vec3(.06,.3,.45),stripe); }" };
            AddChild(new MeshInstance3D { Position = new(0, 0, 1), Mesh = new QuadMesh { Size = new(1.4f, 1.5f) }, MaterialOverride = new ShaderMaterial { Shader = wallShader } });
            film.PixelGridHeight = 270;
            particles.AddToGroup(RelinkPostProcessEffect.DetailParticleGroup);
            await Frames(4);
            using var occluded = await Capture(folder + "/occluded-preserved.png");
            particles.RemoveFromGroup(RelinkPostProcessEffect.DetailParticleGroup);
            await Frames(3);
            using var occludedFiltered = await Capture(folder + "/occluded-filtered.png");
            double occlusionError = 0;
            for (int y = 0; y < occluded.GetHeight(); y++)
                for (int x = 0; x < occluded.GetWidth(); x++)
                    occlusionError += Difference(occluded.GetPixel(x, y), occludedFiltered.GetPixel(x, y));
            if (occlusionError > 1) throw new InvalidOperationException("Particle mask affected the occluding wall: " + occlusionError);
            GD.Print($"PARTICLE_RENDER: occlusion_error={occlusionError:F4}, msaa={GetViewport().Msaa3D}");
            GD.Print("PARTICLE_RENDER: PASS");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("PARTICLE_RENDER: FAIL " + error); GetTree().Quit(1); }
    }

    private static double Difference(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
    private async Task Frames(int count) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task<Image> Capture(string path)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = GetViewport().GetTexture().GetImage();
        if (image.SavePng(path) != Error.Ok) throw new InvalidOperationException("Capture failed: " + path);
        return image;
    }
}
