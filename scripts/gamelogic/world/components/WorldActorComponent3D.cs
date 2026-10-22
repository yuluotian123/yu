using Framework;
using GameLogic.AI;
using Godot;
namespace GameLogic;

/// <summary>Opt a reusable character scene into the unique world roster.</summary>
[Tool,GlobalClass]
public partial class WorldActorComponent3D : Component3D
{
    public override int Priority=>1100;
    [Export(PropertyHint.File,"*.tscn")] public string ScenePathOverride {get;set;}="";
    [Export] public float DetailedDistance {get;set;}=30;
    [Export] public float AbstractDistance {get;set;}=45;
    [Export] public double MinimumResidencySeconds {get;set;}=2;
    internal bool Proxy;
    internal WorldActorRuntime Runtime;
    public override void OnInit()
    {
        Proxy=Owner.GetMeta("_world_proxy",false).AsBool();
        if(Owner.GetComponent<CharacterDataComponent3D>() is {} data)data.Persist=false;
        if(Owner.GetComponent<AiAgentComponent3D>() is {} ai)ai.Persist=false;
        if(Owner.GetComponent<CharacterPersistenceComponent3D>() is {} persistence)persistence.Persist=false;
        ((WorldModule)ModuleSystem.GetModule<IWorldModule>()).Population?.Bind(this);
    }
    public override void OnDestroy()=>((WorldModule)ModuleSystem.GetModule<IWorldModule>()).Population?.Unbind(this);
}
