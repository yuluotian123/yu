using System;
using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldInteractionCapability : WorldCapabilityDefinition
{
    public override string Key=>"interaction";
    public override Type ComponentType=>typeof(WorldInteractionComponent3D);
    public override string Adapter=>"point";
    [Export] public Godot.Collections.Array<WorldInteractionDefinition> Operations {get;set;}=new();
    public override void Validate()
    {
        var ids=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach(var operation in Operations)
        {
            WorldCatalog.Require(operation!=null,"交互操作不能为空。");operation.Validate();
            WorldCatalog.Require(ids.Add(operation.Id),"交互操作 Id 重复："+operation.Id);
        }
    }
    public override WorldCapabilityState CreateState(Action changed)=>new WorldEmptyCapabilityState();
}
