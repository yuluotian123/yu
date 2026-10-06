using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Framework;
using GameLogic;
using Godot;

public partial class TimeOfDaySmokeTest : Node
{
    public override void _Ready() => Callable.From(Run).CallDeferred();

    private async void Run()
    {
        try
        {
            VerifyClock();
            VerifyOrbits();
            await VerifySceneCamera();
            await VerifyEnvironment();
            ModuleSystem.Shutdown();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print("[TimeOfDaySmokeTest] PASS: calendar, moon, GM, original cloud mesh and Timeline, scene camera, persistence and lifecycle.");
            GetTree().Quit(0);
        }
        catch (Exception e)
        {
            GD.PushError($"[TimeOfDaySmokeTest] FAIL: {e}");
            ModuleSystem.Shutdown();
            GetTree().Quit(1);
        }
    }

    private static void VerifyClock()
    {
        var clock = new WorldClock();
        int dayEvents = 0;
        clock.DayChanged += _ => dayEvents++;
        clock.SetDate(0, 23);
        clock.Tick(100); // 1200 秒 / 日，100 秒 = 2 游戏小时。
        Require(clock.State.Day == 1 && Near(clock.State.Hour, 1), "Midnight rollover.");
        Require(dayEvents == 1, "Day event count.");
        clock.Paused = true;
        clock.Tick(1200);
        Require(Near(clock.TotalHours, 25), "Paused clock advanced.");
        clock.Paused = false;
        clock.Speed = 2;
        clock.Tick(50);
        Require(Near(clock.State.Hour, 3), "Time speed applied incorrectly.");
        clock.SetDate(2, 24);
        Require(clock.State.Day == 3 && Near(clock.State.Hour, 0), "24:00 must normalize.");
        clock.Speed = 1;
        clock.SetLunarPhase(0);
        Require(Near(clock.State.MoonIllumination, 0) && clock.State.Phase == MoonPhase.NewMoon, "New moon.");
        clock.AdvanceHours(clock.LunarPeriodDays * 24 / 4);
        Require(Near(clock.State.MoonIllumination, .5) && clock.State.Phase == MoonPhase.FirstQuarter, "First quarter.");
        clock.AdvanceHours(clock.LunarPeriodDays * 24 / 4);
        Require(Near(clock.State.MoonIllumination, 1) && clock.State.Phase == MoonPhase.FullMoon, "Full moon.");
        clock.AdvanceHours(clock.LunarPeriodDays * 24 / 2);
        Require(Near(clock.State.MoonIllumination, 0), "Lunar wrap.");
        clock.AdvanceHours(-1e8);
        Require(clock.TotalHours == 0, "Negative seek must clamp at epoch.");
        clock.AdvanceHours(1e12);
        Require(clock.State.Day == WorldClock.MaxDay, "Forward jump overflow.");
        double before = clock.TotalHours;
        ExpectInvalid(() => clock.Restore(1, 1200, 29, 0, double.NaN, false));
        Require(clock.TotalHours == before, "Invalid restore changed state.");
        ExpectInvalid(() => clock.Tick(double.PositiveInfinity));
        ExpectInvalid(() => clock.DayLengthSeconds = 0);
        ExpectInvalid(() => clock.SetLunarPhase(-.1));
    }

    private static void VerifyOrbits()
    {
        var noon = CelestialMath.Direction(12, 0, 75, -25);
        var midnight = CelestialMath.Direction(0, 0, 75, -25);
        Require(noon.Y > .96 && midnight.Y < -.96, "Solar elevation.");
        Require(Math.Abs(CelestialMath.Direction(6, 0, 75, -25).Y) < 1e-5, "Sunrise horizon.");
        var fullMoon = CelestialMath.Direction(0, .5, 75, -25);
        Require(fullMoon.Y > .96 && midnight.Dot(fullMoon) < -.999f, "Full moon must oppose Sun.");
        var quarter = CelestialMath.Direction(18, .25, 75, -25);
        Require(quarter.Y > .96, "First quarter must culminate in evening.");
    }

