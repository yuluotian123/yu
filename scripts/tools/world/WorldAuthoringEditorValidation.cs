#if TOOLS
using System;
using System.Linq;
using GameLogic;
using Godot;

/// <summary>Bridge for the real-editor save/build regression, outside gameplay lifecycle.</summary>
[Tool]
public partial class WorldAuthoringEditorValidation : RefCounted
{
    const string Folder="res://.godot/world-validation/authoring/";
    public bool ValidateComponents(Node root)
    {
        foreach(var host in WorldModule.Hosts(root))foreach(var binding in host.Components.OfType<WorldObjectComponent3D>())
        {WorldCatalog.ValidateOverrides(binding);binding.ValidateComponents(host.Components,host);}
        return true;
    }
    public bool CompileFixture()
    {
        var type=ResourceLoader.Load<WorldObjectTypeDefinition>(Folder+"service_type.tres");
        var level=new WorldLevelDefinition{Id="authoring_service_level",ScenePath=Folder+"service_level.tscn"};
        var world=new WorldDefinition{Levels=new(){level},ObjectTypes=new(){type}};
        WorldAuthoringCompiler.CompileAll(world);return new WorldCatalog(world).Objects.Count==2;
    }
    public void SetExtraCapability(bool enabled)
    {
        var type=ResourceLoader.Load<WorldObjectTypeDefinition>(Folder+"service_type.tres");
        if(enabled)type.Capabilities.Add(new WorldLiquidCapability());
        else foreach(var definition in type.Capabilities.OfType<WorldLiquidCapability>().ToArray())type.Capabilities.Remove(definition);
        if(ResourceSaver.Save(type)!=Error.Ok)throw new InvalidOperationException("Cannot save service fixture type");
    }
    public bool BuildProject()
    {
        WorldAuthoringPlugin Find(Node root)
        {
            if(root is WorldAuthoringPlugin plugin)return plugin;
            foreach(Node child in root.GetChildren())if(Find(child) is {} found)return found;
            return null;
        }
        return Find(EditorInterface.Singleton.GetBaseControl().GetTree().Root)?._Build()==true;
    }
}
#endif
