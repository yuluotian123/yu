using System;
using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldUsageCapacityCapability : WorldCapabilityDefinition
{
    public override string Key=>"usage_capacity";
    public override Type ComponentType=>typeof(WorldUsageCapacityComponent3D);
    [Export] public int Slots {get;set;}=1;
    public override void Validate()=>WorldCatalog.Require(Slots>0,"使用容量必须大于零。");
    public override WorldCapabilityState CreateState(Action changed)=>new WorldUsageCapacityState(Slots);
}
