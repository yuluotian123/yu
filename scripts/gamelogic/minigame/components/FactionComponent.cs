using Framework;
using GameLogic;
using Godot;


public enum FactionTag
{
    player,
    Ally,
    Enemy,
    Neutral
}

[GlobalClass]
public partial class FactionComponent : Component2D
{

    [Export] public FactionTag FactionType { get; set; } = FactionTag.player;

    public override int Priority => ComponentPriority.Default;

    public bool IsPlayerFaction => FactionType == FactionTag.player;

}