    private async Task VerifyEnvironment()
    {
        var module = ModuleSystem.GetModule<ITimeOfDayModule>();
        var clock = module.Clock;
        var save = (ISaveSection)module;
        clock.SetDate(13, 22.75);
        clock.SetLunarPhase(.25);
        clock.Speed = 3;
        clock.Paused = true;
        JsonObject captured = save.Capture();
        captured["initialized"] = true;
        clock.SetDate(0, 12);
        clock.SetLunarPhase(0);
        clock.Paused = false;
        save.Restore(captured, 1);
        Require(clock.State.Day == 13 && Near(clock.State.Hour, 22.75) && Near(clock.State.LunarPhase, .25) && clock.Paused && clock.Speed == 3, "Save round trip.");
        double savedHours = clock.TotalHours;
        save.Restore(new JsonObject(), 999);
        Require(clock.TotalHours == savedHours, "Future schema mutated state.");

        var packed = GD.Load<PackedScene>("res://assets/scenes/environment/day_night_environment.tscn");
        var rig = packed.Instantiate<GameObject3D>();
        var template = rig.GetNode<WorldEnvironment>("WorldEnvironment").Environment;
        var other = packed.Instantiate<GameObject3D>();
        var otherEnvironment = other.GetNode<WorldEnvironment>("WorldEnvironment").Environment;
        Require(template != otherEnvironment && template.Sky != otherEnvironment.Sky &&
            template.Sky.SkyMaterial != otherEnvironment.Sky.SkyMaterial, "Local To Scene isolation failed.");
        other.Free();
        AddChild(rig);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var component = rig.GetComponent<DayNightEnvironmentComponent3D>();
        var environment = rig.GetNode<WorldEnvironment>("WorldEnvironment");
        Require(module.HasEnvironment && component != null, "Environment did not attach.");
        Require(environment.Environment == template, "Controller replaced scene-authored environment.");
        var skyMaterial = (ShaderMaterial)template.Sky.SkyMaterial;
        var projectionCamera = new Camera3D { Current = true, Projection = Camera3D.ProjectionType.Perspective, Fov = 47 };
        AddChild(projectionCamera);
        component.OnUpdate(.016);
        Require(template.SkyCustomFov == 0 && template.SkyRotation == Vector3.Zero, "Perspective sky must inherit camera projection.");
        projectionCamera.Projection = Camera3D.ProjectionType.Orthogonal;
        component.OnUpdate(.016);
        Require(Near(template.SkyCustomFov, component.Profile.OrthographicSkyFov, 1e-5) &&
            Near(template.SkyRotation.X, Mathf.DegToRad(component.Profile.OrthographicSkyPitchDegrees), 1e-5),
            "Orthographic sky projection must update even when the clock is paused.");
        projectionCamera.Free();
        var cloudMesh = rig.GetNode<MeshInstance3D>("OriginalClouds/BaseClouds");
        Require(cloudMesh.Mesh is ArrayMesh && cloudMesh.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.TexUV2].AsVector2Array().Length == 80,
            "Scene must reference the author's original mesh and UV2, without runtime card generation.");
        var cloudMaterial = (ShaderMaterial)cloudMesh.MaterialOverride;
        Require(cloudMaterial.GetShaderParameter("cloud_map").AsGodotObject() is Texture2D cloudMap && cloudMap.ResourcePath.EndsWith("/78678.png"),
            "Scene must reference the unmodified source RGBA cloud map.");
        VerifyCloudAnimation(clock, component, cloudMaterial);
        component.SetDate(1, 12);
        component.SetLunarPhase(.5);
        Require(rig.GetNode<DirectionalLight3D>("Sun").Visible, "No noon Sun.");
        Require(!rig.GetNode<DirectionalLight3D>("Moon").Visible, "Full Moon above horizon at noon.");
        component.SetTimeOfDay(0);
        // SetTime changes lunar age slightly; force full moon at this instant.
        component.SetLunarPhase(.5);
        Require(!rig.GetNode<DirectionalLight3D>("Sun").Visible && rig.GetNode<DirectionalLight3D>("Moon").Visible, "Midnight lights.");
        Require(Near(rig.GetNode<DirectionalLight3D>("Moon").LightIntensityLux, .3, 1e-4), "Full moon intensity.");
        component.SetLunarPhase(0);
        Require(!rig.GetNode<DirectionalLight3D>("Moon").Visible, "New moon emits light.");
        clock.Paused = false;
        savedHours = clock.TotalHours;
        GetTree().Paused = true;
        ((IProcessModule)module).Process(100, 100);
        GetTree().Paused = false;
        Require(clock.TotalHours == savedHours, "World pause ignored.");
        await VerifyGm(clock);
        savedHours = clock.TotalHours;
        rig.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(!module.HasEnvironment, "Environment ownership leaked.");
        ((IProcessModule)module).Process(100, 100);
        Require(clock.TotalHours == savedHours, "Menu time advanced.");
        var nextRig = packed.Instantiate<GameObject3D>();
        AddChild(nextRig);
        Require(clock.TotalHours == savedHours, "Level change reset time.");
        nextRig.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void VerifyCloudAnimation(WorldClock clock, DayNightEnvironmentComponent3D component, ShaderMaterial clouds)
    {
        var timeline = component.Profile.CloudTimeline;
        Require(timeline != null && timeline.Curves.Count == 22, "All original base-cloud material curves must be scene-authored.");
        Require(Near(timeline.Sample("_Cloud_SDF_TSb", 0), .041, 1e-6) && Near(timeline.Sample("_Cloud_SDF_TSb", 1), .05, 1e-6),
            "Source endpoints must not be silently repaired or approximated.");
        float refreshRate = component.Profile.VisualUpdatesPerSecond;
        component.Profile.VisualUpdatesPerSecond = 1;
        component.RefreshProfile();
        clock.Speed = 1;
        clock.Paused = false;
        component.SetDate(2, 12.2);
        double noise = clouds.GetShaderParameter("noise_time").AsDouble();
        double shape = clouds.GetShaderParameter("cloud_sdf").AsDouble();
        clock.Tick(1.0 / 60);
        component.OnUpdate(1.0 / 60);
        Require(clouds.GetShaderParameter("noise_time").AsDouble() != noise && clouds.GetShaderParameter("cloud_sdf").AsDouble() != shape,
            "Original noise and SDF must evolve each frame, independently of light refresh.");
        clock.Paused = true;
        noise = clouds.GetShaderParameter("noise_time").AsDouble();
        shape = clouds.GetShaderParameter("cloud_sdf").AsDouble();
        clock.Tick(3);
        component.OnUpdate(3);
        Require(clouds.GetShaderParameter("noise_time").AsDouble() == noise && clouds.GetShaderParameter("cloud_sdf").AsDouble() == shape,
            "Paused clock must freeze cloud animation.");
        component.SetDate(2, 12.2);
        noise = clouds.GetShaderParameter("noise_time").AsDouble();
        shape = clouds.GetShaderParameter("cloud_sdf").AsDouble();
        component.AdvanceHours(.12);
        Require(clouds.GetShaderParameter("noise_time").AsDouble() != noise && clouds.GetShaderParameter("cloud_sdf").AsDouble() != shape,
            "Time seek must update the original cloud shader.");
        component.SetDate(2, 12.2);
        Require(clouds.GetShaderParameter("noise_time").AsDouble() == noise && clouds.GetShaderParameter("cloud_sdf").AsDouble() == shape,
            "Cloud rewind must reproduce the same shape.");
        component.Profile.VisualUpdatesPerSecond = refreshRate;
        component.RefreshProfile();
    }

