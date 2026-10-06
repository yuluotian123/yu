# AI 角色示例

本文以 [ai_runner.tscn](../assets/scenes/ai_runner.tscn) 为例，说明 AI 不经过 CharacterGraph，直接由 BehaviorTree 调用 Movement；战斗 AI 则可按需直接调用 AbilitySystem。

总览见 [角色系统](CHARACTER_SYSTEM.md)，玩家对照见 [玩家角色示例](PLAYER_CHARACTER_EXAMPLE.md)。

## 简单 AI 场景

当前 AIRunner 只有：

| Priority | 组件 | 职责 |
| ---: | --- | --- |
| 100 | `SimpleAICharacterControllerComponent3D` | 运行 BehaviorTree，并将宿主配置写入黑板 |
| 50 | `CharacterMovementComponent3D` | 仲裁命令并执行物理移动 |
| 最后 | `CharacterPersistenceComponent3D` | 保存稳定状态 |

它明确没有：

- `PlayerCharacterInputComponent3D`
- `CharacterGraphComponent3D`
- `AbilitySystemComponent3D`
- `CharacterAnimationComponent3D`
- 已删除的 `CharacterCommandBufferComponent2D`

这保证 AI 的感知、选择和行为顺序只由 BehaviorTree 表达，不会与玩家输入图形成两个决策权威。

## 巡逻流程

行为树资源是 [ai_patrol_behavior_tree.tres](../assets/graphs/ai_patrol_behavior_tree.tres)。

```text
Root → Parallel（RequireAll）
         ├─ Action：往返巡逻（Patrol）
         └─ Action：周期跳跃（Periodic Jump）
```

两种动作直接调用 CharacterMovementComponent3D，不读取 AI Controller。Controller 只负责运行图、同步组件绑定和初始化黑板。动作也可放进技能 Flow 的 Action 节点。

## 巡逻与跳跃参数

Controller 的 Inspector 保留每个角色的配置，并在 Runtime.Start 后写入本地黑板：

| Inspector 配置 | 黑板键 |
| --- | --- |
| PatrolDistance | Patrol.Distance |
| StartDirection | Patrol.StartDirection |
| ReverseAtEdges | Patrol.ReverseAtEdges |
| EdgeLookAhead | Patrol.LookAhead |
| TurnPauseDuration | Patrol.TurnPause |
| JumpInterval | Jump.Interval |
| JumpSustainDuration | Jump.HoldDuration |

图资源自带这些参数的默认值，也可不通过该 Controller 运行。持续动作创建任务时读取配置；巡逻中心取任务开始时的宿主世界位置。到达水平范围或检测到前方无地面时转向，暂停后继续。它不处理墙体导航或寻路。

方向、巡逻中心、暂停、跳跃冷却和按住时间都保存在任务中；多个节点或角色可共用同一动作定义。周期跳跃在间隔到达且落地时请求起跳。移动与跳跃分别提交同一优先级命令的对应字段，不覆盖彼此；树中断时分别清除移动和跳跃输入。

更细的编排可以组合“设置朝向”“定向移动”“跳跃”“处于地面”“前方有地面”。旧的 CharacterAi 专用动作与辅助上下文已删除，现有巡逻资源已直接改为通用动作。

## 战斗 AI

战斗 AI 可以在场景中增加：

- `AbilitySystemComponent3D` 和自己的 `AbilitySetResource`。
- `CharacterAnimationComponent3D`，如果需要 Sprite 动画。

BehaviorTree Action 直接调用：

```csharp
AbilitySystemComponent3D abilities = owner.GetComponent<AbilitySystemComponent3D>();
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
5. 需要表现时增加 CharacterAnimationComponent3D 和 LocomotionGraph。

不要加入 CharacterGraph，也不要恢复 CommandBuffer 或 SkillManager。

## 验证

[CharacterGraphRuntimeSmokeTest](../scripts/test/CharacterGraphRuntimeSmokeTest.cs) 会加载 AIRunner 并断言简单 AI 没有 CharacterGraph、AbilitySystem 和 CommandBuffer，但具有 Movement。场景运行时再观察巡逻、边缘转向和周期跳跃。
