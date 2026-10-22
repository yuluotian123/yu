using System;
using System.Collections.Generic;
using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldTakeStockInteraction : WorldInteractionDefinition
{
    public WorldTakeStockInteraction(){Id="take_stock";DisplayName="使用物资";}
    [Export] public double Amount {get;set;}=1;
    public override IReadOnlyList<string> RequiredCapabilities {get;}=Array.AsReadOnly(new[]{"stock"});
    public override string ConsumedProperty=>"stock";
    public override bool SupportsSemantic=>true;
    public override void Validate(){base.Validate();WorldCatalog.Require(double.IsFinite(Amount)&&Amount>0,"取用数量必须为有限正数。");}
    internal override WorldInteractionResult Check(WorldInteractionContext context)=>(context.Target==null||context.Target.Owner.GetComponent<WorldStockComponent3D>()?.IsActive==true)&&context.State?.Capability<WorldStockState>("stock") is {} stock&&stock.Amount>=(context.Amount??Amount)
        ?WorldInteractionResult.Ok():WorldInteractionResult.Fail("库存不足或库存能力已停用。");
    internal override WorldInteractionResult Apply(WorldInteractionContext context)
    {
        var stock=context.State.Capability<WorldStockState>("stock");
        double amount=context.Amount??Amount;
        if(stock==null||stock.Amount<amount)return WorldInteractionResult.Fail("库存不足。");stock.Amount-=amount;return WorldInteractionResult.Ok($"已使用物资 {amount:0.##}，剩余 {stock.Amount:0.##}。");
    }
}
