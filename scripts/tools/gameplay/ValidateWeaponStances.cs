using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Framework;
using GameLogic;
using Godot;

// Integration check against the real player scene, input graph, weapon and animations.
public partial class ValidateWeaponStances : Node3D
{
    private GameObject3D _player;
    private WeaponEquipmentComponent3D _equipment;
    private AbilitySystemComponent3D _abilities;
    private CharacterAnimationComponent3D _animation;
    private WorldEnvironment _previewWorld;

    public override void _Process(double delta) => RelinkPostProcessEffect.CaptureCharacterSprites(_previewWorld);

    public override async void _Ready()
    {
        try
        {
            var floor = new StaticBody3D { Position = new Vector3(0, -0.1f, 0) };
            floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(40, 0.2f, 40) } });
            AddChild(floor);
            _player = CreatePlayer("StancePlayer", Vector3.Zero);
            _equipment = _player.GetComponent<WeaponEquipmentComponent3D>();
            _abilities = _player.GetComponent<AbilitySystemComponent3D>();
            _animation = _player.GetComponent<CharacterAnimationComponent3D>();
            await Frames(30);
            if (OS.GetCmdlineUserArgs().Contains("--capture") || OS.GetCmdlineUserArgs().Contains("--forest-capture") ||
                OS.GetCmdlineUserArgs().Contains("--movement-capture"))
            {
                // Fixed-fps capture uses simulation time; ability cooldown checks below use the live clock.
                var previewOther = CreatePlayer("PreviewPlaceholder", new Vector3(8, 0, 0));
                if (OS.GetCmdlineUserArgs().Contains("--forest-capture")) await CaptureForestPreview(previewOther);
                else await CapturePreview(previewOther);
                GD.Print("STANCE_CAPTURE: PASS"); GetTree().Quit(); return;
            }
            Check(_player.InitializationError == string.Empty, "player initializes");
            ExpectIdle("mid");
            SpriteFrames frames = _animation.Sprite.SpriteFrames;
            Check(!frames.HasAnimation("idle") && !_equipment.CurrentWeapon.AnimationProfile.Frames.HasAnimation("idle"), "legacy idle is removed");
            Check(frames.HasAnimation("run") && frames.HasAnimation("attack"), "locomotion and attack survive composition");
            foreach (string stance in new[] { "high", "mid", "low" })
            {
                Check(frames.GetFrameCount("idle_" + stance) == 72 && frames.GetAnimationSpeed("idle_" + stance) == 24,
                    stance + " imports 72 frames at 24 fps");
                Check(_equipment.CurrentWeapon.StanceMoveAnimations[stance] == "run_" + stance &&
                    frames.GetAnimationLoopMode("run_" + stance) == SpriteFrames.LoopMode.Linear,
                    stance + " has its own looping movement");
            }
            await CheckMovementLeadIns();
            if (OS.GetCmdlineUserArgs().Contains("--movement-check"))
            {
                GD.Print("MOVEMENT_START_CHECK: PASS"); GetTree().Quit(); return;
            }

            await MouseTap(MouseButton.WheelUp);
            ExpectIdle("mid");
            KeyInput(Key.Shift, true);
            await Frames(2);
            await MouseTap(MouseButton.WheelUp, true);
            Check(_equipment.IsTransitioning && _equipment.CurrentStance == "mid" && _equipment.TransitionTarget == "high", "switch plays an edge before committing stance");
            Check(_animation.ActiveAnimation == "stance_mid_to_high", "correct directed transition plays");
            var effect = _player.GetComponent<StanceSwitchEffectComponent3D>();
            Check(effect.IsPlaying && _player.GetNode<Node3D>("StanceSwitchEffect").Visible, "switch starts the external full-body effect");
            Check(_animation.Sprite.MaterialOverlay == null && _animation.Sprite.MaterialOverride == null,
                "external effect never modifies the character material");
            Check(!_equipment.TryPrimaryAttack(), "transition cannot leak an attack");
            await SettleStance();
            ExpectIdle("high");
            _animation.Sprite.SetFrameAndProgress(13, 0.25f);
            _equipment.RequestStance("high");
            Check(_animation.Sprite.Frame == 13, "same stance does not restart idle");
            Check(!_equipment.RequestStance("missing"), "unknown stance rejected");

            await MouseTap(MouseButton.WheelDown, true);
            await SettleStance();
            ExpectIdle("low");
            await MouseTap(MouseButton.WheelDown, true);
            ExpectIdle("low");
            Check(!_equipment.IsTransitioning, "reselecting low guard is a no-op");
            await MouseTap(MouseButton.Left, true);
            await SettleStance();
            ExpectIdle("mid");
            Check(!_equipment.IsBusy, "Shift-click reaches middle guard without attacking");
            KeyInput(Key.Shift, false);
            await Frames(2);

            await MouseTap(MouseButton.Left);
            AbilityRuntime attack = _abilities.GetRuntime("greatsword_attack");
            Check(attack.IsRunning && _animation.ActiveAnimation == "attack", "plain click still attacks");
            double cooldown = attack.CooldownReadyTime;
            ulong version = attack.ActivationVersion;
            KeyInput(Key.Shift, true);
            await Frames(2);
            await MouseTap(MouseButton.WheelUp, true);
            await MouseTap(MouseButton.Left, true);
            Check(_equipment.PendingStance == string.Empty, "selecting current stance cancels queued target");
            await MouseTap(MouseButton.WheelDown, true);
            Check(_equipment.CurrentStance == "mid" && _equipment.PendingStance == "low", "attack queues last stance");
            Check(_animation.ActiveAnimation == "attack" && attack.IsRunning, "stance input preserves active attack");
            KeyInput(Key.Shift, false);
            await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
            await SettleStance();
            ExpectIdle("low");
            Check(attack.CooldownReadyTime == cooldown && attack.ActivationVersion == version, "switch preserves skill cooldown and activation");
            Check(ReferenceEquals(frames, _animation.Sprite.SpriteFrames), "switch does not replace playback frames");

            Check(_equipment.TryPrimaryAttack(), "attack available after cooldown");
            _equipment.RequestStance("high");
            _equipment.RequestStance("low");
            Check(_equipment.PendingStance == string.Empty, "requesting current stance clears pending switch");
            _equipment.RequestStance("high");
            _abilities.CancelAbility("greatsword_attack", "Validation");
            await Frames(3);
            ExpectIdle("low");
            Check(_equipment.PendingStance == string.Empty, "interrupt clears pending switch");

            JoyInput(JoyButton.RightShoulder, true);
            await Frames(2);
            foreach (var (button, stance) in new[] { (JoyButton.Y, "high"), (JoyButton.X, "mid"), (JoyButton.A, "low") })
            {
                JoyInput(button, true);
                await Frames(2);
                await SettleStance();
                ExpectIdle(stance);
                JoyInput(button, false);
                await Frames(2);
            }
            JoyInput(JoyButton.RightShoulder, false);
            await Frames(2);
            GD.Print("STANCE_CHECK: keyboard and controller chords passed");

            // Windows sends the wheel press and release back-to-back in the same frame.
            KeyInput(Key.Shift, true);
            await Frames(2);
            foreach (var (wheel, stance) in new[] {
                (MouseButton.WheelUp, "high"), (MouseButton.Left, "mid"), (MouseButton.WheelUp, "high"),
                (MouseButton.WheelDown, "low"), (MouseButton.WheelDown, "low") })
            {
                MouseInput(wheel, true, true);
                MouseInput(wheel, false, true);
                await Frames(3);
                await SettleStance();
                ExpectIdle(stance);
            }
            KeyInput(Key.Shift, false);
            await Frames(2);
            GD.Print("STANCE_CHECK: native Nioh mouse chords passed");

            // Chained switches wait only for the visible edge, with no extra cooldown.
            ulong firstSwitch = 0, secondSwitch = 0;
            void TrackSwitch(string stance)
            {
                if (firstSwitch == 0) firstSwitch = Engine.GetPhysicsFrames();
                else secondSwitch = Engine.GetPhysicsFrames();
            }
            _equipment.StanceSwitchStarted += TrackSwitch;
            KeyInput(Key.Shift, true);
            MouseInput(MouseButton.WheelUp, true, true);
            MouseInput(MouseButton.WheelUp, false, true);
            await Frames(2);
            MouseInput(MouseButton.WheelDown, true, true);
            MouseInput(MouseButton.WheelDown, false, true);
            await Frames(2);
            Check(_equipment.TransitionTarget == "high" && _equipment.PendingStance == "low", "fast input retains the latest direct target");
            await SettleStance();
            ExpectIdle("low");
            _equipment.StanceSwitchStarted -= TrackSwitch;
            Check(secondSwitch > firstSwitch && secondSwitch - firstSwitch < 14, "next switch starts without a 0.3-second cooldown");
            await Frames(20);
            Check(!effect.IsPlaying && !_player.GetNode<Node3D>("StanceSwitchEffect").Visible, "external effect stops after its lifetime");
            _equipment.RequestStance("low");
            Check(!effect.IsPlaying, "same stance does not flash");
            KeyInput(Key.Shift, false);
            await Frames(2);

            // A native mouse chord must survive releasing Shift before the next physics tick.
            _equipment.RequestStance("mid");
            await SettleStance();
            KeyInput(Key.Shift, true);
            MouseInput(MouseButton.WheelDown, true, true);
            MouseInput(MouseButton.WheelDown, false, true);
            KeyInput(Key.Shift, false);
            await Frames(3);
            await SettleStance();
            ExpectIdle("low");
            GD.Print("STANCE_CHECK: quick-release Shift-wheel chord passed");

            KeyInput(Key.Shift, true);
            MouseInput(MouseButton.Left, true, true);
            MouseInput(MouseButton.Left, false, true);
            KeyInput(Key.Shift, false);
            await Frames(3);
            await SettleStance();
            Check(!attack.IsRunning, "quick-release Shift-click does not leak an attack");
            ExpectIdle("mid");
            await MouseTap(MouseButton.WheelUp);
            ExpectIdle("mid");
            JoyInput(JoyButton.Y, true);
            await Frames(3);
            JoyInput(JoyButton.Y, false);
            ExpectIdle("mid");

            var inputProvider = _player.GetComponent<PlayerCharacterInputComponent3D>();
            inputProvider.GameplayBlocked = true;
            KeyInput(Key.Shift, true);
            await Frames(2);
            await MouseTap(MouseButton.WheelUp, true);
            Check(_equipment.CurrentStance == "mid", "gameplay input lock blocks stance");
            KeyInput(Key.Shift, false);
            await Frames(2);
            inputProvider.GameplayBlocked = false;
            KeyInput(Key.Shift, true);
            MouseInput(MouseButton.WheelUp, true, true);
            var input = ModuleSystem.GetModule<IInputModule>();
            input.TryConsumeJustPressed("player_stance_high", "UI");
            var node = new CharacterInputActionNodeData { ActionName = "player_stance_high", HandlerLayer = "Combat", BufferTime = 0 };
            Check(!node.IsTriggered(inputProvider), "UI-consumed chord is filtered");
            MouseInput(MouseButton.WheelUp, false, true);
            KeyInput(Key.Shift, false);
            await Frames(2);

            foreach (string stance in new[] { "high", "mid", "low" })
            {
                KeyInput(Key.D, true);
                _equipment.RequestStance(stance);
                await SettleStance();
                await WaitForMovementLoop(stance);
                Check(_animation.ActiveAnimation == "run_" + stance, "moving switch resumes target guard locomotion");
                int previousFrame = _animation.Sprite.Frame;
                await Frames(8);
                Check(_animation.Sprite.Frame != previousFrame, "stance movement advances");
                ExpectFootBaseline();
                KeyInput(Key.D, false);
                await Frames(12);
                ExpectIdle(stance);
            }

            KeyInput(Key.D, true);
            await Frames(5);
            Check(_animation.ActiveAnimation == "run_low", "low guard movement resolves through the run alias");
            _equipment.RequestStance("high");
            Check(_equipment.IsTransitioning, "ground switching plays a transition while running");
            await Frames(3);
            Check(_player.GetComponent<CharacterMovementComponent3D>().MovementLocked, "feet are planted during transition");
            KeyInput(Key.D, false);
            await SettleStance();
            await Frames(3);
            ExpectIdle("high");
            KeyInput(Key.Space, true);
            await Frames(6);
            _equipment.RequestStance("low");
            Check(!_equipment.IsTransitioning && _equipment.PendingStance == "low", "airborne switch waits for landing");
            KeyInput(Key.Space, false);
            await Frames(60);
            await SettleStance();
            ExpectIdle("low");

            // A visible edge completes before following the most recent request.
            _equipment.RequestStance("high");
            _equipment.RequestStance("mid");
            _equipment.RequestStance("low");
            Check(_equipment.TransitionTarget == "high" && _equipment.PendingStance == "low", "in-flight input keeps only latest target, including return to source");
            await SettleStance();
            ExpectIdle("low");
            _equipment.RequestStance("high");
            await SettleStance();
            JsonObject saved = _equipment.CaptureDurableState();
            _equipment.RequestStance("low");
            _equipment.RestoreDurableState(saved);
            ExpectIdle("high");
            Check(!_equipment.IsTransitioning && _equipment.PendingStance == string.Empty, "load clears transition and queued target");
            Check(!effect.IsPlaying && _animation.Sprite.MaterialOverlay == null, "loading clears the flash");
            saved["stance"] = "unknown";
            _equipment.RestoreDurableState(saved);
            ExpectIdle("mid");
            saved.Remove("stance");
            _equipment.RequestStance("low");
            _equipment.RestoreDurableState(saved);
            ExpectIdle("mid");

            GameObject3D other = CreatePlayer("OtherStancePlayer", new Vector3(3, 0, 0));
            other.GetComponent<PlayerCharacterInputComponent3D>().GameplayBlocked = true;
            await Frames(30);
            _equipment.RequestStance("high");
            Check(other.GetComponent<WeaponEquipmentComponent3D>().CurrentStance == "mid", "instances have independent stances");
            Check(!other.GetComponent<StanceSwitchEffectComponent3D>().IsPlaying &&
                other.GetComponent<CharacterAnimationComponent3D>().Sprite.MaterialOverlay == null, "flash is local to the switching character");
            Check(_equipment.CurrentWeapon.AnimationProfile.AdditionalFrames.GetAnimationNames().Length == 3, "shared art remains unchanged");
            Check(!_equipment.CurrentWeapon.AnimationProfile.Frames.HasAnimation("idle_high"), "base frames are not mutated");
            Check(!_equipment.CurrentWeapon.AnimationProfile.Frames.HasAnimation("run_high") &&
                _equipment.CurrentWeapon.AnimationProfile.ActionFrames.GetAnimationNames().Length == 5,
                "stance movement composition leaves shared base and action resources unchanged");
            _player.OnRecycle();
            _player.OnSpawn();
            await Frames(10);
            ExpectIdle("mid");
            Check(_equipment.PendingStance == string.Empty, "recycle clears transient stance");
            Check(!_equipment.IsTransitioning && !_player.GetComponent<CharacterMovementComponent3D>().MovementLocked, "recycle releases transition lock");
            Check(!effect.IsPlaying && _animation.Sprite.MaterialOverlay == null, "recycle clears the flash");
            var children = _player.GetChildren();
            using (var childrenOwner = (Godot.Collections.Array)children)
                Check(children.Count(child => child.Name == "StanceSwitchEffect") == 1,
                    "recycle creates exactly one external effect instance");
            foreach (string source in new[] { "high", "mid", "low" })
                foreach (string target in new[] { "high", "mid", "low" })
                {
                    if (source == target) continue;
                    _equipment.RequestStance(source);
                    await SettleStance();
                    _equipment.RequestStance(target);
                    Check(_animation.ActiveAnimation == "stance_" + source + "_to_" + target, "all six directed transitions are wired");
                    await SettleStance();
                    ExpectIdle(target);
                }
            GD.Print("STANCE_CHECK: attack, locomotion, geometry, persistence and instance isolation passed");

            GD.Print("STANCE_CHECK: PASS");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError("STANCE_CHECK: FAIL " + error);
            GetTree().Quit(1);
        }
    }

    private GameObject3D CreatePlayer(string name, Vector3 position)
    {
        var player = GD.Load<PackedScene>("res://assets/scenes/player.tscn").Instantiate<GameObject3D>();
        player.Name = name;
        player.Position = position + new Vector3(0, 0.5f, 0);
        // Do not register test actors in the user's save or world session.
        var authoredComponents = player.Components;
        using var authoredComponentsOwner = (Godot.Collections.Array)authoredComponents;
        player.Components = new Godot.Collections.Array<Component3D>(authoredComponents.Where(component =>
            component is not CharacterPersistenceComponent3D and not PlayerWorldInteractionComponent3D and not CharacterDataComponent3D));
        player.GetNodeOrNull("WorldInteractionHUD")?.Free();
        AddChild(player);
        return player;
    }

    private void ExpectIdle(string stance)
    {
        Check(_equipment.CurrentStance == stance, $"expected {stance}, got {_equipment.CurrentStance}");
        Check(_animation.ActiveAnimation == "idle_" + stance, $"expected idle_{stance}, got {_animation.ActiveAnimation}");
        ExpectFootBaseline();
    }

    private void ExpectFootBaseline()
    {
        var profile = _equipment.CurrentWeapon.AnimationProfile;
        Vector2I size = profile.FrameSizeFor(_animation.Sprite.Animation);
        Vector2I anchor = profile.FootAnchorFor(_animation.Sprite.Animation);
        float foot = _animation.Sprite.Position.Y + (size.Y * 0.5f - anchor.Y) * _animation.Sprite.PixelSize * _animation.Sprite.Scale.Y;
        Check(Mathf.IsEqualApprox(foot, -_player.GetComponent<CharacterMovementComponent3D>().HalfHeight), "foot stays on body baseline");
    }

    private async Task CapturePreview(GameObject3D other)
    {
        other.QueueFree();
        _player.QueueFree();
        await Frames(2);
        var profile = GD.Load<RelinkPostProcessProfile>("res://assets/resources/rendering/relink_post/profile.tres");
        _previewWorld = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.08f, 0.09f, 0.12f),
                BackgroundIntensity = 1,
                TonemapMode = Godot.Environment.ToneMapper.Linear
            },
            Compositor = new Compositor { CompositorEffects = new Godot.Collections.Array<CompositorEffect>
            {
                new RelinkPostProcessEffect { Pass = RelinkPostProcessEffect.PassKind.ColorGrade, Profile = profile },
                new RelinkPostProcessEffect { Profile = profile, PixelGridHeight = 270 }
            } }
        };
        AddChild(_previewWorld);
        var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 2.3f, Position = new Vector3(0, 1.0f, 7), Current = true };
        AddChild(camera);
        var overlay = new CanvasLayer();
        AddChild(overlay);
        string[] names = { "high", "mid", "low" };
        var actors = new GameObject3D[names.Length];
        var labels = new Label[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            GameObject3D actor = CreatePlayer("Preview" + i, new Vector3((i - 1) * 1.15f, 0, 0));
            actors[i] = actor;
            actor.GetComponent<PlayerCharacterInputComponent3D>().GameplayBlocked = true;
            actor.GetComponent<WeaponEquipmentComponent3D>().RequestStance(names[i]);
            AnimatedSprite3D sprite = actor.GetComponent<CharacterAnimationComponent3D>().Sprite;
            sprite.Shaded = false;
            sprite.Modulate = Colors.White;
            var label = new Label { Text = names[i].ToUpperInvariant(), Position = new Vector2(130 + i * 365, 28) };
            labels[i] = label;
            label.AddThemeFontSizeOverride("font_size", 24);
            label.AddThemeColorOverride("font_color", new Color(0.28f, 0.3f, 0.35f));
            overlay.AddChild(label);
        }
        await Frames(30);
        if (OS.GetCmdlineUserArgs().Contains("--movement-capture"))
        {
            string movementFolder = "res://.godot/stance-actions/runtime-movement";
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(movementFolder));
            for (int frame = 0; frame < 150; frame++)
            {
                if (frame == 15)
                    foreach (var actor in actors)
                        actor.GetComponent<CharacterAnimationComponent3D>().RequestAnimation("MovementPreview", "run", 30);
                if (frame == 135)
                    foreach (var actor in actors)
                        actor.GetComponent<CharacterAnimationComponent3D>().ClearAnimationRequest("MovementPreview");
                await Frames(1);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = GetViewport().GetTexture().GetImage();
                Check(image.SavePng(movementFolder + $"/frame-{frame:D3}.png") == Error.Ok, "movement preview frame saved");
            }
            GD.Print("STANCE_CHECK: movement capture " + ProjectSettings.GlobalizePath(movementFolder));
            return;
        }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string path = "res://.godot/stance-switch/runtime-preview.png";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://.godot/stance-switch"));
        Check(GetViewport().GetTexture().GetImage().SavePng(path) == Error.Ok, "runtime screenshot saved");
        GD.Print("STANCE_CHECK: preview " + ProjectSettings.GlobalizePath(path));

        string[] sources = { "high", "mid", "high" };
        string[] targets = { "mid", "low", "low" };
        for (int i = 0; i < actors.Length; i++)
        {
            actors[i].GetComponent<WeaponEquipmentComponent3D>().RequestStance(sources[i]);
            labels[i].Text = sources[i].ToUpperInvariant() + " / " + targets[i].ToUpperInvariant();
        }
        await Frames(30);
        string folder = "res://.godot/stance-switch/effect-preview-relink";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        for (int frame = 0; frame < 90; frame++)
        {
            if (frame == 12 || frame == 54)
                for (int i = 0; i < actors.Length; i++)
                    actors[i].GetComponent<WeaponEquipmentComponent3D>().RequestStance(frame == 12 ? targets[i] : sources[i]);
            await Frames(1);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Check(GetViewport().GetTexture().GetImage().SavePng(folder + $"/frame-{frame:D3}.png") == Error.Ok, "effect preview frame saved");
        }
        GD.Print("STANCE_CHECK: six-way flash capture " + ProjectSettings.GlobalizePath(folder));
    }

    private async Task CaptureForestPreview(GameObject3D other)
    {
        other.QueueFree(); _player.QueueFree();
        await Frames(2);
        Node3D authored = GD.Load<PackedScene>("res://assets/scenes/levels/forest_town_level.tscn").Instantiate<Node3D>();
        var rig = authored.GetNode<Node3D>("EnvironmentRig");
        var camera = (Camera3D)authored.GetNode<Camera3D>("CameraRig/Camera3D").Duplicate();
        Vector3 spawn = authored.GetNode<Node3D>("EntryDefault/Arrival").Position;
        foreach (string name in new[] { "EnvironmentRig", "Village" })
        {
            Node child = authored.GetNode(name);
            child.Owner = null; authored.RemoveChild(child); AddChild(child);
        }
        AddChild(camera); camera.MakeCurrent();
        authored.Free();
        var actor = CreatePlayer("ForestStancePreview", spawn - new Vector3(0, .5f, 0));
        actor.GetComponent<PlayerCharacterInputComponent3D>().GameplayBlocked = true;
        var weapon = actor.GetComponent<WeaponEquipmentComponent3D>();
        _previewWorld = rig.GetNode<WorldEnvironment>("WorldEnvironment");
        rig.GetMeta("time_of_day").As<GodotObject>().Call("SetPaused", true);
        rig.GetMeta("time_of_day").As<GodotObject>().Call("SetTimeOfDay", 12f);
        rig.GetMeta("weather").As<GodotObject>().Call("SetAutomatic", false);
        rig.GetMeta("weather").As<GodotObject>().Call("SetWeather", 0, true);
        weapon.RequestStance("high");
        await Frames(120);
        string folder = "res://.godot/stance-switch/forest-particles";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        for (int frame = 0; frame < 120; frame++)
        {
            if (frame == 12) weapon.RequestStance("mid");
            if (frame == 48) weapon.RequestStance("low");
            if (frame == 84) weapon.RequestStance("high");
            await Frames(1);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = GetViewport().GetTexture().GetImage();
            Check(image.SavePng(folder + $"/frame-{frame:D3}.png") == Error.Ok, "forest preview saved");
        }
        GD.Print("STANCE_CHECK: authored forest particle capture " + ProjectSettings.GlobalizePath(folder));
    }

    private async Task CheckMovementLeadIns()
    {
        const string request = "MovementStartCheck";
        foreach (string stance in new[] { "high", "mid" })
        {
            _equipment.RequestStance(stance);
            await SettleStance();
            string start = _equipment.CurrentWeapon.StanceMoveStartAnimations[stance];
            Check(_equipment.CurrentWeapon.AnimationProfile.IsOneShot(start), "movement start is a one-shot");
            _animation.RequestAnimation(request, "run", 30);
            await Frames(2);
            Check(_animation.ActiveAnimation == start, stance + " turns before walking");
            await WaitForMovementLoop(stance);
            for (int frame = 0; frame < 100; frame++)
            {
                await Frames(1);
                Check(_animation.ActiveAnimation == "run_" + stance, "walking does not repeat the turn");
            }
            _animation.ClearAnimationRequest(request);
            await Frames(3);
            ExpectIdle(stance);
            _animation.RequestAnimation(request, "run", 30);
            await Frames(2);
            Check(_animation.ActiveAnimation == start, "new movement replays the start");
            _animation.RequestAnimation("MovementInterruptCheck", "attack", 100);
            await Frames(2);
            Check(_animation.ActiveAnimation == "attack", "higher-priority ability interrupts movement start");
            _animation.ClearAnimationRequest("MovementInterruptCheck");
            await Frames(2);
            Check(_animation.ActiveAnimation == start, "movement can resume after interruption");
            _animation.ClearAnimationRequest(request);
            await Frames(3);
            ExpectIdle(stance);
        }
        _equipment.RequestStance("low");
        await SettleStance();
        _animation.RequestAnimation(request, "run", 30);
        await Frames(2);
        Check(_animation.ActiveAnimation == "run_low", "stance without a start plays its loop directly");
        _animation.ClearAnimationRequest(request);
        _equipment.RequestStance("mid");
        await SettleStance();
        ExpectIdle("mid");
    }

    private async Task WaitForMovementLoop(string stance)
    {
        for (int frame = 0; frame < 180 && _animation.ActiveAnimation != "run_" + stance; frame++)
            await Frames(1);
        Check(_animation.ActiveAnimation == "run_" + stance, "movement start finishes into the correct stance loop");
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private async Task SettleStance()
    {
        for (int i = 0; i < 150 && (_equipment.IsTransitioning || !string.IsNullOrEmpty(_equipment.PendingStance)); i++)
            await Frames(1);
        Check(!_equipment.IsTransitioning && string.IsNullOrEmpty(_equipment.PendingStance), "transition finishes within 2.5 seconds");
        await Frames(2);
    }

    private async Task MouseTap(MouseButton button, bool shift = false)
    {
        MouseInput(button, true, shift);
        await Frames(2);
        MouseInput(button, false, shift);
        await Frames(2);
    }

    private static void MouseInput(MouseButton button, bool pressed, bool shift = false) =>
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = pressed, ShiftPressed = shift });
    private static void KeyInput(Key key, bool pressed) =>
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = pressed, ShiftPressed = key == Key.Shift && pressed });
    private static void JoyInput(JoyButton button, bool pressed) =>
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = pressed });
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
