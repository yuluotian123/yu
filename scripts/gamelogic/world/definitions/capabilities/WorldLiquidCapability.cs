using System;
using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldLiquidCapability : WorldCapabilityDefinition
{
    public override string Key=>"liquid";
    public override Type ComponentType=>typeof(WorldLiquidComponent3D);
    [Export] public double VolumeLimit {get;set;}=1;
    [Export] public string InitialLiquid {get;set;}="water";
    [Export] public double InitialVolume {get;set;}
    public override void Validate()=>WorldCatalog.Require(double.IsFinite(VolumeLimit)&&VolumeLimit>0&&double.IsFinite(InitialVolume)&&InitialVolume>=0&&InitialVolume<=VolumeLimit&&(!string.IsNullOrWhiteSpace(InitialLiquid)||InitialVolume==0),"液体容量或初始内容无效。");
    public override WorldCapabilityState CreateState(Action changed)=>new WorldLiquidState(VolumeLimit,InitialLiquid,InitialVolume,changed);
}
