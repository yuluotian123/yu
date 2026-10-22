using Godot;
namespace GameLogic;

/// <summary>Liquid contents do not imply interaction slots, stock or opening hours.</summary>
[Tool, GlobalClass]
public partial class WorldLiquidComponent3D : WorldCapabilityComponent3D
{
    public override string CapabilityKey=>"liquid";
    WorldLiquidState State=>Object?.State?.Capability<WorldLiquidState>(CapabilityKey);
    public string Liquid=>State?.Liquid??"";
    public double Volume=>State?.Volume??0;
    public double VolumeLimit=>State?.VolumeLimit??0;
    public bool TryFill(string liquid,double amount)=>IsActive&&Object?.IsAvailable==true&&State.TryFill(liquid,amount);
    public bool TryConsume(double amount)=>IsActive&&Object?.IsAvailable==true&&State.TryConsume(amount);
}
