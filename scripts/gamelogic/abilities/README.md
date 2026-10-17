# Ability 系统

Ability 系统只包含能力资源、运行时和 Timeline Action。CharacterGraph、Movement 与 Animation 已独立到 `scripts/gamelogic/character`。

完整说明见 [角色系统](../../../docs/CHARACTER_SYSTEM.md)，使用示例见 [玩家角色示例](../../../docs/PLAYER_CHARACTER_EXAMPLE.md) 和 [AI 角色示例](../../../docs/AI_CHARACTER_EXAMPLE.md)。

## 结构

```text
AbilitySetResource
  -> AbilityResource
       |-- AbilityId
       |-- Cooldown
       |-- AbilityActivationPolicy
       +-- AbilityFlowGraphAsset
             -> AbilityTimelineNodeData
                  -> Animation / Movement / Hitbox / Event actions
```

核心实现：

- [AbilitySystemComponent3D.cs](runtime/AbilitySystemComponent3D.cs)
- [AbilityResource.cs](runtime/AbilityResource.cs)
- [AbilityRuntime.cs](runtime/AbilityRuntime.cs)
- [AbilitySetResource.cs](runtime/AbilitySetResource.cs)
- [AbilityFlowGraphAsset.cs](runtime/AbilityFlowGraphAsset.cs)
- [Ability Timeline Actions](actions/)

## 边界

AbilitySystem 负责显式授予、冷却、优先级、并发、激活、取消、Movement 锁和持久化。Ability Timeline 负责能力开始后的动画、位移、判定和事件时序。

CharacterGraph 不授予 Ability，也不拥有 Timeline 数据。玩家图通过稳定 AbilityId 请求冲刺；武器普攻由装备组件授予并触发。Interrupt/Completion 关系只匹配 Ability 节点，不能连接组件方法调用节点。AI BehaviorTree 可直接调用同一个 AbilitySystem API。

## 新增 Ability

1. 创建 `AbilityFlowGraphAsset` 和 Timeline。
2. 创建 `AbilityResource`，设置稳定 AbilityId、Cooldown、Policy 和 Graph。
3. 将资源加入角色的 `AbilitySetResource`。
4. 玩家 Ability 在 CharacterGraph 连接 Input 与 Ability 节点；AI Ability 从 BehaviorTree 直接请求。
5. 动画、Dash 位移、Hitbox 和 Event 放在 Timeline，不放在 CharacterGraph。

现有资源：

- [Greatsword Attack](../../../assets/resources/weapons/greatsword_attack.tres)
- [Greatsword Attack Timeline](../../../assets/resources/weapons/greatsword_attack_timeline.tres)
- [Katana Attack](../../../assets/resources/weapons/katana_attack.tres)
- [Katana Attack Timeline](../../../assets/resources/weapons/katana_attack_timeline.tres)
- [Dash Resource](../../../assets/resources/abilities/dash_ability.tres)
- [Dash Timeline](../../../assets/resources/abilities/dash_timeline.tres)
- [Player Ability Set](../../../assets/resources/abilities/player_ability_set.tres)
