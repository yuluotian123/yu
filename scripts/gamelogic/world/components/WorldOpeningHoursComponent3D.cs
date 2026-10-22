using Framework;
using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldOpeningHoursComponent3D : WorldCapabilityComponent3D
{
    public override string CapabilityKey=>"opening_hours";
    WorldOpeningHoursState State=>Object?.State?.Capability<WorldOpeningHoursState>(CapabilityKey);
    public bool? ManualOpen=>State?.ManualOpen;
    public bool AllowsUse=>IsActive&&(State?.ManualOpen??(Object?.Definition<WorldOpeningHoursCapability>()?.Includes(ModuleSystem.GetModule<ITimeOfDayModule>().Clock.TotalHours%24)??false));
    public void SetOpen(bool open)=>State.ManualOpen=open;
    public void ClearOverride()=>State.ManualOpen=null;
}
