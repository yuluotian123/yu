using Framework;
using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldUsageCapacityComponent3D : WorldCapabilityComponent3D
{
    public override string CapabilityKey=>"usage_capacity";
    WorldUsageCapacityState State=>Object?.State?.Capability<WorldUsageCapacityState>(CapabilityKey);
    double Now=>((WorldModule)ModuleSystem.GetModule<IWorldModule>()).RuntimeSeconds;
    public int Slots=>State?.Slots??0;
    public int Reservations=>State?.Reservations(Now)??0;
    public bool Available(string actor)=>IsActive&&Object?.CanUse(actor)==true&&State.Available(actor,Now);
    public bool Reserve(string actor)=>IsActive&&Object?.CanUse(actor)==true&&State.Reserve(actor,Now);
    public bool Reserved(string actor)=>State?.Reserved(actor,Now)==true;
    public void Release(string actor)=>State?.Release(actor);
}
