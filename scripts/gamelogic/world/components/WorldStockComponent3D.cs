using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldStockComponent3D : WorldCapabilityComponent3D
{
    public override string CapabilityKey=>"stock";
    WorldStockState State=>Object?.State?.Capability<WorldStockState>(CapabilityKey);
    public double Amount {get=>State?.Amount??0;set{if(State!=null)State.Amount=value;}}
    public void Restock()=>Amount=Object.Definition<WorldStockCapability>().InitialAmount;
    public bool TryTake(double amount)
    {
        if(!IsActive||Object?.IsAvailable!=true||!double.IsFinite(amount)||amount<=0||State==null||State.Amount<amount)return false;
        State.Amount-=amount;return true;
    }
}
