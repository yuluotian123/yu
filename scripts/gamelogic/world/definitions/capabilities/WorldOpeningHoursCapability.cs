using System;
using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldOpeningHoursCapability : WorldCapabilityDefinition
{
    public override string Key=>"opening_hours";
    public override Type ComponentType=>typeof(WorldOpeningHoursComponent3D);
    [Export] public double OpenHour {get;set;}=8;
    [Export] public double CloseHour {get;set;}=20;
    public override void Validate()=>WorldCatalog.Require(double.IsFinite(OpenHour)&&double.IsFinite(CloseHour)&&OpenHour>=0&&OpenHour<=24&&CloseHour>=0&&CloseHour<=24,"开放时间必须在 0 至 24 小时内。");
    public bool Includes(double hour)=>OpenHour==CloseHour||(OpenHour<CloseHour?hour>=OpenHour&&hour<CloseHour:hour>=OpenHour||hour<CloseHour);
    public override WorldCapabilityState CreateState(Action changed)=>new WorldOpeningHoursState(changed);
}
