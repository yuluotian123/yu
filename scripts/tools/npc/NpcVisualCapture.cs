using System;
using Framework;
using GameLogic;
using GameLogic.AI;
using Godot;

/// <summary>Opt-in GPU capture of the real resident inside SpaceLevel.</summary>
public partial class NpcVisualCapture : Node
{
    public override async void _Ready()
    {
        int exit = 0;
        Node3D level = null;
        try
        {
            ModuleSystem.RegisterModule<ISaveModule>(new SaveModule("res://.godot/npc/visual_saves", "res://.godot/npc/no_legacy"));
            level = GD.Load<PackedScene>("res://assets/scenes/spacelevel.tscn").Instantiate<Node3D>();
            AddChild(level);
            var npc = level.GetNode<GameObject3D>("Courier");
            npc.GetComponent<AiAgentComponent3D>().Running = false;
            level.GetNode<GameObject3D>("Resident").GetComponent<AiAgentComponent3D>().Running = false;
            level.GetNode<GameObject3D>("CameraRig").GetComponent<SceneCameraComponent3D>().IsActive = false;
            for (int i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 2.2f, Near = .05f, Far = 1000 };
            level.AddChild(camera);
            camera.GlobalPosition = npc.GlobalPosition + new Vector3(-1.4f, .7f, 0);
            camera.LookAt(npc.GlobalPosition + new Vector3(0, .12f, 0), Vector3.Up);
            camera.MakeCurrent();
            var animation = npc.GetComponent<CharacterAnimationComponent3D>();
            foreach (string clip in new[] { "idle", "walk" })
            {
                animation.RequestAnimation("VisualValidation", clip, 100, restartIfPlaying: false);
                for (int i = 0; i < 12; i++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    camera.GlobalPosition = npc.GlobalPosition + new Vector3(-1.4f, .7f, 0);
                    camera.LookAt(npc.GlobalPosition + new Vector3(0, .12f, 0), Vector3.Up);
                    camera.MakeCurrent();
                }
                GD.Print($"CAPTURE {clip}: npc={npc.GlobalPosition}, camera={camera.GlobalPosition}, sprite={animation.Sprite.GlobalPosition}, visible={animation.Sprite.Visible}");
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = GetViewport().GetTexture().GetImage();
                if (image == null || image.IsEmpty()) throw new InvalidOperationException("GPU capture is empty.");
                if (image.SavePng($"res://.godot/npc/art/spacelevel_{clip}.png") != Error.Ok)
                    throw new InvalidOperationException("GPU capture could not be written.");
            }
            animation.RequestAnimation("VisualValidation", "idle", 100, restartIfPlaying: false);
            var life=level.GetNode<GameObject3D>("RPGTown/TownContent/NpcLife");
            camera.Size=23;camera.GlobalPosition=life.GlobalPosition+new Vector3(0,18,20);
            camera.LookAt(life.GlobalPosition+new Vector3(0,0,2),Vector3.Up);
            for(int i=0;i<12;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using(var overview=GetViewport().GetTexture().GetImage())
                if(overview.SavePng("res://.godot/npc/art/spacelevel_population.png")!=Error.Ok)throw new Exception("Population capture failed.");
            GD.Print("NPC_VISUAL_CAPTURE_PASS actual SpaceLevel idle + walk + two authored NPCs");
        }
        catch (Exception error) { exit = 1; GD.PushError(error.ToString()); }
        level?.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ModuleSystem.Shutdown();
        GetTree().Quit(exit);
    }
    public override void _Process(double delta) => ModuleSystem.Process(delta, delta);
}
