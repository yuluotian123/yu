using System;
using System.Reflection;
using System.Text.Json.Nodes;
using GameLogic;
using Godot;

/// <summary>Checks that late world checkpoints retain the last valid player world transform.</summary>
public partial class WorldExitValidation : Node
{
    // Keep test setup and checkpoint inspection out of the production API.
    static void Bind(WorldModule world, GameObject3D player) =>
        typeof(WorldModule).GetProperty(nameof(WorldModule.Player))!.SetValue(world, player);

    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    static void CheckTransform(JsonNode node, Transform3D expected)
    {
        Check(node is JsonArray { Count: 12 }, "Checkpoint lost the player transform");
        var values = node.AsArray();
        Vector3 Vector(int offset) => new(values[offset].GetValue<float>(), values[offset + 1].GetValue<float>(), values[offset + 2].GetValue<float>());
        var actual = new Transform3D(new Basis(Vector(0), Vector(3), Vector(6)), Vector(9));
        Check(actual.IsEqualApprox(expected), $"Checkpoint transform changed: {actual} != {expected}");
    }

    static void CheckCheckpoint(SaveModule save, Transform3D expected)
    {
        save.CheckpointSections();
        var sections = (JsonObject)typeof(SaveModule).GetField("_pendingSections", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(save)!;
        CheckTransform(sections["world"]!["session"]!["player_transform"], expected);
    }

    void Validate()
    {
        var world = new WorldModule();
        var save = new SaveModule("res://.godot/world-exit-validation", "res://.godot/world-exit-validation");
        save.RegisterSection(world);
        var parent = new Node3D { Position = new(31, 7, -19), Rotation = new(0, .6f, 0) };
        AddChild(parent);
        var player = new GameObject3D { Name = "ExitPlayer", PersistentId = "exit_validation", Position = new(3, 2, -4), Rotation = new(.1f, -.4f, 0) };
        GameObject3D replacement = null;
        try
        {
            Bind(world, player); parent.AddChild(player);
            CheckCheckpoint(save, player.GlobalTransform);
            // Move after the previous checkpoint: an old saved value must not win at exit.
            player.Position += new Vector3(5, 1, 2); player.RotateY(.3f);
            var final = player.GlobalTransform;
            parent.RemoveChild(player);
            Check(!player.IsInsideTree(), "Player did not leave the tree");
            player.Transform = Transform3D.Identity;
            CheckCheckpoint(save, final);
            GD.Print("WORLD_EXIT_DETACHED_CHECKPOINT_PASS");

            parent.AddChild(player); player.Position = new(-8, 3, 6);
            final = player.GlobalTransform;
            player.Free();
            CheckCheckpoint(save, final);
            GD.Print("WORLD_EXIT_FREED_CHECKPOINT_PASS");

            replacement = new GameObject3D { Name = "ReplacementPlayer", PersistentId = "exit_validation_new" };
            Bind(world, replacement);
            Check(world.Capture()["player_transform"] == null, "Replacement inherited the previous player's transform");
            parent.AddChild(replacement); replacement.Position = new(9, 1, 3);
            CheckCheckpoint(save, replacement.GlobalTransform);
            Bind(world, null);
            parent.RemoveChild(replacement);
            Check(world.Capture()["player_transform"] == null, "Unbound player exit restored a stale transform");
            GD.Print("WORLD_EXIT_REBIND_RESET_PASS");
        }
        finally
        {
            Bind(world, null); save.UnregisterSection(world); save.Shutdown();
            if (GodotObject.IsInstanceValid(player)) player.Free();
            if (GodotObject.IsInstanceValid(replacement)) replacement.Free();
            parent.Free();
        }
    }

    public override void _Ready()
    {
        try { Validate(); GD.Print("WORLD_EXIT_VALIDATION_PASS"); GetTree().Quit(); }
        catch (Exception error) { GD.PushError("WORLD_EXIT_VALIDATION_FAILED " + error); GetTree().Quit(1); }
    }
}
