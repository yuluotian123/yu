using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using GameLogic;
using Godot;

// Runs the real Player EC components; no gameplay behavior is replaced by this fixture.
public partial class CharacterVisualSmoke : Node3D
{
    private readonly List<string> _errors = new();
    private int _checks;
    private const string Output = "res://assets/resources/sprites/amber_fox_style_v2/full_256/runtime-validation.json";
    private static readonly string[] States =
        { "idle", "run", "attack", "jumpup", "inair", "isfalling", "land", "jump", "dash", "hurt", "death" };

    public override async void _Ready()
    {
        try { await Run(); }
        catch (Exception error) { _errors.Add(error.ToString()); }
        var report = new { checks = _checks, errors = _errors, scene = "res://assets/scenes/player.tscn",
            coverage = "real Player initialization, both weapon profiles, 22 animation requests, input switch, attack timing and queued switch, foot anchor" };
        string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        using var file = FileAccess.Open(Output, FileAccess.ModeFlags.Write);
        file.StoreString(json);
        GD.Print("CHARACTER_VISUAL_SMOKE ", json);
        // Release temporary managed wrappers before the native engine shuts down.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GetTree().Quit(_errors.Count == 0 ? 0 : 1);
    }

    private async Task Run()
    {
        var floor = new StaticBody3D { Position = new Vector3(0, -0.568f, 0) };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(30, 0.2f, 30) } });
        AddChild(floor);
        var player = GD.Load<PackedScene>("res://assets/scenes/player.tscn").Instantiate<GameObject3D>();
        AddChild(player);
        await Frames(12);
        var equipment = player.GetComponent<WeaponEquipmentComponent3D>();
        var animation = player.GetComponent<CharacterAnimationComponent3D>();
        var movement = player.GetComponent<CharacterMovementComponent3D>();
        Check(equipment != null && animation?.Sprite != null, "Player EC initialization");
        Check(equipment.CurrentWeaponId == "greatsword", "default greatsword");
        Check(movement.IsOnFloor, "native floor contact");
        foreach (string weapon in new[] { "greatsword", "katana" })
        {
            string shortName = weapon == "greatsword" ? "gs" : "kt";
            Check(equipment.CurrentWeaponId == weapon, "equipped " + weapon);
            var profile = equipment.CurrentWeapon.AnimationProfile;
            Check(profile.IsValid(), "valid profile " + weapon);
            Check(profile.FrameSize == new Vector2I(256, 256) && profile.FootAnchor == new Vector2I(128, 224), "canvas/anchor " + weapon);
            Check(animation.Sprite.SpriteFrames.ResourcePath.EndsWith($"full_256/{shortName}/sprite_frames.tres"), "new frames " + weapon);
            if (weapon == "greatsword")
            {
                CheckRunHeight(profile.Frames);
                CheckAttackIdleEndpoints(profile.Frames);
            }
            foreach (string state in States)
            {
                animation.RequestAnimation("VisualSmoke", state, 9999);
                await Frames(2);
                Check(animation.Sprite.Animation == state && animation.ActiveAnimation == state, weapon + "/" + state);
                CheckGeometry(animation.Sprite, movement.HalfHeight, weapon, state);
            }
            foreach (string state in new[] { "idle", "run", "idle" })
            {
                animation.RequestAnimation("VisualSmoke", state, 9999);
                await Frames(2);
                CheckGeometry(animation.Sprite, movement.HalfHeight, weapon, state);
            }
            animation.ClearAnimationRequest("VisualSmoke");
            await Frames(2);
            Check(equipment.TryPrimaryAttack(), "activate attack " + weapon);
            await Frames(2);
            Check(animation.Sprite.Animation == "attack", "timeline attack playback " + weapon);
            float expected = weapon == "greatsword" ? 1.2f : .36f;
            SpriteFrames frames = animation.Sprite.SpriteFrames;
            Check(Math.Abs(frames.GetFrameCount("attack") / frames.GetAnimationSpeed("attack") - expected) < .0001, "attack duration " + weapon);
            if (weapon == "greatsword")
            {
                Check(equipment.RequestNextWeapon() && equipment.PendingSlot >= 0 && equipment.CurrentWeaponId == "greatsword", "queue switch during attack");
                await ToSignal(GetTree().CreateTimer(.8), SceneTreeTimer.SignalName.Timeout);
                Check(equipment.CurrentWeaponId == "greatsword" && equipment.PendingSlot >= 0 && animation.Sprite.Animation == "attack", "extended attack remains active after old cutoff");
                await ToSignal(GetTree().CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
                Check(equipment.CurrentWeaponId == "katana" && equipment.PendingSlot == -1, "apply queued switch");
            }
            else
            {
                await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            }
        }
        Input.ActionPress("player_switch_weapon");
        await Frames(6);
        Input.ActionRelease("player_switch_weapon");
        await Frames(3);
        Check(equipment.CurrentWeaponId == "greatsword", "input action switches back to greatsword");
        Check(animation.Sprite.SpriteFrames.ResourcePath.EndsWith("full_256/gs/sprite_frames.tres"), "switch refreshes visual");
        CheckGeometry(animation.Sprite, movement.HalfHeight, "greatsword", "idle");
        player.QueueFree();
        await Frames(2);
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private void Check(bool passed, string label)
    {
        _checks++;
        if (!passed) _errors.Add(label);
    }

    private void CheckGeometry(AnimatedSprite3D sprite, float halfHeight, string weapon, string state)
    {
        bool approvedDensity = weapon == "greatsword" && (state == "idle" || state == "run" || state == "attack");
        float expectedSize = approvedDensity ? .003214285714f : .00402234637f;
        int footY = approvedDensity ? 240 : 224;
        int referenceHeight = approvedDensity ? 224 : 179;
        Check(Mathf.Abs(sprite.PixelSize - expectedSize) < .00000001f, "pixel size " + weapon + "/" + state);
        float foot = sprite.Position.Y - (footY - 128) * sprite.PixelSize * sprite.Scale.Y;
        Check(Mathf.Abs(foot + halfHeight) < .00001f, "foot matches collision " + weapon + "/" + state);
        Check(Mathf.Abs(referenceHeight * sprite.PixelSize * sprite.Scale.Y - .936f) < .00001f, "world height " + weapon + "/" + state);
    }

    private void CheckAttackIdleEndpoints(SpriteFrames frames)
    {
        using Image idle = frames.GetFrameTexture("idle", 0).GetImage();
        foreach (int index in new[] { 0, frames.GetFrameCount("attack") - 1 })
        {
            using Image attack = frames.GetFrameTexture("attack", index).GetImage();
            bool equal = attack.GetSize() == idle.GetSize();
            for (int y = 0; equal && y < idle.GetHeight(); y++)
            for (int x = 0; equal && x < idle.GetWidth(); x++)
            {
                Color a = attack.GetPixel(x, y), b = idle.GetPixel(x, y);
                equal = a.A == b.A && (b.A == 0f || a == b);
            }
            Check(equal, "attack endpoint equals approved idle " + index);
        }
    }

    private void CheckRunHeight(SpriteFrames frames)
    {
        // Equal PixelSize alone missed the previous run's 28-41px head drop.
        int idleTop = EarTop(frames.GetFrameTexture("idle", 0));
        Check(idleTop == 16, "approved idle ear height");
        int sum = 0;
        int count = frames.GetFrameCount("run");
        for (int i = 0; i < count; i++)
        {
            int top = EarTop(frames.GetFrameTexture("run", i));
            Check(top < 256 && top <= idleTop + 20, "run head drop frame " + i);
            sum += top;
        }
        Check(count > 0 && (float)sum / count <= idleTop + 14, "run mean head drop");
    }

    private static int EarTop(Texture2D texture)
    {
        using Image image = texture.GetImage();
        // Ear-tip brown inside the head window; excludes the raised steel blade.
        for (int y = 0; y < 90; y++)
        for (int x = 98; x < 171; x++)
        {
            Color color = image.GetPixel(x, y);
            if (color.A > .5f && color.R > 45f / 255 && color.R < 180f / 255 &&
                color.G < 140f / 255 && color.B < 140f / 255) return y;
        }
        return 256;
    }
}
