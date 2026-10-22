using System;
using System.Collections.Generic;
using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldConsumeLiquidInteraction : WorldInteractionDefinition
{
    public WorldConsumeLiquidInteraction(){Id="pour_liquid";DisplayName="倒出液体";}
    [Export] public double Amount {get;set;}=.25;
    public override IReadOnlyList<string> RequiredCapabilities {get;}=Array.AsReadOnly(new[]{"liquid"});
    public override void Validate(){base.Validate();WorldCatalog.Require(double.IsFinite(Amount)&&Amount>0,"液体用量必须为有限正数。");}
    internal override WorldInteractionResult Check(WorldInteractionContext context)=>context.Target.Owner.GetComponent<WorldLiquidComponent3D>() is {IsActive:true} liquid&&liquid.Volume>=Amount
        ?WorldInteractionResult.Ok():WorldInteractionResult.Fail("液体不足或液体能力已停用。");
    internal override WorldInteractionResult Apply(WorldInteractionContext context)
    {
        var liquid=context.Target.Owner.GetComponent<WorldLiquidComponent3D>();
        return liquid.TryConsume(Amount)?WorldInteractionResult.Ok($"已倒出液体 {Amount:0.##}，剩余 {liquid.Volume:0.##}。"):WorldInteractionResult.Fail("液体不足。");
    }
}
