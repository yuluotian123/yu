# 角色系统

当前代码的完整使用说明见：[角色系统与图使用说明](character-graphs.md)。本文保留架构背景和设计边界；新增角色、Property Get/Set、黑板绑定时以使用说明为准。

本文描述当前玩家角色蓝图、Ability、移动、动画和 AI 的实际结构。玩家与 AI 共享 Movement、Ability 和 Animation 的运行能力，但只有玩家使用 CharacterGraph。

相关示例：

- [主角人物档案](../characters/protagonist.md)
- [NPC 图配置与运行指南](npc-ai.md)
- [玩家场景](../../assets/scenes/player.tscn)
- [NPC 正式小镇场景](../../assets/scenes/levels/forest_town_level.tscn)
- [Player 场景内联 CharacterGraph](../../assets/scenes/player.tscn)

## 1. 完整结构

```text
玩家
InputMap / InputModule
        |
        v
PlayerCharacterInputComponent3D (100)
        | ICharacterInputProvider
        v
CharacterGraphComponent3D (90)
        |-- 生命周期: BeginPlay / Update / PhysicsUpdate / EndPlay
        |-- Move Axis1D ----------> AddMovementInput ---------+
        |-- Jump Press -----------> RequestJumpStart          |
        |-- Jump Release ---------> SetJumpSustain(false)     |
        |-- Attack / Dash --------> Ability 请求              |
        v                                                  v
AbilitySystemComponent3D (55)                  CharacterMovementComponent3D (50)
        |                                      |-- 输入仲裁
        |-- Grant / Cooldown / Priority        |-- 跳跃缓冲 / 土狼时间
        |-- Concurrent / Interrupt / Cancel    |-- 重力 / 速度覆盖
        |-- AbilityRuntime                     |-- MoveAndSlide
        |        |
        |        v
        |   AbilityFlowGraph / Timeline
        |   Animation / Movement / Hitbox / Event
        |        |
        +--------+---- 动画请求 / 速度覆盖 -------------------+
                                                           |
                                                           v
                                      CharacterAnimationComponent3D (20)
                                      |-- Locomotion HFSM
                                      |-- Ability 动画仲裁
                                      +-- AnimatedSprite3D 唯一写入者

AI
AiAgentComponent3D (100) → Goal / Plan / GOAP → Action Flow
        |-- Movement API ----------------> CharacterMovementComponent3D
        +-- Ability API (战斗 AI 可选) ---> AbilitySystemComponent3D
```

NPC 使用自己的图配置决策，复用 Movement、Animation 和按需配置的 AbilitySystem。NPC 图与玩家 CharacterGraph 分别组织控制意图。

## 2. 组件职责

| 组件 | 职责 | 不负责 |
| --- | --- | --- |
| `PlayerCharacterInputComponent3D` | 把 InputModule 适配为 `ICharacterInputProvider` | 不硬编码 Move、Jump、Attack、Dash |
| `CharacterGraphComponent3D` | 持有玩家图并驱动多入口事件运行时 | 不做物理、Locomotion 或 AI 决策 |
| `AbilitySystemComponent3D` | 授予、冷却、优先级、并发、打断、取消、持久化 | 不扫描 CharacterGraph，不读取玩家输入 |
| `CharacterMovementComponent3D` | 收集意图、仲裁来源、计算速度并移动 Body | 不依赖 AbilitySystem，不播放动画 |
| `CharacterAnimationComponent3D` | 运行 LocomotionGraph、仲裁动画请求、写 Sprite | 不计算角色移动 |
| `CharacterPersistenceComponent3D` | 保存位置、朝向、标记和 Ability 冷却 | 不保存输入、图执行流、动画和速度 |

旧的 `CharacterCommandBufferComponent2D`、`SkillManagerComponent2D`、`SpriteAnimationComponent2D` 和玩家 Controller 已删除。

## 3. CharacterGraph

[CharacterGraphComponent3D](../../scripts/gamelogic/character/graph/CharacterGraphComponent3D.cs) 持有 `CharacterGraphAsset : FlowGraphAsset`。它是 Character 专用的多入口 FlowGraph，不是 HFSM，也不包含 Idle、Run、Jump、Fall 等 Locomotion 状态。

### 生命周期入口

- `BeginPlay`：所有组件完成 `OnInit()` 后，在首次 Update 或 PhysicsUpdate 时触发一次。
- `Update(delta)`：普通更新入口。
- `PhysicsUpdate(delta)`：物理更新入口，也是玩家输入轮询入口。
- `EndPlay`：组件销毁、runtime 停止时触发一次。

每个事件节点默认不可重入。如果一次流程停在 Delay、WaitEvent 或运行中的 Ability，上一次执行完成前不会为同一事件节点创建第二条流程。

### 输入节点

