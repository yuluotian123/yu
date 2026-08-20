# 角色系统

本文描述当前玩家角色蓝图、Ability、移动、动画和 AI 的实际结构。玩家与 AI 共享 Movement、Ability 和 Animation 的运行能力，但只有玩家使用 CharacterGraph。

相关示例：

- [玩家角色示例](PLAYER_CHARACTER_EXAMPLE.md)
- [AI 角色示例](AI_CHARACTER_EXAMPLE.md)
- [玩家场景](../assets/scenes/player.tscn)
- [简单 AI 场景](../assets/scenes/ai_runner.tscn)
- [默认 CharacterGraph](../assets/graphs/character_graph.tres)

## 1. 完整结构

```text
玩家
InputMap / InputModule
        |
        v
PlayerCharacterInputComponent2D (100)
        | ICharacterInputProvider
        v
CharacterGraphComponent2D (90)
        |-- 生命周期: BeginPlay / Update / PhysicsUpdate / EndPlay
        |-- Move Axis1D ----------> AddMovementInput ---------+
        |-- Jump Press -----------> RequestJumpStart          |
        |                         -> SetJumpSustain(true)      |
        |-- Jump Release ---------> SetJumpSustain(false)     |
        |-- Attack / Dash --------> Ability 请求              |
        v                                                  v
AbilitySystemComponent2D (55)                  CharacterMovementComponent2D (50)
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
                                      CharacterAnimationComponent2D (20)
                                      |-- Locomotion HFSM
                                      |-- Ability 动画仲裁
                                      +-- AnimatedSprite2D 唯一写入者

AI
BehaviorTree Controller (100)
        |-- Movement API ----------------> CharacterMovementComponent2D
        +-- Ability API (战斗 AI 可选) ---> AbilitySystemComponent2D
```

简单 AI 不挂载 CharacterGraph、AbilitySystem 或 Animation。战斗 AI 可以按需增加 AbilitySystem 和 Animation，但仍不增加 CharacterGraph。

## 2. 组件职责

| 组件 | 职责 | 不负责 |
| --- | --- | --- |
| `PlayerCharacterInputComponent2D` | 把 InputModule 适配为 `ICharacterInputProvider` | 不硬编码 Move、Jump、Attack、Dash |
| `CharacterGraphComponent2D` | 持有玩家图并驱动多入口事件运行时 | 不做物理、Locomotion 或 AI 决策 |
| `AbilitySystemComponent2D` | 授予、冷却、优先级、并发、打断、取消、持久化 | 不扫描 CharacterGraph，不读取玩家输入 |
| `CharacterMovementComponent2D` | 收集意图、仲裁来源、计算速度并移动 Body | 不依赖 AbilitySystem，不播放动画 |
| `CharacterAnimationComponent2D` | 运行 LocomotionGraph、仲裁动画请求、写 Sprite | 不计算角色移动 |
| `CharacterPersistenceComponent2D` | 保存位置、朝向、标记和 Ability 冷却 | 不保存输入、图执行流、动画和速度 |

旧的 `CharacterCommandBufferComponent2D`、`SkillManagerComponent2D`、`SpriteAnimationComponent2D` 和玩家 Controller 已删除。

## 3. CharacterGraph

[CharacterGraphComponent2D](../scripts/gamelogic/character/graph/CharacterGraphComponent2D.cs) 持有 `CharacterGraphAsset : FlowGraphAsset`。它是 Character 专用的多入口 FlowGraph，不是 HFSM，也不包含 Idle、Run、Jump、Fall 等 Locomotion 状态。

### 生命周期入口

- `BeginPlay`：所有组件完成 `OnInit()` 后，在首次 Update 或 PhysicsUpdate 时触发一次。
- `Update(delta)`：普通更新入口。
- `PhysicsUpdate(delta)`：物理更新入口，也是玩家输入轮询入口。
- `EndPlay`：组件销毁、runtime 停止时触发一次。

每个事件节点默认不可重入。如果一次流程停在 Delay、WaitEvent 或运行中的 Ability，上一次执行完成前不会为同一事件节点创建第二条流程。

### 输入节点

`CharacterInputActionNodeData` 支持 `Pressed`、`Released`、`Held` 和 `Axis1D`。Axis1D 用 `PositiveAction - NegativeAction` 生成有符号值，并配置 `AxisDeadzone`、`AxisThreshold`、`ValueScale` 和反转。输入节点保存逻辑 Action 名，设备按键仍由 InputMap/InputModule 管理。

默认玩家图：

```text
Axis1D(player_move_left, player_move_right)
    -> AddMovementInput

Pressed(player_jump)
    -> RequestJumpStart
    -> SetJumpSustain(true)

Released(player_jump)
    -> SetJumpSustain(false)

Pressed(player_attack) -> ActivateAbility(attack)
Pressed(player_dash)   -> ActivateAbility(dash)

Attack -- Interrupt, priority 100 --> Dash
```

