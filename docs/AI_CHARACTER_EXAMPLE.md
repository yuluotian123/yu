# AI 角色示例

本文以 [ai_runner.tscn](../assets/scenes/ai_runner.tscn) 为例，说明 AI 不经过 CharacterGraph，直接由 BehaviorTree 调用 Movement；战斗 AI 则可按需直接调用 AbilitySystem。

总览见 [角色系统](CHARACTER_SYSTEM.md)，玩家对照见 [玩家角色示例](PLAYER_CHARACTER_EXAMPLE.md)。

## 简单 AI 场景

当前 AIRunner 只有：

| Priority | 组件 | 职责 |
| ---: | --- | --- |
| 100 | `SimpleAICharacterControllerComponent2D` | 运行 BehaviorTree，产生巡逻和跳跃意图 |
| 50 | `CharacterMovementComponent2D` | 仲裁命令并执行物理移动 |
| 最后 | `CharacterPersistenceComponent2D` | 保存稳定状态 |

它明确没有：

- `PlayerCharacterInputComponent2D`
- `CharacterGraphComponent2D`
- `AbilitySystemComponent2D`
- `CharacterAnimationComponent2D`
- 已删除的 `CharacterCommandBufferComponent2D`

这保证 AI 的感知、选择和行为顺序只由 BehaviorTree 表达，不会与玩家输入图形成两个决策权威。

## 巡逻流程

行为树资源是 [ai_patrol_behavior_tree.tres](../assets/graphs/ai_patrol_behavior_tree.tres)。

```text
BehaviorTree tick
  -> MaintainDirection
  -> ShouldTurn?
       -> TurnAround / TurnPause
  -> ApplyPatrolMove
       -> SetFrameMoveAxis(direction)
  -> TryPeriodicJump
       -> RequestFrameJumpStart()
       -> SetFrameJumpSustain(timer > 0)
  -> SubmitCommand(move, jumpStart, jumpSustain, AI priority)
  -> CharacterMovementComponent2D
       -> acceleration / jump / gravity / MoveAndSlide
```

Controller 每个物理帧先重置本帧意图，再 tick BehaviorTree，最后一次性向 Movement 提交 `CharacterCommand2D`。Move Axis 和 Jump Start 是帧级数据；AI 每帧都提交 Jump Sustain，因此与玩家的 Press/Release 持久语义可以共用同一个 Movement API。

## 转向与边缘检测

BehaviorTree 可以读取：

- 出生位置和巡逻距离。
- 当前方向和转向暂停计时。
- `Movement.HasGroundAhead()` 的地面探测结果。
- 当前 `IsOnFloor`、速度和 MovementMode。

检测到巡逻边界、墙体策略或悬崖时，行为树修改自己的 `_direction`。Movement 只接收最终 axis，不知道“巡逻”“追击”或“逃跑”等 AI 语义。

## 周期跳跃

`CharacterAiTryPeriodicJumpAction` 维护跳跃冷却和 sustain timer：

```text
if IsOnFloor && JumpCooldown <= 0
    RequestFrameJumpStart()
    JumpSustainTimer = configured duration

SetFrameJumpSustain(JumpSustainTimer > 0)
```

行为树决定什么时候想跳和按住多久；Movement 决定 Jump Buffer、Coyote Time、重力、释放截断和锁是否允许跳跃。

## 战斗 AI

战斗 AI 可以在场景中增加：

- `AbilitySystemComponent2D` 和自己的 `AbilitySetResource`。
- `CharacterAnimationComponent2D`，如果需要 Sprite 动画。

BehaviorTree Action 直接调用：

```csharp
AbilitySystemComponent2D abilities = owner.GetComponent<AbilitySystemComponent2D>();
AbilityActivationResult result = abilities.TryActivateAbility("attack", "BehaviorTree");
```

不增加 CharacterGraph。BehaviorTree 负责目标选择、距离判断、攻击时机和失败后的重试；AbilitySystem 负责 Ability 是否已授予、冷却、优先级、并发、打断、Timeline 和 Movement 锁。

```text
Perception / Blackboard
        |
        v
BehaviorTree: Select target / Move / Attack / Retreat
        |                         |
        v                         v
Movement API                AbilitySystem API
                                  |
                                  v
                           Ability Timeline
                         Animation / Dash / Hitbox
```

## 玩家与 AI 为什么不共用 CharacterGraph

CharacterGraph 的定位是“玩家角色蓝图”：把输入 Action 映射为移动和 Ability，并配置玩家连招/打断关系。AI 已经有 BehaviorTree，如果再让 AI 经过 CharacterGraph，会出现两层行为编排：

- BehaviorTree 已决定 Attack，但 CharacterGraph 再决定是否路由。
- AI 技能关系可能被玩家输入图的连招边限制。
- 调试时无法快速判断决策来自 BehaviorTree 还是 CharacterGraph。

所以两者可以同时存在于项目中，但不应同时作为同一个 AI 实例的决策图。它们在 Movement 和 AbilitySystem 层汇合。

## 商业游戏中的常见分层

常见实现也是三层：

1. 决策层：BehaviorTree、StateTree、Utility AI 或定制 planner 选择目标和动作。
2. 执行层：Movement/Nav、Ability/Combat API 接受意图并做规则裁决。
3. 表现层：Animation Blueprint/State Machine 根据移动结果和 Ability montage/timeline 播放表现。

AI 通常不会模拟玩家按键，也不会复用玩家 Input Graph。它复用的是角色移动、寻路、技能、动画和命中判定等执行系统。对于可被玩家接管的角色，可以在 Controller/决策来源层切换 Player Input 与 AI，而不改变 Movement 和 AbilitySystem。

## 增加战斗 AI 的步骤

1. 在 AI 场景加入 AbilitySystem，并配置专用 AbilitySet。
2. 在 BehaviorTree 增加距离、视线、冷却后的攻击 Action。
3. Action 调用 `TryActivateAbility()`，根据返回值决定 Success、Failure 或稍后重试。
4. 需要打断逻辑时，由 BehaviorTree 选择请求时机，Ability policy 做最终优先级裁决。
5. 需要表现时增加 CharacterAnimationComponent2D 和 LocomotionGraph。

不要加入 CharacterGraph，也不要恢复 CommandBuffer 或 SkillManager。

## 验证

[CharacterGraphRuntimeSmokeTest](../scripts/test/CharacterGraphRuntimeSmokeTest.cs) 会加载 AIRunner 并断言简单 AI 没有 CharacterGraph、AbilitySystem 和 CommandBuffer，但具有 Movement。场景运行时再观察巡逻、边缘转向和周期跳跃。