`CharacterInputActionNodeData` 支持 `Pressed`、`Released`、`Held` 和 `Axis1D`。Axis1D 用 `PositiveAction - NegativeAction` 生成有符号值，并配置 `AxisThreshold`、`ValueScale` 和反转。输入节点保存逻辑 Action 名，设备按键仍由 InputMap/InputModule 管理。

默认玩家图：

```text
Axis1D(player_move_left, player_move_right)
    -> AddMovementInput

Pressed(player_jump)
    -> RequestJumpStart

Released(player_jump)
    -> SetJumpSustain(false)

Pressed(player_attack) -> WeaponEquipment.TryPrimaryAttack()
```

CharacterGraph 的 Action 菜单隐藏组件 Call/Get/Set 包装和 Use Ability。组件操作使用直接成员节点；技能激活使用专用节点，确保打断窗口与关系规则生效。其他 Flow 与行为树仍可使用这些通用动作。旧的 Character 移动节点、专用 Sequence/WaitEvent 类型和加载时兼容迁移代码已删除。

输入 Inspector 按 Pressed / Released / Held / Axis1D 显示适用参数；只保留一个 AxisThreshold，轴模式支持缩放与反向。切换模式保留之前填写的字段。

### 流程节点

- `Branch`：按条件选择输出。
- `Sequence`：通用顺序触发节点，按端口依次启动分支，不等待前一分支完成。
- `Delay`：等待指定时间，与 Wait Seconds 动作共用 GraphDelayTask。
- `Wait Event`：等待进入节点之后的新事件，例如 `Ability.attack.Completed`；Inspector 可选择已有事件，必须设置正数 Timeout，分别从 Received / Timeout 输出。
- `Publish Event`：向当前执行上下文的事件流发送事件。普通 Flow 各运行时隔离；CharacterGraph 的所有事件分支共享本角色的事件流。外部通过 `Runtime.Context.Events.Publish(name)` 或组件的 PublishEvent 调用发布。

### Ability 节点和关系边

Ability 节点使用稳定 `AbilityId`，资源路径只用于编辑器定位 Timeline。节点输出 `Activated`、`Completed`、`Cancelled` 和 `Rejected`。它与通用 Use Ability 共用 AbilityActivationHandle，跟踪一次激活的版本；同一技能重新激活后，旧节点走 Cancelled，既不会等待新一次激活，也不会将其取消。

关系边分为普通 `Flow`、打断用 `Interrupt` 和完成后请求用 `Completion`。Interrupt/Completion 可配置时间窗口、请求优先级和条件。CharacterGraph 只判断“是否存在这条关系且当前能否请求”；AbilitySystem 仍会检查授予、冷却、Ability policy、当前 Ability 优先级和并发规则，因此是最终权威。

## 4. AbilitySystem

[AbilitySystemComponent3D](../../scripts/gamelogic/abilities/runtime/AbilitySystemComponent3D.cs) 只从 [AbilitySetResource](../../scripts/gamelogic/abilities/runtime/AbilitySetResource.cs) 显式获得 Ability，不扫描 CharacterGraph。

```text
AbilitySetResource
    -> AbilityResource(attack)
        |-- AbilityId / Cooldown
        |-- AbilityActivationPolicy
        +-- AbilityFlowGraphAsset
    -> AbilityResource(dash)
```

关键类型：

- `AbilityResource`：稳定 ID、显示名、冷却、Policy 和 Timeline 图。
- `AbilityRuntime`：单个角色上的执行实例、冷却结束时间、运行状态和 Return label。
- `AbilityActivationPolicy`：优先级、能否打断、是否并发、是否锁移动/跳跃。
- `AbilityFlowGraphAsset`：Ability 的执行流程。
- `AbilityTimelineNodeData`：Animation、Movement、Hitbox、Event 等时序轨道。

```text
TryActivateAbility(id, source, optional requestPriority)
    -> 是否已授予 / 是否冷却 / 是否已激活
    -> 并发 / CanInterrupt / 有效优先级裁决
    -> 取消被打断 Ability
    -> 创建或复用 AbilityRuntime
    -> 注册 Movement 控制锁
    -> 启动 AbilityFlowGraph
```

当前主角只授予大剑普攻，冲刺能力及其打断接线已移除。

## 5. Movement

[CharacterMovementComponent3D](../../scripts/gamelogic/character/movement/CharacterMovementComponent3D.cs) 同时是移动输入缓冲和物理权威。公开入口：

- `AddMovementInput(axis, sourcePriority)` / `StopMovementInput(sourcePriority)`
- `SubmitCommand(command, sourcePriority)`
- `RequestJumpStart(sourcePriority)` / `SetJumpSustain(requested, sourcePriority)`
- `RequestVelocityOverride(request)`
- `SetControlLock(...)` / `ClearControlLock(key)`