### 流程节点

- `Branch`：按条件选择输出。
- `Sequence`：按输出端口启动多条后续流程。
- `Delay`：等待指定时间。
- `WaitEvent`：等待事件版本变化，例如 `Ability.attack.Completed`。

### Ability 节点和关系边

Ability 节点使用稳定 `AbilityId`，资源路径只用于编辑器定位 Timeline。节点输出 `Activated`、`Completed`、`Cancelled` 和 `Rejected`。

关系边分为普通 `Flow`、打断用 `Interrupt` 和完成后请求用 `Completion`。Interrupt/Completion 可配置时间窗口、请求优先级和条件。CharacterGraph 只判断“是否存在这条关系且当前能否请求”；AbilitySystem 仍会检查授予、冷却、Ability policy、当前 Ability 优先级和并发规则，因此是最终权威。

## 4. AbilitySystem

[AbilitySystemComponent2D](../scripts/gamelogic/abilities/runtime/AbilitySystemComponent2D.cs) 只从 [AbilitySetResource](../scripts/gamelogic/abilities/runtime/AbilitySetResource.cs) 显式获得 Ability，不扫描 CharacterGraph。

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

Attack policy 优先级为 50，Dash 为 100。默认图只有 `Attack -> Dash`，所以 Dash 能打断 Attack，Attack 不能反向打断 Dash。

## 5. Movement

[CharacterMovementComponent2D](../scripts/gamelogic/character/movement/CharacterMovementComponent2D.cs) 同时是移动输入缓冲和物理权威。公开入口：

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

[CharacterAnimationComponent2D](../scripts/gamelogic/character/animation/CharacterAnimationComponent2D.cs) 合并了旧 Locomotion 组件和 Sprite 动画仲裁。第一版继续使用 AnimatedSprite2D、SpriteFrames 和 Locomotion HFSM。

LocomotionGraph 只读取 Movement 的 `MovementMode`、`IsOnFloor`、`MoveInputX` 和 `Velocity.Y`，包含 Idle、Run、Jump、Fall、Land 等动画语义。Ability Timeline 可以提交更高优先级动画请求；Ability 完成或取消时清除请求，下一物理帧自动恢复 Locomotion 动画。

CharacterGraph 不包含 Locomotion。二者都“是图”，但职责不同：CharacterGraph 表达玩家输入与 Ability 编排，LocomotionGraph 表达移动结果到动画状态的映射。

## 7. 玩家与 AI 共用边界

| 能力 | 玩家 | 简单 AI | 战斗 AI |
| --- | --- | --- | --- |
| Input Adapter | 是 | 否 | 否 |
| CharacterGraph | 是 | 否 | 否 |
| BehaviorTree | 否 | 是 | 是 |
| Movement | 是 | 是 | 是 |
| AbilitySystem | 是 | 否 | 按需 |
| Animation | 是 | 否 | 按需 |
| Persistence | 是 | 是 | 是 |

AI BehaviorTree 直接调用与玩家图相同的 Movement 和 AbilitySystem API。共享的是执行层，不共享玩家输入编排图。

## 8. 更新顺序

| Priority | 组件 |
| ---: | --- |
| 100 | Player Input / AI Controller |
| 90 | CharacterGraph |
| 55 | AbilitySystem |
| 50 | Movement |
| 20 | Animation |

因此本物理帧内，输入或 AI 先产生意图，Ability Timeline 再提交锁和速度覆盖，Movement 最终移动，Animation 最后读取结果并选择画面。

## 9. 持久化

Save schema 为 2。角色保存稳定 ID、位置、旋转、朝向、自定义标记和 Ability 冷却。字段名为 `abilities`；读取兼容旧 `skills` 字段。输入快照、Jump Sustain、当前图节点、动画、速度和 Ability Timeline 进度不保存。

## 10. Markdown 链接为什么不能点击

反引号表示代码，`res://` 是 Godot 资源协议，所以 `` `res://assets/scenes/player.tscn` `` 和 `` `docs/PLAYER_CHARACTER_EXAMPLE.md` `` 不是 Markdown 链接。应写成标准相对链接：

```markdown
[玩家场景](../assets/scenes/player.tscn)
[玩家示例](PLAYER_CHARACTER_EXAMPLE.md)
```

链接必须相对于当前 `.md` 文件所在目录，而不是相对于仓库根目录。

## 11. 验证

[CharacterGraphRuntimeSmokeTest](../scripts/test/CharacterGraphRuntimeSmokeTest.cs) 覆盖生命周期、不可重入、Axis 符号/死区、图驱动移动与跳跃、Ability 优先级/打断、Timeline 动画与 Dash 位移、锁释放、AI 场景组成和持久化边界。