    private async Task VerifySceneCamera()
    {
        // 从真正关卡提取已序列化的相机，检查 Inspector 设置不会被代码覆盖。
        var level = GD.Load<PackedScene>("res://assets/scenes/spacelevel.tscn").Instantiate<GameObject3D>();
        var rig = level.GetNode<GameObject3D>("CameraRig");
        var camera = rig.GetNode<Camera3D>("Camera3D");
        Require(camera.Current && camera.Projection == Camera3D.ProjectionType.Orthogonal, "Authored level camera missing.");
        level.RemoveChild(rig);
        level.Free();
        var owner = new Node3D();
        var target = new Node3D { Position = new Vector3(1.6f, -8.84f, 0) };
        owner.AddChild(target);
        owner.AddChild(rig);
        camera.Projection = Camera3D.ProjectionType.Perspective;
        camera.Fov = 47;
        camera.Size = 6.8f;
        camera.RotationDegrees = new Vector3(-18, 12, 0);
        AddChild(owner);
        var initial = camera.GlobalTransform;
        var module = ModuleSystem.GetModule<ICameraModule>();
        var profile = new CameraProfile3D { EnableManualLook = false };
        int rootsBefore = GetTree().Root.GetChildCount();
        module.Activate(owner, rig, target, profile);
        Require(module.Camera == camera && module.Rig == rig && module.FollowTarget == target, "Module must use the scene camera.");
        Require(camera.GlobalTransform.IsEqualApprox(initial) && camera.Size == 6.8f && camera.Fov == 47 &&
            camera.Projection == Camera3D.ProjectionType.Perspective, "Authored camera settings were overwritten.");
        target.Position += Vector3.Right * 3;
        module.SnapToTarget();
        Require(camera.GlobalPosition.IsEqualApprox(initial.Origin + Vector3.Right * 3), "Scene camera follow offset changed.");
        Require(camera.GlobalBasis.IsEqualApprox(initial.Basis), "Scene camera rotation changed.");
        module.SetViewHeight(7.2f, true);
        Require(Near(camera.Size, 7.2, 1e-5), "Runtime zoom failed.");
        module.Release(owner);
        Require(!module.IsActive && module.Camera == null && GodotObject.IsInstanceValid(rig), "Release must not delete scene nodes.");
        Require(GetTree().Root.GetChildCount() == rootsBefore && GetTree().Root.GetNodeOrNull("GlobalCameraRig") == null,
            "Camera module generated a runtime rig.");
        module.Activate(owner, rig, target, profile);
        owner.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(!module.IsActive && module.Camera == null, "Scene exit leaked camera ownership.");
        var nextLevel = GD.Load<PackedScene>("res://assets/scenes/spacelevel.tscn").Instantiate<GameObject3D>();
        var nextRig = nextLevel.GetNode<GameObject3D>("CameraRig");
        nextLevel.RemoveChild(nextRig);
        nextLevel.Free();
        var nextOwner = new Node3D();
        var nextTarget = new Node3D { Name = "Player", Position = new Vector3(1.6f, -8.84f, 0) };
        nextOwner.AddChild(nextRig); // 相机先 Ready，目标后 Ready。
        nextOwner.AddChild(nextTarget);
        AddChild(nextOwner);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(module.IsActive && module.Rig == nextRig && module.FollowTarget == nextTarget, "Scene camera did not bind automatically.");
        Vector3 cameraBefore = module.Camera.GlobalPosition;
        nextTarget.Position += Vector3.Right * 2;
        for (int frame = 0; frame < 30; frame++) nextRig.GetComponent<SceneCameraComponent3D>().OnUpdate(1.0 / 60);
        Require(module.Camera.GlobalPosition.X > cameraBefore.X + 1, "Normal frame updates did not follow the player.");
        nextOwner.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(!module.IsActive, "Automatic scene camera leaked on exit.");
    }

