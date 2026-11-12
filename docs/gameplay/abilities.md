# 能力系统

Ability 系统包含能力资源、技能输入、运行时和 Timeline Action。CharacterGraph、Movement 与 Animation 位于 `scripts/gamelogic/character`。

完整说明见 [角色系统](characters.md)，使用示例见 [主角人物档案](../characters/protagonist.md) 和 [NPC 图配置与运行指南](npc-ai.md)。

## 结构

```text
AbilitySetResource
  -> AbilityResource
       |-- AbilityId
       |-- Cooldown
       |-- AbilityActivationPolicy
       +-- AbilityFlowGraphAsset
             -> AbilityInputNodeData
                  -> Timeline / Condition / Return
                       -> Animation / Input Window / Particle actions
```

核心实现：

- [AbilitySystemComponent3D.cs](../../scripts/gamelogic/abilities/runtime/AbilitySystemComponent3D.cs)
- [AbilityResource.cs](../../scripts/gamelogic/abilities/runtime/AbilityResource.cs)
- [AbilityRuntime.cs](../../scripts/gamelogic/abilities/runtime/AbilityRuntime.cs)
- [AbilitySetResource.cs](../../scripts/gamelogic/abilities/runtime/AbilitySetResource.cs)
- [AbilityFlowGraphAsset.cs](../../scripts/gamelogic/abilities/runtime/AbilityFlowGraphAsset.cs)
- [能力时间轴动作](../../scripts/gamelogic/abilities/actions)

## 边界

AbilitySystem 负责授予、轮询技能输入、冷却、优先级、并发、激活、取消、Movement 锁和持久化。Ability Timeline 负责动画、输入窗口和特效时序。

当前玩家 CharacterGraph 用一个 `CharacterRegisterAbilityNodeData` 引用大剑 `WeaponDefinition`，从 `PrimaryAttack` / `StancePrimaryAttacks` 展开三套普攻。节点的 `SourcePath` 也支持单个 `AbilityResource`；不用在角色图重复维护架势映射。左键输入和当前架势条件放在武器技能图。移动、跳跃和架势切换仍使用原来的 CharacterGraph 输入与组件调用；架势切换不注册为技能。装备组件保留授予武器能力的通用入口，重复授予同一资源不会重建运行时。AI BehaviorTree 可直接调用同一 AbilitySystem API。

`AbilityInputNodeData` 复用现有输入模块的消费、UI 屏蔽与组合键规则；条件先决定当前武器/架势是否匹配，匹配后才消费左键。普通点击激活技能，运行中的同名输入只写入 Timeline 打开的缓存窗口。每个窗口最多保留一次续招，下一窗口重新接收输入；取消、完成和回收清空窗口与缓存。

当前三架势各保留一次完整普攻：`AbilityInput -> AbilityTimeline -> Return`，Timeline 不打开续招窗口，攻击中连点或按住不会续招。完整动画仍在技能图内，角色图注册方式不变。

当前攻击 v12 的完整 Timeline 为上段 1.060 秒、中段 0.997 秒、下段 0.911 秒。前摇 377–426 毫秒、收势 406–484 毫秒，动画锁与冷却覆盖完整动作，保留 48–54 毫秒落点短停。时序由攻击制作配置统一同步，不能只延长动画而提前结束 Ability。

通用连招逻辑继续保留：`AbilityInputWindowAction` 打开输入窗口，`AbilityConsumeInputCondition` 消费一次输入并选择下一段或收招。以后可在同一个 AbilityFlowGraph 中重新编排，无需武器连击状态机。每个结束分支使用独立 Return，避免单输入端口超限。集成检查用内存图覆盖 1/2/3 段、窗口重复输入和取消清理，不依赖已删除的连击素材。

特效场景由 AbilitySystem 持有的 `AbilityEffectPlayer3D` 按需实例化，播放时段由 Timeline 的 Particle Scene Clip 控制。武器攻击特效已全部移除；Player 不挂载独立换势特效组件。换势仍由装备组件处理排队与动画提交，实际开始时运行武器定义中的 `StanceEffectGraph`；它是无注册、无冷却的表现 Timeline。

## 新增能力

1. 创建 `AbilityFlowGraphAsset`，加入技能输入、条件分支和 Timeline。
2. 创建 `AbilityResource`，设置稳定 AbilityId、Cooldown、Policy 和 Graph。
3. 玩家武器能力在 CharacterGraph 添加 Register Ability 节点；通用角色也可用 `AbilitySetResource` 授予。
4. 玩家武器输入放在技能图；AI 从 BehaviorTree 直接请求。
5. 动画、位移、Hitbox 和 Event 放在 Timeline，不放在 CharacterGraph。

现有资源：

- [中段横扫](../../assets/resources/weapons/greatsword_attack.tres)及其[时间轴](../../assets/resources/weapons/greatsword_attack_timeline.tres)，保留 `greatsword_attack` ID
- [上段下劈](../../assets/resources/weapons/greatsword_attack_high.tres)及其[时间轴](../../assets/resources/weapons/greatsword_attack_high_timeline.tres)
- [下段前刺](../../assets/resources/weapons/greatsword_attack_low.tres)及其[时间轴](../../assets/resources/weapons/greatsword_attack_low_timeline.tres)
- [换势表现 Timeline](../../assets/resources/weapons/greatsword_stance_effect.tres)，由实际换势开始驱动，不注册为技能
