# 玩家操作角色示例

本文以 [player.tscn](../assets/scenes/player.tscn) 为例，说明移动、跳跃、Attack 和 Dash 如何从输入进入 CharacterGraph，再分别交给 Movement 或 AbilitySystem。

总览见 [角色系统](CHARACTER_SYSTEM.md)，AI 对照见 [AI 角色示例](AI_CHARACTER_EXAMPLE.md)。

## 场景组件

| Priority | 组件 | 配置 |
| ---: | --- | --- |
| 100 | `PlayerCharacterInputComponent2D` | 无 Move/Jump/Ability Action 字段 |
| 90 | `CharacterGraphComponent2D` | [character_graph.tres](../assets/graphs/character_graph.tres) |
| 55 | `AbilitySystemComponent2D` | [player_ability_set.tres](../assets/abilities/player_ability_set.tres) |
| 50 | `CharacterMovementComponent2D` | 移速、跳跃、重力、Body 路径 |
| 20 | `CharacterAnimationComponent2D` | [character_locomotion_hfsm.tres](../assets/graphs/character_locomotion_hfsm.tres) |
| 最后 | `PlayerCameraComponent2D`、`CharacterPersistenceComponent2D` | Camera 与 Save |

输入组件需要暴露在场景中，因为它是玩家与 InputModule 的设备边界；但它不暴露 `MoveAction`、`JumpAction` 或技能数组。所有逻辑 Action 映射都在 CharacterGraph 资源中编辑。

## 初始化

```text
GameObject2D._Ready
  -> 克隆并按 Priority 排序组件
  -> Input.OnInit: 获取 IInputModule
  -> CharacterGraph.OnInit: 查找 ICharacterInputProvider，创建 runtime
  -> AbilitySystem.OnInit: 从 AbilitySet 显式 Grant
  -> Movement.OnInit: 绑定 CharacterBody2D
  -> Animation.OnInit: 启动 Locomotion HFSM
  -> 首次 Update/PhysicsUpdate: CharacterGraph 触发 BeginPlay
```

CharacterGraph 不负责 Grant Ability。即使图里有 `ActivateAbility(attack)`，如果 AbilitySet 没有授予 `attack`，AbilitySystem 也会返回 `NotGranted`。

## 移动输入

默认图节点：

```text
Axis1D
  NegativeAction = player_move_left
  PositiveAction = player_move_right
  Deadzone = 0.1
  Scale = 1.0
        |
        v signed axis
AddMovementInput
```

运行流程：

```text
InputModule.GetActionStrength(right) - GetActionStrength(left)
  -> CharacterGraph 检查 deadzone
  -> AddMovementInput(axis, InputPriority)
  -> Movement 在本物理帧消费 axis
  -> 加速/减速、更新 Facing
  -> MoveAndSlide
  -> Animation 读取最终速度并选择 Idle 或 Run
```

例如左为 `0.8`、右为 `0.1`，图向 Movement 传递 `-0.7`，不会丢失方向符号。

## 跳跃输入

```text
Pressed(player_jump)
  -> RequestJumpStart
  -> SetJumpSustain(true)

Released(player_jump)
  -> SetJumpSustain(false)
```

`RequestJumpStart` 是一次性请求，进入 Movement 的 Jump Buffer。`SetJumpSustain(true)` 会持续保存，不会在下一物理帧自动清空；松开后设置 false，Movement 可按 `JumpCutMultiplier` 截短仍在上升的跳跃。

CharacterGraph 只提交“想跳”的意图。是否在地面、是否处于 Coyote Time、当前是否被 Ability 锁住，以及最终 Y 速度都由 Movement 决定。

## Attack

资源：

- [attack_ability.tres](../assets/abilities/attack_ability.tres)
- [attack_timeline.tres](../assets/abilities/attack_timeline.tres)

```text
Pressed(player_attack)
  -> CharacterAbilityNode(attack)
  -> AbilitySystem.TryActivateAbility("attack")
  -> Grant / Cooldown / Priority 检查
  -> 注册移动与跳跃锁
  -> Attack Ability Timeline
       |-- Animation Track
       |-- Slash / Camera / Event Actions
       +-- Complete 或 Cancel 清理
```

Ability 节点的 `Activated` 输出在启动成功时执行。节点保持活动直到 Ability 完成或取消，再从 `Completed` 或 `Cancelled` 输出继续；冷却或裁决失败从 `Rejected` 输出继续。

## Attack 被 Dash 打断

资源：

- [dash_ability.tres](../assets/abilities/dash_ability.tres)
- [dash_timeline.tres](../assets/abilities/dash_timeline.tres)

默认 CharacterGraph 只有：

```text
Attack -- Interrupt [window 0..any, request priority 100] --> Dash
```

玩家在 Attack 中按 Dash 时：

1. CharacterGraph 找到 `Attack -> Dash` Interrupt 边。
2. 检查时间窗口和边条件。
3. 把 Dash 请求和边的 request priority 交给 AbilitySystem。
4. AbilitySystem 再检查 Dash 已授予、未冷却、允许打断且有效优先级不低于 Attack。
5. Attack 以 `Interrupted` 取消，Timeline 的 Cancel action 清理动画和表现。
6. Dash Timeline 启动，在 AbilitySystem(55) 更新时提交速度覆盖。
7. Movement(50) 在同一物理帧应用覆盖，所以 Dash 不多延迟一帧。

不存在 `Dash -> Attack` 边，因此 Dash 期间的 Attack 请求会被 CharacterGraph 拒绝。即使未来加上这条边，AbilitySystem 仍保留最终裁决权。

## 动画恢复

Locomotion 和 Ability 都向 `CharacterAnimationComponent2D` 提交动画请求，只有该组件写 `AnimatedSprite2D`。

```text
Run request (Locomotion, lower priority)
Attack request (Ability, higher priority)
        -> 播放 Attack
Attack Complete/Cancel
        -> 清除 Attack request
        -> 自动恢复 Run 或 Idle
```

因此 Ability 不需要知道完成后应该回 Idle、Run 还是 Fall；Animation 会根据 Movement 的当前结果恢复。

## 增加一个玩家 Ability

1. 创建 `AbilityFlowGraphAsset` 和 Timeline。
2. 创建 `AbilityResource`，配置稳定 `AbilityId`、Cooldown 和 `AbilityActivationPolicy`。
3. 把 AbilityResource 加入玩家的 `AbilitySetResource`。
4. 在 CharacterGraph 新增 Input 节点和 Ability 节点。
5. 用 Flow 连接输入到 Ability；需要连招时再增加 Interrupt 或 Completion 关系边。
6. 为 Activated、Completed、Cancelled、Rejected 输出接入需要的时序或分支。

不需要修改 `PlayerCharacterInputComponent2D`，也不需要向 Movement 增加技能 Action 名。

## 可点击链接说明

文档中的仓库导航使用标准相对链接，例如 `[角色系统](CHARACTER_SYSTEM.md)`。Godot 的 `res://...` 适合资源加载，但不是 Markdown 仓库链接；反引号中的路径只是代码，也不会跳转。
