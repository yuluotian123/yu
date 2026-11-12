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
                OS.GetCmdlineUserArgs().Contains("--movement-capture") || OS.GetCmdlineUserArgs().Contains("--attack-capture"))
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
            await CheckStanceAttacks();
            await CheckAttackFacing();
            await CheckSingleAttacks();
            await CheckComboFramework();

            await MouseTap(MouseButton.WheelUp);
            ExpectIdle("mid");
            KeyInput(Key.Shift, true);
            await Frames(2);
            await MouseTap(MouseButton.WheelUp, true);
            Check(_equipment.IsTransitioning && _equipment.CurrentStance == "mid" && _equipment.TransitionTarget == "high", "switch plays an edge before committing stance");
            Check(_animation.ActiveAnimation == "stance_mid_to_high", "correct directed transition plays");
            var effect = _abilities.Effects;
            Check(effect.IsScenePlaying("stance-switch") && _player.GetNode<Node3D>("AbilityEffect_stance-switch").Visible, "switch starts the external full-body effect");
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
            AbilityRuntime attack = _abilities.GetRuntime(_equipment.CurrentPrimaryAttack.AbilityId);
            Check(attack.IsRunning && _animation.ActiveAnimation == "attack_mid", "plain click attacks from the current guard");
            double cooldown = attack.CooldownReadyTime;
            ulong version = attack.ActivationVersion;
            KeyInput(Key.Shift, true);
            await Frames(2);
            await MouseTap(MouseButton.WheelUp, true);
            await MouseTap(MouseButton.Left, true);
            Check(_equipment.PendingStance == string.Empty, "selecting current stance cancels queued target");
            await MouseTap(MouseButton.WheelDown, true);
            Check(_equipment.CurrentStance == "mid" && _equipment.PendingStance == "low", "attack queues last stance");
            Check(_animation.ActiveAnimation == "attack_mid" && attack.IsRunning, "stance input preserves active attack");
            KeyInput(Key.Shift, false);
            await WaitForAttack(attack);
            await SettleStance();
            ExpectIdle("low");
            Check(attack.CooldownReadyTime == cooldown && attack.ActivationVersion == version, "switch preserves skill cooldown and activation");
            Check(ReferenceEquals(frames, _animation.Sprite.SpriteFrames), "switch does not replace playback frames");

            Check(_equipment.TryPrimaryAttack(), "attack available after cooldown");
            string interruptedAttackId = _equipment.CurrentPrimaryAttack.AbilityId;
            _equipment.RequestStance("high");
            _equipment.RequestStance("low");
            Check(_equipment.PendingStance == string.Empty, "requesting current stance clears pending switch");
            _equipment.RequestStance("high");
            _abilities.CancelAbility(interruptedAttackId, "Validation");
            await Frames(3);
            ExpectIdle("low");
            Check(_equipment.PendingStance == string.Empty, "interrupt clears pending switch");

            GD.Print("STANCE_CHECK: keyboard and mouse chords passed");

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
            Check(!effect.IsScenePlaying("stance-switch") && !_player.GetNode<Node3D>("AbilityEffect_stance-switch").Visible, "external effect stops after its lifetime");
            _equipment.RequestStance("low");
            Check(!effect.IsScenePlaying("stance-switch"), "same stance does not flash");
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
            Check(!effect.IsScenePlaying("stance-switch") && _animation.Sprite.MaterialOverlay == null, "loading clears the flash");
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
            Check(!other.GetComponent<AbilitySystemComponent3D>().Effects.IsScenePlaying("stance-switch") &&
                other.GetComponent<CharacterAnimationComponent3D>().Sprite.MaterialOverlay == null, "flash is local to the switching character");
            Check(!ReferenceEquals(_abilities.Effects, other.GetComponent<AbilitySystemComponent3D>().Effects),
                "effect state belongs to individual actors");
            Check(_equipment.CurrentWeapon.AnimationProfile.AdditionalFrames.GetAnimationNames().Length == 3, "shared art remains unchanged");
            Check(!_equipment.CurrentWeapon.AnimationProfile.Frames.HasAnimation("idle_high"), "base frames are not mutated");
            Check(!_equipment.CurrentWeapon.AnimationProfile.Frames.HasAnimation("run_high") &&
                _equipment.CurrentWeapon.AnimationProfile.ActionFrames.GetAnimationNames().Length == 5,
                "stance movement composition leaves shared base and action resources unchanged");
            Check(!_equipment.CurrentWeapon.AnimationProfile.Frames.HasAnimation("attack_high") &&
                _equipment.CurrentWeapon.AnimationProfile.AttackFrames.GetAnimationNames().Length == 3,
                "stance attacks leave shared base resources unchanged");
            foreach (var definition in _equipment.CurrentWeapon.StancePrimaryAttacks.Values)
                Check(!ReferenceEquals(_abilities.GetRuntime(definition.AbilityId),
                    other.GetComponent<AbilitySystemComponent3D>().GetRuntime(definition.AbilityId)),
                    "stance attack runtimes belong to individual actors");
            _player.OnRecycle();
            _player.OnSpawn();
            await Frames(10);
            ExpectIdle("mid");
            Check(_equipment.PendingStance == string.Empty, "recycle clears transient stance");
            Check(!_equipment.IsTransitioning && !_player.GetComponent<CharacterMovementComponent3D>().MovementLocked, "recycle releases transition lock");
            Check(!effect.IsScenePlaying("stance-switch") && _animation.Sprite.MaterialOverlay == null, "recycle clears the flash");
            var children = _player.GetChildren();
            using (var childrenOwner = (Godot.Collections.Array)children)
                Check(children.Count(child => child.Name == "AbilityEffect_stance-switch") == 0,
                    "recycle frees the lazily created particle scene");
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
        bool captureAttacks = OS.GetCmdlineUserArgs().Contains("--attack-capture");
        var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = captureAttacks ? 4.2f : 2.3f, Position = new Vector3(0, 1.0f, 7), Current = true };
        AddChild(camera);
        var overlay = new CanvasLayer();
        AddChild(overlay);
        string[] names = { "high", "mid", "low" };
        string[] attackLabels = { "HIGH / HEAVY CUT", "MID / HEAVY SWEEP", "LOW / HEAVY THRUST" };
        var actors = new GameObject3D[names.Length];
        var labels = new Label[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            GameObject3D actor = CreatePlayer("Preview" + i, new Vector3((i - 1) * (captureAttacks ? 1.8f : 1.15f), 0, 0));
            actors[i] = actor;
            actor.GetComponent<PlayerCharacterInputComponent3D>().GameplayBlocked = true;
            actor.GetComponent<WeaponEquipmentComponent3D>().RequestStance(names[i]);
            AnimatedSprite3D sprite = actor.GetComponent<CharacterAnimationComponent3D>().Sprite;
            sprite.Shaded = false;
            sprite.Modulate = Colors.White;
            Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
            float labelX = captureAttacks ? viewportSize.X * .5f + (i - 1) * 1.8f / camera.Size * viewportSize.Y - 110 : 130 + i * 365;
            var label = new Label { Text = captureAttacks ? attackLabels[i] : names[i].ToUpperInvariant(), Position = new Vector2(labelX, 28) };
            labels[i] = label;
            label.AddThemeFontSizeOverride("font_size", 24);
            label.AddThemeColorOverride("font_color", new Color(0.28f, 0.3f, 0.35f));
            overlay.AddChild(label);
        }
        await Frames(30);
        if (captureAttacks)
        {
            string attackFolder = "res://.godot/stance-actions/runtime-attacks";
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(attackFolder));
            var trace = new JsonArray();
            bool leftFacing = OS.GetCmdlineUserArgs().Contains("--left-facing");
            if (leftFacing)
                foreach (var actor in actors) actor.GetComponent<CharacterMovementComponent3D>().RestoreFacing(-1);
            double longestAttack = actors.Max(actor => actor.GetComponent<WeaponEquipmentComponent3D>()
                .CurrentPrimaryAttack.Graph.Nodes.OfType<AbilityTimelineNodeData>()
                .Where(node => node.Id.StartsWith("hit_", StringComparison.Ordinal)).Sum(node => node.Duration));
            int attackFrames = (int)Math.Ceiling(longestAttack * 60);
            int secondAttack = 15 + attackFrames + 30;
            for (int frame = 0; frame < secondAttack + attackFrames + 30; frame++)
            {
                if (frame == 15 || frame == secondAttack)
                    foreach (var actor in actors)
                        Check(actor.GetComponent<WeaponEquipmentComponent3D>().TryPrimaryAttack(), "preview starts the stance ability");
                await Frames(1);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = GetViewport().GetTexture().GetImage();
                Check(image.SavePng(attackFolder + $"/frame-{frame:D3}.png") == Error.Ok, "attack preview frame saved");
                var actorFrames = new JsonArray();
                foreach (var actor in actors)
                {
                    var animation = actor.GetComponent<CharacterAnimationComponent3D>();
                    actorFrames.Add(new JsonObject { ["animation"] = animation.ActiveAnimation.ToString(),
                        ["frame"] = animation.Sprite.Frame, ["progress"] = animation.Sprite.FrameProgress,
                        ["facing"] = actor.GetComponent<CharacterMovementComponent3D>().Facing });
                }
                trace.Add(new JsonObject { ["capture_frame"] = frame, ["actors"] = actorFrames });
            }
            using var traceFile = FileAccess.Open(attackFolder + "/frames.json", FileAccess.ModeFlags.Write);
            traceFile.StoreString(trace.ToJsonString());
            GD.Print("STANCE_CHECK: attack capture " + ProjectSettings.GlobalizePath(attackFolder));
            return;
        }
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
        for (int frame = 0; frame < 180; frame++)
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

    private async Task CheckStanceAttacks()
    {
        var weapon = _equipment.CurrentWeapon;
        Check(weapon.StancePrimaryAttacks.Count == 3, "weapon supplies an ability for every stance");
        Check(weapon.StancePrimaryAttacks.Values.Select(ability => ability.AbilityId).Distinct().Count() == 3,
            "stance abilities have independent stable IDs");
        foreach (string stance in new[] { "high", "mid", "low" })
        {
            _equipment.RequestStance(stance);
            await SettleStance();
            AbilityResource definition = weapon.StancePrimaryAttacks[stance];
            AbilityRuntime runtime = _abilities.GetRuntime(definition.AbilityId);
            Check(runtime != null && ReferenceEquals(_equipment.CurrentPrimaryAttack, definition),
                stance + " selects the already granted skill");
            Check(weapon.AnimationProfile.IsOneShot("attack_" + stance), stance + " attack is a one-shot");
            await MouseTap(MouseButton.Left);
            Check(runtime.IsRunning && _animation.ActiveAnimation == "attack_" + stance,
                stance + " click plays its own attack through the ability system");
            Check(_player.GetComponent<CharacterMovementComponent3D>().MovementLocked, "attack holds movement until recovery");
            ExpectFootBaseline();
            double cooldown = runtime.CooldownReadyTime;
            await WaitForAttack(runtime);
            ExpectIdle(stance);
            Check(!_player.GetComponent<CharacterMovementComponent3D>().MovementLocked, "recovery releases movement");
            Check(runtime.CooldownReadyTime == cooldown, "finishing the skill does not reset its cooldown");
            Check(ReferenceEquals(runtime, _abilities.GetRuntime(definition.AbilityId)), "skill runtime survives stance use");
        }
        _equipment.RequestStance("mid");
        await SettleStance();
        GD.Print("STANCE_ATTACK_CHECK: PASS (high, mid, low; selection, animation, recovery, locking, runtime identity)");
    }

    private async Task WaitForAttack(AbilityRuntime runtime)
    {
        for (int i = 0; i < 300 && runtime.IsRunning; i++) await Frames(1);
        Check(!runtime.IsRunning, "attack completes within five seconds");
        await Frames(2);
    }

    private async Task CheckSingleAttacks()
    {
        var graph = _player.GetComponent<CharacterGraphComponent3D>().CharacterGraph;
        Check(graph.Validate(out var characterValidation), characterValidation.ToDisplayText());
        Check(graph.Nodes.OfType<CharacterRegisterAbilityNodeData>().Count() == 1 &&
            graph.Nodes.OfType<CharacterInputActionNodeData>().Count() == 7,
            "one weapon registration retains stance, movement and jump input");
        Check(!graph.Nodes.Any(node => node.Id.EndsWith("_pad", StringComparison.Ordinal)) &&
            !InputMap.HasAction("player_stance_high_pad") && !InputMap.HasAction("player_stance_modifier"),
            "controller stance branches and dedicated bindings are removed");
        Check(_equipment.CurrentWeapon.StanceEffectGraph.Validate(out var effectValidation), effectValidation.ToDisplayText());
        foreach (var registration in graph.Nodes.OfType<CharacterRegisterAbilityNodeData>())
        {
            var abilities = registration.GetAbilities().ToArray();
            Check(abilities.Length == 3 && abilities.All(ability => _equipment.CurrentWeapon.GetPrimaryAttacks().Contains(ability)),
                "one weapon source expands the three stance skills");
            foreach (var ability in abilities)
            {
                Check(ability.Graph.Validate(out var validation), ability.AbilityId + ": " + validation.ToDisplayText());
                Check(ability.Graph.Nodes.OfType<AbilityInputNodeData>().Any(), "registered skill owns its input");
                Check(ability.Graph.Nodes.Count == 3 && ability.Graph.Nodes.OfType<AbilityTimelineNodeData>().Count() == 1,
                    "equipped attack contains one full Timeline and one exit");
                var timeline = ability.Graph.Nodes.OfType<AbilityTimelineNodeData>().Single();
                Check(timeline.Tracks.Count == 1 && timeline.Tracks[0].Clips.Count == 1 &&
                    timeline.Tracks[0].Clips[0].Action is AbilityPlayAnimationAction && timeline.CancelActions.Count == 0,
                    "equipped attack Timeline contains only animation");
            }
        }
        foreach (string stance in new[] { "high", "mid", "low" })
        {
            _equipment.RequestStance(stance);
            await SettleStance();
            var runtime = _abilities.GetRuntime(_equipment.CurrentPrimaryAttack.AbilityId);
            for (int i = 0; i < 100 && _abilities.CanActivateAbility(runtime.AbilityId) != AbilityActivationResult.Activated; i++) await Frames(1);
            await MouseTap(MouseButton.Left);
            ulong version = runtime.ActivationVersion;
            Check(runtime.IsRunning, "physical left click activates the graph");
            await MouseTap(MouseButton.Left);
            await Frames(20);
            await MouseTap(MouseButton.Left);
            MouseInput(MouseButton.Left, true);
            for (int i = 0; i < 100 && runtime.IsRunning; i++)
            {
                Check(_animation.ActiveAnimation == "attack_" + stance, "only the equipped single attack plays through recovery");
                Check(!runtime.IsInputWindowOpen("primary_attack") && !runtime.TryBufferInput("primary_attack"),
                    "single attack has no continuation window");
                await Frames(1);
            }
            Check(!runtime.IsRunning, "single attack completes within its duration");
            await Frames(12); // Holding the button through recovery must not retrigger.
            MouseInput(MouseButton.Left, false);
            await Frames(2);
            Check(runtime.ActivationVersion == version,
                stance + " repeated/held input executes exactly one complete attack");
            Check(!runtime.HasBufferedInput("primary_attack") && !runtime.IsInputWindowOpen("primary_attack"), "completion leaves no queued attack");
            Check(!_player.GetComponent<CharacterMovementComponent3D>().MovementLocked, "recovery releases movement");
            ExpectIdle(stance);
            await MouseTap(MouseButton.Left);
            Check(runtime.IsRunning && runtime.ActivationVersion == version + 1, "a new click after recovery starts a new attack");
            await WaitForAttack(runtime);
        }
        _equipment.RequestStance("mid");
        await SettleStance();
        GD.Print("SINGLE_ATTACK_CHECK: PASS (all stances, physical clicks, spam/hold, complete recovery, fresh click)");
    }

    // Exercise reusable combo branching without keeping unused production animations or graph assets.
    private async Task CheckComboFramework()
    {
        var graph = new AbilityFlowGraphAsset();
        graph.Nodes.Add(new FlowEntryNodeData { Id = "entry" });
        for (int step = 1; step <= 3; step++)
        {
            var hit = new AbilityTimelineNodeData { Id = "hit_" + step, Duration = .2f };
            if (step < 3)
                hit.Tracks.Add(new FlowTimelineTrack { Clips = new()
                {
                    new FlowTimelineClip { StartTime = .03f, Duration = .17f,
                        Action = new AbilityInputWindowAction { InputName = "primary_attack" } }
                } });
            graph.Nodes.Add(hit);
            graph.Nodes.Add(new FlowReturnNodeData { Id = "finished_" + step, Label = "Finished_" + step });
            if (step < 3)
                graph.Nodes.Add(new FlowConditionNodeData { Id = "continue_" + step,
                    Conditions = new() { new AbilityConsumeInputCondition { InputName = "primary_attack" } } });
        }
        graph.Connections.Add(new FlowConnection { FromNode = "entry", ToNode = "hit_1" });
        for (int step = 1; step <= 3; step++)
        {
            graph.Connections.Add(new FlowConnection { FromNode = "hit_" + step,
                ToNode = (step < 3 ? "continue_" : "finished_") + step });
            if (step == 3) continue;
            graph.Connections.Add(new FlowConnection { FromNode = "continue_" + step, ToNode = "hit_" + (step + 1) });
            graph.Connections.Add(new FlowConnection { FromNode = "continue_" + step, FromPort = 1, ToNode = "finished_" + step });
        }
        Check(graph.Validate(out var validation), "combo probe: " + validation.ToDisplayText());
        var resource = new AbilityResource { AbilityId = "combo_framework_probe", Graph = graph,
            ActivationPolicy = new AbilityActivationPolicy { BlocksMovement = true, BlocksJump = true } };
        Check(_abilities.GrantAbility(resource), "grant in-memory combo probe");
        var runtime = _abilities.GetRuntime(resource.AbilityId);
        foreach (int hits in new[] { 1, 2, 3 })
        {
            Check(_abilities.TryActivateAbility(resource.AbilityId) == AbilityActivationResult.Activated, "start combo probe");
            ulong version = runtime.ActivationVersion;
            int enteredHits = 1;
            runtime.FlowRuntime.NodeEntered += (_, node) => { if (node.Id.StartsWith("hit_", StringComparison.Ordinal)) enteredHits++; };
            Check(!runtime.TryBufferInput("primary_attack"), "input outside a combo window is rejected");
            for (int step = 1; step < hits; step++)
            {
                for (int i = 0; i < 40 && !runtime.IsInputWindowOpen("primary_attack"); i++) await Frames(1);
                Check(runtime.TryBufferInput("primary_attack"), "Timeline opens the continuation window");
                Check(runtime.TryBufferInput("primary_attack"), "same-window repeated click remains one buffered input");
                for (int i = 0; i < 40 && enteredHits == step && runtime.IsRunning; i++) await Frames(1);
                Check(enteredHits == step + 1, "consume condition advances exactly one stage");
            }
            await WaitForAttack(runtime);
            Check(enteredHits == hits && runtime.LastReturnLabel == "Finished_" + hits,
                "combo probe exits after exactly " + hits + " stages");
            Check(runtime.ActivationVersion == version && !runtime.HasBufferedInput("primary_attack") &&
                !runtime.IsInputWindowOpen("primary_attack"), "combo completion clears input without restarting the runtime");
            Check(!_player.GetComponent<CharacterMovementComponent3D>().MovementLocked, "combo completion releases movement");
        }
        Check(_abilities.TryActivateAbility(resource.AbilityId) == AbilityActivationResult.Activated, "restart probe for cancellation");
        for (int i = 0; i < 40 && !runtime.IsInputWindowOpen("primary_attack"); i++) await Frames(1);
        Check(runtime.TryBufferInput("primary_attack"), "buffer exists before cancellation");
        Check(_abilities.CancelAbility(resource.AbilityId, "ComboCancellationCheck"), "cancel combo probe");
        Check(!runtime.HasBufferedInput("primary_attack") && !runtime.IsInputWindowOpen("primary_attack"),
            "cancellation immediately clears combo input");
        await Frames(2);
        Check(!_player.GetComponent<CharacterMovementComponent3D>().MovementLocked, "movement update observes the released lock");
        GD.Print("COMBO_FRAMEWORK_CHECK: PASS (in-memory graph, 1/2/3 stages, one input per window, cancellation)");
    }

    private async Task CheckAttackFacing()
    {
        var movement = _player.GetComponent<CharacterMovementComponent3D>();
        foreach (string stance in new[] { "high", "mid", "low" })
        foreach (int facing in new[] { 1, -1 })
        {
            _equipment.RequestStance(stance);
            await SettleStance();
            movement.RestoreFacing(facing);
            AbilityRuntime runtime = _abilities.GetRuntime(_equipment.CurrentPrimaryAttack.AbilityId);
            for (int i = 0; i < 180 && _abilities.CanActivateAbility(runtime.AbilityId) != AbilityActivationResult.Activated; i++)
                await Frames(1);
            Check(_equipment.TryPrimaryAttack(), "facing check starts " + stance);
            KeyInput(facing > 0 ? Key.A : Key.D, true);
            await Frames(30);
            KeyInput(facing > 0 ? Key.A : Key.D, false);
            Check(movement.Facing == facing, "attack direction remains locked through the strike");
            Check(!_player.HasNode("WeaponTrail"), "animation-only attack creates no weapon particle scene");
            if (facing < 0)
            {
                _abilities.CancelAbility(runtime.AbilityId, "FacingCancellationCheck");
                Check(!runtime.IsRunning, "attack cancellation stops the Timeline");
                await Frames(3);
                Check(!movement.MovementLocked, "cancellation releases movement");
            }
            else await WaitForAttack(runtime);
        }
        movement.RestoreFacing(1);
        _equipment.RequestStance("mid");
        await SettleStance();
        GD.Print("ATTACK_FACING_CHECK: PASS (all stances, both facings, cancellation, no weapon effects)");
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
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