Move Axis 和 Jump Start 是本物理帧意图；Jump Sustain 是持久意图，按下后保持到 Release 明确关闭。这样图只需在 Pressed 和 Released 时提交，不必每帧重复发送 Held。

同一物理帧有多个来源时，较高 `sourcePriority` 获胜。AbilitySystem 以 AbilityId 作为控制锁令牌；Movement 只认识令牌和锁规则，不反向引用 AbilitySystem。

```text
消费本帧命令
-> 解析移动/跳跃锁
-> 水平加减速
-> Jump Buffer / Coyote Time / Release Cut
-> 重力
-> Ability 速度覆盖
-> MoveAndSlide
-> 发布最终速度、接地和 MovementMode
```

## 6. Animation 与 Locomotion

[CharacterAnimationComponent3D](../../scripts/gamelogic/character/animation/CharacterAnimationComponent3D.cs) 合并了旧 Locomotion 组件和 Sprite 动画仲裁。第一版继续使用 AnimatedSprite3D、SpriteFrames 和 Locomotion HFSM。

LocomotionGraph 读取 Movement 的接地、移动量和垂直速度，包含待机、跑步、起跳、腾空、下落和落地等动画语义。动画图不反向驱动物理，也不因落地动画限制平移。Ability Timeline 可以提交更高优先级动画请求；Ability 完成或取消时清除请求，下一物理帧自动恢复 Locomotion 动画。

CharacterGraph 不包含 Locomotion。二者都“是图”，但职责不同：CharacterGraph 表达玩家输入与 Ability 编排，LocomotionGraph 表达移动结果到动画状态的映射。

## 7. 玩家与 AI 共用边界

| 能力 | 玩家 | NPC |
| --- | --- | --- |
| Input Adapter | 是 | 否 |
| CharacterGraph | 是 | 否 |
| NPC 图 / GOAP | 否 | 是 |
| Movement | 是 | 是 |
| AbilitySystem | 是 | 按需 |
| Animation | 是 | 是 |
| Persistence | 是 | 是 |

NPC 的行动能力调用与玩家图相同的 Movement 和 AbilitySystem API，决策配置见 [NPC 图指南](npc-ai.md)。

## 8. 更新顺序

| Priority | 组件 |
| ---: | --- |
| 100 | 玩家输入／AI 控制器 |
| 90 | CharacterGraph |
| 55 | AbilitySystem |
| 50 | Movement |
| 20 | Animation |

因此本物理帧内，输入或 AI 先产生意图，Ability Timeline 再提交锁和速度覆盖，Movement 最终移动，Animation 最后读取结果并选择画面。

## 9. 持久化

存档文件的 `FormatVersion` 为 2，角色分区 schema 为 5。角色保存稳定 ID、位置、旋转、朝向、自定义标记、武器所有权与装备、Ability 冷却。能力字段为 `abilities`，读取兼容旧 `skills` 字段。输入快照、Jump Sustain、当前图节点、动画、速度和 Ability Timeline 进度不保存。格式及迁移规则见[存档系统](saves.md)。

## 10. 验证

运行 ForestTownLevel 检查移动、跳跃、技能打断、动画、AI 行为及持久化边界。

## 3D 场景与移动模式

当前 ForestTownLevel 的角色使用 GameObject3D 与 Component3D，碰撞统一由 CharacterBody3D / StaticBody3D 处理。2D 指人物精灵外观，使用 AnimatedSprite3D，不再使用 SubViewport 或二维碰撞投影。MovementSpace 默认 SideView；Free3D 开启 X/Z 双轴移动。世界单位为米，Y 向上。当前关卡的自由移动与镜头配置见 [HD-2D 场景镜头](../rendering/hd2d-camera.md)。

## 玩家接线示例

主角外观、完整动作预览及新需求统一见[主角人物档案](../characters/protagonist.md)。本节仅说明当前玩家场景的组件与输入接线。

### 场景组件

| 优先级 | 组件 | 配置 |
| ---: | --- | --- |
| 110 | `InventoryComponent3D` | 初始物品、背包容量与物品状态 |
| 100 | `PlayerCharacterInputComponent3D` | 无 Move/Jump/Ability Action 字段 |
| 90 | `CharacterGraphComponent3D` | `player.tscn` 内联 CharacterGraph |
| 55 | `AbilitySystemComponent3D` | [player_ability_set.tres](../../assets/resources/abilities/player_ability_set.tres) |
| 50 | `CharacterMovementComponent3D` | 移速、跳跃、重力、Body 路径 |
| 20 | `CharacterAnimationComponent3D` | `player.tscn` 内嵌 Locomotion HFSM |
| 10 | `WeaponEquipmentComponent3D` | 初始装备槽、默认武器 |
| 最后 | `CharacterPersistenceComponent3D` | 存档 |

