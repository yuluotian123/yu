#if TOOLS
using System.Linq;
using Godot;
using GameLogic;

/// <summary>Offline registration uses the maintained compiler for identities and portal routes.</summary>
[Tool]
public partial class ForestInteriorAuthoring : RefCounted
{
    public void CompileWorld()
    {
        var world=ResourceLoader.Load<WorldDefinition>(WorldDefinition.CommonPath);
        string[] names={"01_chief","03_smith","04_carpenter","05_herbalist","06_forester","07_gatherer","08_farmer_east","09_farmer_west","10_smallholder"};
        foreach(string name in names)
        {
            var level=ResourceLoader.Load<WorldLevelDefinition>($"res://assets/resources/world/levels/forest_house_{name}.tres");
            if(!world.Levels.Any(item=>item.Id==level.Id))world.Levels.Add(level);
            if(!world.Locations.Any(item=>item.Id==level.Location.Id))world.Locations.Add(level.Location);
        }
        WorldAuthoringCompiler.CompileAll(world);
        if(ResourceSaver.Save(world,WorldDefinition.CommonPath)!=Error.Ok)throw new System.InvalidOperationException("World registration save failed");
        GD.Print($"FOREST_INTERIORS_WORLD: {world.Levels.Count} levels; {world.Routes.Count} routes; legacy storage identity retained");
    }
}
#endif
