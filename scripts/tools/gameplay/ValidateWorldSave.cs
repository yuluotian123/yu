using System;
using System.Threading.Tasks;
using GameLogic;
using Godot;

// Saves only under .godot; never overwrites an authored scene or world catalog.
public partial class ValidateWorldSave : Node
{
    public override async void _Ready()
    {
        try
        {
            const string folder = "res://.godot/crash-save/stress";
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
            var source = GD.Load<PackedScene>("res://assets/scenes/levels/forest_town_level.tscn");
            var original = GD.Load<WorldLevelDefinition>("res://assets/resources/world/levels/forest_town_level.tres");
            var level = new WorldLevelDefinition { Id = "save_stress", DisplayName = "Save stress", ScenePath = folder + "/forest.tscn", Location = original.Location };
            WorldLevelCatalog held = null;
            string previous = "";
            for (int i = 0; i < 12; i++)
            {
                Node root = source.Instantiate(PackedScene.GenEditState.Instance);
                try
                {
                    root.SetMeta("save_stress_iteration", i);
                    var packed = new PackedScene();
                    if (packed.Pack(root) != Error.Ok || ResourceSaver.Save(packed, level.ScenePath) != Error.Ok)
                        throw new InvalidOperationException("Scene snapshot save failed.");
                }
                finally { WorldAuthoringCompiler.Forget(root); root.Free(); }
                WorldAuthoringCompiler.Compile(level);
                var cached = ResourceLoader.Load<WorldLevelCatalog>(level.CatalogPath);
                held ??= cached;
                if (!ReferenceEquals(held, cached) || held.SourceSignature == previous || held.Version != 1)
                    throw new InvalidOperationException("Save must update the held catalog without replacing its C# instance.");
                previous = held.SourceSignature;
                // The no-change path must also leave the editor-held resource intact.
                WorldAuthoringCompiler.Compile(level);
                if (!ReferenceEquals(held, ResourceLoader.Load<WorldLevelCatalog>(level.CatalogPath)))
                    throw new InvalidOperationException("No-change compile replaced the catalog.");
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                GD.Print($"WORLD_SAVE: cycle={i + 1}, entries={held.Entries.Count}, actors={held.Actors.Count}");
            }
            GD.Print("WORLD_SAVE: PASS (12 scene saves, 24 compiles, forced GC, stable cached instance)");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("WORLD_SAVE: FAIL " + error); GetTree().Quit(1); }
    }
}