输入组件需要暴露在场景中，因为它是玩家与 InputModule 的设备边界；但它不暴露 `MoveAction`、`JumpAction` 或技能数组。所有逻辑 Action 映射都在 CharacterGraph 资源中编辑。

相机直接放在关卡的 CameraRig/Camera3D 中，Player 不包含相机。CameraRig 的 SceneCameraComponent3D 自动绑定 Player 并处理跟随，ICameraModule 只路由控制接口。参见 [场景相机](../rendering/camera.md)。

### 初始化

```text
GameObject3D._Ready
  -> 克隆并按 Priority 排序组件
  -> Inventory.OnInit: 初始化物品与格子位置
  -> Input.OnInit: 获取 IInputModule
  -> CharacterGraph.OnInit: 查找 ICharacterInputProvider，创建 runtime
  -> AbilitySystem.OnInit: 读取基础 AbilitySet（当前为空）
  -> Movement.OnInit: 绑定 CharacterBody3D
  -> Animation.OnInit: 启动 Locomotion HFSM
  -> WeaponEquipment.OnInit: 授予武器普攻，应用初始装备与外观
  -> 首次 Update/PhysicsUpdate: CharacterGraph 触发 BeginPlay
```

CharacterGraph 不负责 Grant Ability。基础 AbilitySet 当前为空；武器装备组件授予 `greatsword_attack`。玩家没有冲刺输入或冲刺能力；请求未授予的 AbilityId 时，AbilitySystem 返回 `NotGranted`。

### 移动输入

默认图节点：

```text
Axis1D
  NegativeAction = player_move_left
  PositiveAction = player_move_right
  AxisThreshold = 0.1
  Scale = 1.0
        |
        v 带符号的轴值
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

### 跳跃输入

```text
Pressed(player_jump)
  -> RequestJumpStart

Released(player_jump)
  -> SetJumpSustain(false)
```

`RequestJumpStart` 同时发起跳跃并开启持续输入，进入 Movement 的 Jump Buffer。持续输入会保存，不会在下一物理帧自动清空；松开后设置 false，Movement 可按 `JumpCutMultiplier` 截短仍在上升的跳跃。

当前玩家使用 `JumpVelocity = 4.31`、`Gravity = 16.5`，在默认 60 Hz 物理更新下完整跳跃约抬升 0.6 米；短按仍会截短上升。

起跳、腾空与落地均保留平移和空中控制，不使用额外的落地平移锁。动画图落地状态仍使用原有 0.20 秒计时，移动输入始终由 Movement 正常处理。


CharacterGraph 只提交“想跳”的意图。是否在地面、是否处于 Coyote Time、当前是否被 Ability 锁住，以及最终 Y 速度都由 Movement 决定。

<a id="attack"></a>
### 攻击

资源：

- [大剑普攻](../../assets/resources/weapons/greatsword_attack.tres)及其[时间轴](../../assets/resources/weapons/greatsword_attack_timeline.tres)

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

普攻使用组件方法调用节点，返回值表示是否成功激活能力；攻击时间轴由 AbilitySystem 继续更新。当前玩家只装备大剑，没有武器切换输入。

### 扩展能力与仲裁

Interrupt/Completion 关系以 `CharacterAbilityNode` 的 AbilityId 匹配源能力，不能从 `TryPrimaryAttack` 这样的组件调用节点建立。未来增加武器普攻打断时，需要先接通武器技能与关系节点的映射。

Ability 节点的 `Activated` 输出在启动成功时执行。节点保持活动直到 Ability 完成或取消，再从 `Completed` 或 `Cancelled` 输出继续；冷却或裁决失败从 `Rejected` 输出继续。

### 动画恢复

Locomotion 和 Ability 都向 `CharacterAnimationComponent3D` 提交动画请求，只有该组件写 `AnimatedSprite3D`。

```text
移动请求 Run（低优先级）
攻击请求 Attack（高优先级）
        -> 播放 Attack
攻击完成／取消
        -> 清除 Attack request
        -> 自动恢复 Run 或 Idle
```

因此 Ability 不需要知道完成后应该回 Idle、Run 还是 Fall；Animation 会根据 Movement 的当前结果恢复。

### 增加一个玩家 Ability

1. 创建 `AbilityFlowGraphAsset` 和 Timeline。
2. 创建 `AbilityResource`，配置稳定 `AbilityId`、Cooldown 和 `AbilityActivationPolicy`。
3. 把 AbilityResource 加入玩家的 `AbilitySetResource`。
4. 在 CharacterGraph 新增 Input 节点和 Ability 节点。
5. 用 Flow 连接输入到 Ability；需要连招时再增加 Interrupt 或 Completion 关系边。
6. 为 Activated、Completed、Cancelled、Rejected 输出接入需要的时序或分支。

不需要修改 `PlayerCharacterInputComponent3D`，也不需要向 Movement 增加技能 Action 名。
