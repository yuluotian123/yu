# 玩家操作角色示例

推荐先阅读：[角色系统与图使用说明](CHARACTER_GRAPH_USAGE.md)。

本文以 [player.tscn](../assets/scenes/player.tscn) 为例，说明移动、跳跃、武器普攻和冲刺如何从输入进入 CharacterGraph，再交给 Movement、武器装备或 AbilitySystem。

总览见 [角色系统](CHARACTER_SYSTEM.md)，AI 对照见 [AI 角色示例](AI_CHARACTER_EXAMPLE.md)。

## 场景组件

| Priority | 组件 | 配置 |
| ---: | --- | --- |
| 110 | `InventoryComponent3D` | 初始物品、背包容量与窗口 |
| 100 | `PlayerCharacterInputComponent3D` | 无 Move/Jump/Ability Action 字段 |
| 90 | `CharacterGraphComponent3D` | `player.tscn` 内联 CharacterGraph |
| 55 | `AbilitySystemComponent3D` | [player_ability_set.tres](../assets/resources/abilities/player_ability_set.tres) |
| 50 | `CharacterMovementComponent3D` | 移速、跳跃、重力、Body 路径 |
| 20 | `CharacterAnimationComponent3D` | `player.tscn` 内嵌 Locomotion HFSM |
| 10 | `WeaponEquipmentComponent3D` | 初始装备槽、默认武器 |
| 最后 | `CharacterPersistenceComponent3D` | Save |

输入组件需要暴露在场景中，因为它是玩家与 InputModule 的设备边界；但它不暴露 `MoveAction`、`JumpAction` 或技能数组。所有逻辑 Action 映射都在 CharacterGraph 资源中编辑。

相机直接放在关卡的 CameraRig/Camera3D 中，Player 不包含相机。CameraRig 的 SceneCameraComponent3D 自动绑定 Player 并处理跟随，ICameraModule 只路由控制接口。参见 [场景相机](CAMERA_SYSTEM.md)。

## 初始化

```text
GameObject3D._Ready
  -> 克隆并按 Priority 排序组件
  -> Inventory.OnInit: 初始化物品与背包入口
  -> Input.OnInit: 获取 IInputModule
  -> CharacterGraph.OnInit: 查找 ICharacterInputProvider，创建 runtime
  -> AbilitySystem.OnInit: 从 AbilitySet 授予冲刺
  -> Movement.OnInit: 绑定 CharacterBody3D
  -> Animation.OnInit: 启动 Locomotion HFSM
  -> WeaponEquipment.OnInit: 授予武器普攻，应用初始装备与外观
  -> 首次 Update/PhysicsUpdate: CharacterGraph 触发 BeginPlay
```

CharacterGraph 不负责 Grant Ability。基础 AbilitySet 只授予 `dash`；武器装备组件授予 `greatsword_attack` 和 `katana_attack`。请求未授予的 AbilityId 时，AbilitySystem 返回 `NotGranted`。

## 移动输入

默认图节点：

```text
Axis1D
  NegativeAction = player_move_left
  PositiveAction = player_move_right
  AxisThreshold = 0.1
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

Released(player_jump)
  -> SetJumpSustain(false)
```

`RequestJumpStart` 同时发起跳跃并开启持续输入，进入 Movement 的 Jump Buffer。持续输入会保存，不会在下一物理帧自动清空；松开后设置 false，Movement 可按 `JumpCutMultiplier` 截短仍在上升的跳跃。

CharacterGraph 只提交“想跳”的意图。是否在地面、是否处于 Coyote Time、当前是否被 Ability 锁住，以及最终 Y 速度都由 Movement 决定。

## Attack

资源：

- [大剑普攻](../assets/resources/weapons/greatsword_attack.tres)及其[时间轴](../assets/resources/weapons/greatsword_attack_timeline.tres)
- [太刀普攻](../assets/resources/weapons/katana_attack.tres)及其[时间轴](../assets/resources/weapons/katana_attack_timeline.tres)

```text
Pressed(player_attack)
  -> WeaponEquipment.TryPrimaryAttack()
  -> AbilitySystem.TryActivateAbility(CurrentWeapon.PrimaryAttack.AbilityId)
  -> Grant / Cooldown / Priority 检查
  -> 注册移动与跳跃锁
  -> Attack Ability Timeline
       |-- Animation Track
       +-- Complete 或 Cancel 清理
```

普攻使用组件方法调用节点，返回值表示是否成功激活能力；攻击时间轴由 AbilitySystem 继续更新。按 Q 调用 `RequestNextWeapon`，攻击或冲刺期间的切换会延迟到能力结束。

## 冲刺与攻击仲裁

资源：

- [dash_ability.tres](../assets/resources/abilities/dash_ability.tres)
- [dash_timeline.tres](../assets/resources/abilities/dash_timeline.tres)

冲刺通过独立的 Ability 节点触发：

```text
Pressed(player_dash)
  -> CharacterAbilityNode(dash)
  -> AbilitySystem.TryActivateAbility("dash")
  -> Dash Timeline 提交速度覆盖
  -> Movement 在同一物理帧应用覆盖
```

当前图不配置攻击与冲刺的 Interrupt 边。攻击期间，CharacterGraph 拒绝冲刺请求；冲刺期间，AbilitySystem 按优先级拒绝普攻请求。

Interrupt/Completion 关系以 `CharacterAbilityNode` 的 AbilityId 匹配源能力，不能从 `TryPrimaryAttack` 这样的组件调用节点建立。未来增加武器普攻打断时，需要先接通武器技能与关系节点的映射。

Ability 节点的 `Activated` 输出在启动成功时执行。节点保持活动直到 Ability 完成或取消，再从 `Completed` 或 `Cancelled` 输出继续；冷却或裁决失败从 `Rejected` 输出继续。

## 动画恢复

Locomotion 和 Ability 都向 `CharacterAnimationComponent3D` 提交动画请求，只有该组件写 `AnimatedSprite3D`。

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

不需要修改 `PlayerCharacterInputComponent3D`，也不需要向 Movement 增加技能 Action 名。

## 可点击链接说明

文档中的仓库导航使用标准相对链接，例如 `[角色系统](CHARACTER_SYSTEM.md)`。Godot 的 `res://...` 适合资源加载，但不是 Markdown 仓库链接；反引号中的路径只是代码，也不会跳转。