    private async Task VerifyGm(WorldClock clock)
    {
        var packed = GD.Load<PackedScene>("res://assets/scenes/ui/time_of_day_gm.tscn");
        var gm = packed.Instantiate<GameObject3D>();
        var input = ModuleSystem.GetModule<IInputModule>();
        bool cameraEnabled = input.IsLayerEnabled("Camera");
        input.DisableLayer("Camera");
        AddChild(gm);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var controller = gm.GetComponent<TimeOfDayGmComponent3D>();
        var panel = gm.GetNode<Control>("%Panel");
        var toggle = gm.GetNode<Button>("%Toggle");
        Require(toggle.Shortcut.Events[0].AsGodotObject() is InputEventKey key && key.Keycode == Key.F8, "GM shortcut must be F8.");
        Require(!panel.Visible, "GM should start collapsed.");
        toggle.EmitSignal(BaseButton.SignalName.Pressed);
        Require(panel.Visible && !input.IsLayerEnabled("Global"), "Opening GM must isolate gameplay input.");
        clock.Paused = true;
        gm.GetNode<Button>("%Noon").EmitSignal(BaseButton.SignalName.Pressed);
        Require(Near(clock.State.Hour, 12), "GM noon preset.");
        var slider = gm.GetNode<HSlider>("%TimeSlider");
        slider.Value = 30;
        gm.GetNode<SpinBox>("%Day").Value = 22;
        gm.GetNode<SpinBox>("%Hour").Value = 17;
        gm.GetNode<SpinBox>("%Minute").Value = 45;
        Require(clock.State.Day == 22 && Near(clock.State.Hour, 17.75), "GM date and precise time.");
        gm.GetNode<SpinBox>("%Speed").Value = 4;
        Require(clock.Speed == 4, "GM speed.");
        gm.GetNode<OptionButton>("%Phase").EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
        Require(Near(clock.State.LunarPhase, .125), "GM moon preset.");
        gm.GetNode<HSlider>("%MoonSlider").Value = .75;
        Require(Near(clock.State.LunarPhase, .75), "GM continuous moon phase.");
        gm.GetNode<CheckButton>("%Pause").ButtonPressed = false;
        Require(!clock.Paused, "GM resume.");
        slider.EmitSignal(Slider.SignalName.DragStarted);
        Require(clock.Paused, "Scrubbing must hold the clock.");
        slider.Value = 720;
        slider.EmitSignal(Slider.SignalName.DragEnded, true);
        Require(!clock.Paused && Near(clock.State.Hour, 12), "Scrubbing must preserve prior playing state.");
        clock.Paused = true;
        slider.EmitSignal(Slider.SignalName.DragStarted);
        slider.EmitSignal(Slider.SignalName.DragEnded, false);
        Require(clock.Paused, "Scrubbing resumed an already paused clock.");
        GetTree().Paused = true;
        slider.Value = 1100;
        Require(Near(clock.State.Hour, 1100.0 / 60), "GM edit during game pause.");
        GetTree().Paused = false;
        gm.GetNode<Button>("%Close").EmitSignal(BaseButton.SignalName.Pressed);
        Require(!panel.Visible && input.IsLayerEnabled("Global") && !input.IsLayerEnabled("Camera"), "GM input restore must preserve existing disabled layers.");
        controller.ShowPanel(true);
        gm.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(input.IsLayerEnabled("Global"), "Destroying an open GM leaked input lock.");
        if (cameraEnabled) input.EnableLayer("Camera");
    }

    private static bool Near(double a, double b, double epsilon = 1e-7) => Math.Abs(a - b) < epsilon;
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void ExpectInvalid(Action action)
    {
        try { action(); } catch (ArgumentOutOfRangeException) { return; }
        throw new InvalidOperationException("Invalid input was accepted.");
    }
}
