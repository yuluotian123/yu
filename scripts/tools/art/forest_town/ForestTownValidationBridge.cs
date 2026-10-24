using Framework;
using GameLogic;
using Godot;

/// <summary>World lifecycle access for the GDScript scene validator.</summary>
public partial class ForestTownValidationBridge : RefCounted
{
    public void Prepare()
    {
#if TOOLS
        WorldAuthoringCompiler.CompileAll();
#endif
        ModuleSystem.RegisterModule<ISaveModule>(new SaveModule("res://.godot/forest-town/saves","res://.godot/forest-town/no-legacy"));
    }
    public void CloseWorld()=>ModuleSystem.GetModule<IWorldModule>().Close();
    public void Shutdown()=>ModuleSystem.Shutdown();
}
