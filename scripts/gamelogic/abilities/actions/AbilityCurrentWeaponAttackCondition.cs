namespace GameLogic
{
    [GraphCallable("Current Weapon Attack", "技能 / 武器", GraphCallableUsage.Flow, ChineseName = "当前架势普攻")]
    public class AbilityCurrentWeaponAttackCondition : GraphConditionBase
    {
        public override string Description => "Current equipped stance attack";
        public override bool IsMet(GraphExecutionContext context)
        {
            var weapon = context?.GetUserData<GameObject3D>()?.GetComponent<WeaponEquipmentComponent3D>();
            return weapon?.CanRequestPrimaryAttack == true &&
                weapon.CurrentPrimaryAttack?.AbilityId == context.GetUserData<AbilityResource>()?.AbilityId;
        }
    }
}
