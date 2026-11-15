using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GameLogic;
using Godot;

// Real player, HFSM, input and collision checks for the authored jump.
public partial class ValidateProtagonistJump : Node3D
{
    private const string Output = "res://.godot/art-validation/greatsword_jump";
    private GameObject3D _player;
    private CharacterMovementComponent3D _movement;
    private CharacterAnimationComponent3D _animation;

    public override async void _Ready()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Output));
            var floor = new StaticBody3D { Position = new Vector3(0, -.1f, 0) };
            floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(40, .2f, 40) } });
            AddChild(floor);
            if (OS.GetCmdlineUserArgs().Contains("--capture"))
            {
                await Capture();
                GD.Print("JUMP_CAPTURE: PASS");
                GetTree().Quit();
                return;
            }

            _player = CreatePlayer("JumpPlayer", Vector3.Zero, false);
            _movement = _player.GetComponent<CharacterMovementComponent3D>();
            _animation = _player.GetComponent<CharacterAnimationComponent3D>();
            await Frames(30);
            Check(_player.InitializationError == string.Empty && _movement.IsOnFloor, "player initializes on the floor");
            var full = await JumpWithInput(60, false);
            Check(full.PreparingFrames >= 3 && full.PreparingFrames <= 8, "brief grounded anticipation is visible");
            Check(full.PreparationGrounded, "crouch finishes before the physics impulse");
            Check(full.Animations.Contains("jump_prepare_mid") && full.Animations.Contains("jumpup_mid") &&
                full.Animations.Contains("inair_mid") && full.Animations.Contains("isfalling_mid") && full.Animations.Contains("land_mid"),
                "all jump phases play through the real HFSM");
            Check(full.Height > .4f && full.Height < .65f, "full jump retains the existing height");
            Check(_movement.IsOnFloor && _animation.ActiveAnimation == "idle_mid", "full landing returns to idle");

            var shortTap = await JumpWithInput(1, false);
            Check(shortTap.Height > .04f && shortTap.Height < full.Height * .5f, "release during preparation produces a short hop");
            var moving = await JumpWithInput(60, true);
            Check(moving.HorizontalDistance > .5f && moving.Height > .4f, "jump retains horizontal control");

            var equipment = _player.GetComponent<WeaponEquipmentComponent3D>();
            var guards = new Dictionary<string, JumpResult>();
            foreach (string stance in new[] { "high", "low" })
            {
                equipment.RequestStance(stance);
                await Frames(24);
                var jump = await JumpWithInput(60, false);
                foreach (string phase in new[] { "jump_prepare", "jumpup", "inair", "isfalling", "land" })
                    Check(jump.Animations.Contains(phase+"_"+stance), stance+" keeps its guard in "+phase);
                Check(_animation.ActiveAnimation == "idle_"+stance, stance+" returns to its own idle");
                Check(Math.Abs(jump.Height-full.Height)<.002f, "stances share jump physics");
                guards[stance]=jump;
            }
            equipment.RequestStance("mid");
            await Frames(24);

            var walkJumps = new Dictionary<string, JumpResult>();
            foreach (string stance in new[] { "high", "mid", "low" })
            {
                equipment.RequestStance(stance);
                await Frames(24);
                walkJumps[stance] = await JumpFromWalk(stance);
            }
            equipment.RequestStance("mid");
            await Frames(24);

            // A stance request during anticipation is queued through takeoff.
            KeyInput(Key.Space, true);
            await Frames(2);
            Check(_movement.IsJumpPreparing, "stance probe starts during preparation");
            equipment.RequestStance("high");
            Check(!equipment.IsTransitioning && equipment.PendingStance == "high", "preparation queues stance changes");
            await Frames(10);
            Check(!_movement.IsOnFloor && equipment.CurrentStance == "mid", "queued stance cannot cancel takeoff");
            KeyInput(Key.Space, false);
            await Frames(80);
            Check(equipment.CurrentStance == "high" && !equipment.IsTransitioning, "queued stance completes after landing");

            // A real control lock cancels a prepared jump without a delayed impulse.
            float groundY = _player.Position.Y;
            KeyInput(Key.Space, true);
            await Frames(2);
            Check(_movement.IsJumpPreparing, "cancel probe starts during preparation");
            _movement.SetControlLock("jump-check", false, true);
            await Frames(12);
            Check(!_movement.IsJumpPreparing && _movement.IsOnFloor && Math.Abs(_player.Position.Y-groundY)<.001f,
                "jump lock cancels anticipation");
            KeyInput(Key.Space, false);
            _movement.ClearControlLock("jump-check");
            await Frames(20);
            Check(_movement.IsOnFloor, "unlock cannot launch a stale jump request");

            Write("gameplay.json", new { full, shortTap, moving, guards, walkJumps, queuedStance = true, cancellation = true });
            GD.Print("JUMP_GAMEPLAY: PASS");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private sealed class JumpResult
    {
        public float Height { get; set; }
        public float HorizontalDistance { get; set; }
        public int PreparingFrames { get; set; }
        public bool PreparationGrounded { get; set; } = true;
        public HashSet<string> Animations { get; } = new();
    }

    private async Task<JumpResult> JumpWithInput(int holdFrames, bool move)
    {
        var result = new JumpResult();
        Vector3 start = _player.Position;
        KeyInput(Key.Space, true);
        if (move) KeyInput(Key.D, true);
        for (int i=0;i<90;i++)
        {
            if (i==holdFrames) KeyInput(Key.Space, false);
            if (i==40 && move) KeyInput(Key.D, false);
            await Frames(1);
            result.Height = Math.Max(result.Height, _player.Position.Y-start.Y);
            result.HorizontalDistance = Math.Max(result.HorizontalDistance, Math.Abs(_player.Position.X-start.X));
            result.Animations.Add(_animation.ActiveAnimation);
            if (_movement.IsJumpPreparing)
            {
                result.PreparingFrames++;
                result.PreparationGrounded &= _movement.IsOnFloor && Math.Abs(_player.Position.Y-start.Y)<.001f;
            }
        }
        KeyInput(Key.Space, false);
        return result;
    }

    private async Task<JumpResult> JumpFromWalk(string stance)
    {
        KeyInput(Key.D, true);
        await Frames(30);
        Check(_animation.ActiveAnimation == "run_"+stance, "walking cycle precedes jump");
        var result = new JumpResult();
        Vector3 start = _player.Position;
        bool landed = false;
        bool resumedWalking = false;
        KeyInput(Key.Space, true);
        for (int i=0;i<80;i++)
        {
            if (i==60) KeyInput(Key.Space, false);
            await Frames(1);
            string clip = _animation.ActiveAnimation;
            result.Animations.Add(clip);
            result.Height = Math.Max(result.Height, _player.Position.Y-start.Y);
            result.HorizontalDistance = Math.Max(result.HorizontalDistance, _player.Position.X-start.X);
            if (_movement.IsJumpPreparing)
            {
                result.PreparingFrames++;
                result.PreparationGrounded &= _movement.IsOnFloor && Math.Abs(_player.Position.Y-start.Y)<.001f;
            }
            if (clip == "land_"+stance) landed = true;
            if (!landed) continue;
            Check(!clip.StartsWith("idle") && !clip.StartsWith("run_start"), "landing cannot return through standing poses while moving");
            if (clip == "run_"+stance) resumedWalking = true;
        }
        Check(landed && resumedWalking && result.HorizontalDistance > 1.5f, "walking jump lands directly into the walking cycle");
        KeyInput(Key.Space, false);
        KeyInput(Key.D, false);
        await Frames(24);
        return result;
    }

    private async Task Capture()
    {
        AddChild(new WorldEnvironment { Environment = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(.085f,.095f,.12f),
            TonemapMode = Godot.Environment.ToneMapper.Linear } });
        var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 2.8f,
            Position = new Vector3(0,1.05f,7), Current = true };
        AddChild(camera);
        var groundMaterial = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(.72f,.74f,.77f) };
        for (int x=-15;x<=15;x++)
            AddChild(new MeshInstance3D { Position = new Vector3(x,.005f,0),
                Mesh = new BoxMesh { Size = new Vector3(.012f,.025f,.04f), Material = groundMaterial } });
        var actors = new[] {
            CreatePlayer("High",new Vector3(-1.65f,0,0),true),
            CreatePlayer("Mid",Vector3.Zero,true),
            CreatePlayer("Low",new Vector3(1.65f,0,0),true)
        };
        var labels = new CanvasLayer(); AddChild(labels);
        string[] names = { "high", "mid", "low" };
        for (int i=0;i<actors.Length;i++)
        {
            var sprite = actors[i].GetComponent<CharacterAnimationComponent3D>().Sprite;
            sprite.Shaded=false; sprite.Modulate=Colors.White;
            var label = new Label { Text=names[i].ToUpperInvariant(), Position=new Vector2(210+i*450,35) };
            label.AddThemeFontSizeOverride("font_size",24);
            label.AddThemeColorOverride("font_color",new Color(.28f,.3f,.35f));
            labels.AddChild(label);
        }
        await Frames(30);
        for(int i=0;i<actors.Length;i++)
            actors[i].GetComponent<WeaponEquipmentComponent3D>().RequestStance(names[i]);
        await Frames(24);
        string folder=Output+"/runtime";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        var trace=new List<object>();
        for(int frame=0;frame<180;frame++)
        {
            for(int i=0;i<actors.Length;i++)
            {
                var movement=actors[i].GetComponent<CharacterMovementComponent3D>();
                int cycleFrame=frame%90;
                float direction=frame<90?1:-1;
                movement.SubmitCommand(new CharacterCommand3D(direction,cycleFrame==24,cycleFrame>=24 && cycleFrame<80));
            }
            await Frames(1);
            camera.Position=new Vector3(actors[1].Position.X,1.05f,7);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            var rows=actors.Select(actor=> {
                var m=actor.GetComponent<CharacterMovementComponent3D>();
                var a=actor.GetComponent<CharacterAnimationComponent3D>();
                return new { animation=a.ActiveAnimation, frame=a.Sprite.Frame, progress=a.Sprite.FrameProgress,
                    position=new[]{actor.Position.X,actor.Position.Y,actor.Position.Z}, velocity=m.VelocityY,
                    grounded=m.IsOnFloor, preparing=m.IsJumpPreparing, facing=m.Facing };
            }).ToArray();
            trace.Add(new { frame, actors=rows });
            using var image=GetViewport().GetTexture().GetImage();
            image.SavePng(folder+$"/frame-{frame:000}.png");
        }
        Write("runtime/frames.json",trace);
    }

    private GameObject3D CreatePlayer(string name,Vector3 position,bool blockInput)
    {
        var actor=GD.Load<PackedScene>("res://assets/scenes/player.tscn").Instantiate<GameObject3D>();
        actor.Name=name;actor.Position=position+new Vector3(0,.5f,0);
        actor.Components=new Godot.Collections.Array<Component3D>(actor.Components.Where(component=>
            component is not CharacterPersistenceComponent3D and not PlayerWorldInteractionComponent3D and not CharacterDataComponent3D));
        actor.GetNodeOrNull("WorldInteractionHUD")?.Free();
        AddChild(actor);
        actor.GetComponent<PlayerCharacterInputComponent3D>().GameplayBlocked=blockInput;
        return actor;
    }

    private async Task Frames(int count)
    {
        for(int i=0;i<count;i++) await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
    }
    private static void Write(string name,object data) => System.IO.File.WriteAllText(
        ProjectSettings.GlobalizePath(Output+"/"+name),JsonSerializer.Serialize(data,new JsonSerializerOptions { WriteIndented=true }));
    private static void KeyInput(Key key,bool pressed) => Input.ParseInputEvent(new InputEventKey {
        PhysicalKeycode=key,Keycode=key,Pressed=pressed });
    private static void Check(bool value,string message)
    {
        if(!value) throw new InvalidOperationException(message);
    }
}
