# 能力系统

Ability 系统只包含能力资源、运行时和 Timeline Action。CharacterGraph、Movement 与 Animation 已独立到 `scripts/gamelogic/character`。

完整说明见 [角色系统](characters.md)，使用示例见 [主角人物档案](../characters/protagonist.md) 和 [NPC 图配置与运行指南](npc-ai.md)。

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

- [AbilitySystemComponent3D.cs](../../scripts/gamelogic/abilities/runtime/AbilitySystemComponent3D.cs)
- [AbilityResource.cs](../../scripts/gamelogic/abilities/runtime/AbilityResource.cs)
- [AbilityRuntime.cs](../../scripts/gamelogic/abilities/runtime/AbilityRuntime.cs)
- [AbilitySetResource.cs](../../scripts/gamelogic/abilities/runtime/AbilitySetResource.cs)
- [AbilityFlowGraphAsset.cs](../../scripts/gamelogic/abilities/runtime/AbilityFlowGraphAsset.cs)
- [能力时间轴动作](../../scripts/gamelogic/abilities/actions)

## 边界

AbilitySystem 负责显式授予、冷却、优先级、并发、激活、取消、Movement 锁和持久化。Ability Timeline 负责能力开始后的动画、位移、判定和事件时序。

CharacterGraph 不授予 Ability，也不拥有 Timeline 数据。当前玩家基础 AbilitySet 为空，武器普攻由装备组件授予并触发。Interrupt/Completion 关系只匹配 Ability 节点，不能连接组件方法调用节点。AI BehaviorTree 可直接调用同一个 AbilitySystem API。

## 新增能力

1. 创建 `AbilityFlowGraphAsset` 和 Timeline。
2. 创建 `AbilityResource`，设置稳定 AbilityId、Cooldown、Policy 和 Graph。
3. 将资源加入角色的 `AbilitySetResource`。
4. 玩家 Ability 在 CharacterGraph 连接 Input 与 Ability 节点；AI Ability 从 BehaviorTree 直接请求。
5. 动画、位移、Hitbox 和 Event 放在 Timeline，不放在 CharacterGraph。

现有资源：

- [大剑普攻](../../assets/resources/weapons/greatsword_attack.tres)
- [大剑普攻时间轴](../../assets/resources/weapons/greatsword_attack_timeline.tres)
- [玩家基础能力集](../../assets/resources/abilities/player_ability_set.tres)
