using System;
using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldStockCapability : WorldCapabilityDefinition
{
    public override string Key=>"stock";
    public override Type ComponentType=>typeof(WorldStockComponent3D);
    [Export] public double InitialAmount {get;set;}
    public override void Validate()=>WorldCatalog.Require(double.IsFinite(InitialAmount)&&InitialAmount>=0,"初始库存必须为有限非负数。");
    public override WorldCapabilityState CreateState(Action changed)=>new WorldStockState(InitialAmount,changed);
}
