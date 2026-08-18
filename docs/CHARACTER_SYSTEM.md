# 角色系统说明

本文档描述当前角色系统的实际实现。系统以 `GameObject2D + Component2D` 为容器，以 CharacterGraph 统一接入玩家输入和 AI 请求，以 HFSM 表达角色状态，以 Skill FlowGraph 执行技能时间线，并由统一移动组件完成最终物理运动。

## 1. 设计边界

角色系统遵循以下职责划分：

- Controller 只产生意图，不直接修改速度或 HFSM 状态。
- CharacterGraph 负责把输入或 AI 请求路由到 Action，并管理 Action 的进入、中断和返回。
- Action 节点定义整条技能链的执行策略，`SkillResource` 只描述可复用技能内容。
- `SkillManagerComponent2D` 管理技能实例、冷却、启动、中断和 FlowGraph 更新。
- `CharacterMovementComponent2D` 是唯一的常规移动与碰撞执行者。
- HFSM 负责语义状态和表现切换，不负责实现移动物理或技能时间线。
- Save V2 只恢复耐久语义状态，不恢复某一物理帧的临时执行现场。

整体数据流如下：

```text
Player InputMap / AI BehaviorTree
              |
              v
Controller -> CharacterCommandBufferComponent2D
              |                 |
              | command         | CharacterActionRequest
              v                 v
CharacterMovement       CharacterGraphComponent2D
       ^                    |          |
       |                    |          v
       |                    |   CharacterSkillChainNodeData
       |                    |          |
       |                    |          v
       |                    |   SkillManagerComponent2D
       |                    |          |
       +---- movement override --------+
                            |
                            v
                  HFSM / Blackboard / Presentation
```

## 2. 场景组件

### 2.1 Player

`res://assets/scenes/player.tscn` 当前包含 8 个组件：

| 组件 | 职责 |
| --- | --- |
| `PlayerCharacterControllerComponent2D` | 从 `InputModule` 读取左右移动和跳跃，提交基础命令 |
| `CharacterCommandBufferComponent2D` | 保存本帧基础命令和最高优先级 Action 请求 |
| `CharacterGraphComponent2D` | 运行 CharacterGraph/HFSM，扫描全局 Input 节点并路由 Action |
| `SkillManagerComponent2D` | 注册技能、管理冷却、运行时间线和处理中断 |
| `CharacterMovementComponent2D` | 跑、跳、重力、地面检测、朝向、速度覆盖和 `MoveAndSlide()` |
| `PlayerCameraComponent2D` | 玩家相机跟随与前视 |
| `SpriteAnimationComponent2D` | 动画表现 |
| `CharacterPersistenceComponent2D` | Save V2 角色数据采集和恢复 |

### 2.2 AI

`res://assets/scenes/ai_runner.tscn` 当前包含 6 个组件：

| 组件 | 职责 |
| --- | --- |
| `SimpleAICharacterControllerComponent2D` | 运行 BehaviorTree，提交基础命令和 Action 请求 |
| `CharacterCommandBufferComponent2D` | 与玩家共用同一种命令缓冲 |
| `CharacterGraphComponent2D` | 与玩家共用同一份角色图，`PhysicalInputEnabled = false` |
| `SkillManagerComponent2D` | 与玩家使用相同的技能运行时 |
| `CharacterMovementComponent2D` | 与玩家使用相同的移动实现，可使用不同 Profile |
| `CharacterPersistenceComponent2D` | 保存稳定 AI 角色数据 |

玩家与 AI 的区别仅在命令来源。进入 CharacterGraph 后，两者走同一套 Action、Skill 和 Movement 流程。

## 3. 物理帧执行顺序

`GameObject2D` 按组件 `Priority` 从高到低执行物理更新。角色相关优先级为：

| 阶段 | Priority | 主要组件 |
| --- | ---: | --- |
| 输入/AI | 100 | Player Controller、AI Controller、Command Buffer |
| 状态图 | 90 | CharacterGraph/HFSM |
| 技能时间线 | 55 | SkillManager |
| 移动与碰撞 | 50 | CharacterMovement |
| 表现 | 10 | Camera、Animation |
| 持久化注册 | `int.MinValue + 10` | CharacterPersistence |

一帧内的实际顺序为：

1. Player Controller 或 AI Controller 生成 `CharacterCommand2D`。
2. AI 需要触发技能时，额外提交 `CharacterActionRequest`。
3. CharacterGraph 读取尚未消费的命令快照并写入 `Character.Command.*` 黑板。
4. CharacterGraph 扫描物理输入节点和 AI Action 请求，选择一个可进入的 Action。
5. HFSM 更新当前状态；Action 进入后启动技能链。
6. SkillManager 更新所有运行中的 Skill FlowGraph。时间线可以在本帧提交移动速度覆盖。
7. Movement 消费一次基础命令，读取技能锁定策略，执行水平移动、跳跃、重力和速度覆盖。
8. Movement 调用 `CharacterBody2D.MoveAndSlide()`，同步 Owner 与 Body，并写入 `Character.Movement.*` 黑板。

这个顺序保证 Dash 一类技能可以在进入 Action 的同一物理帧覆盖普通移动速度。

## 4. 命令与 Action 请求

### 4.1 CharacterCommand2D

基础运动命令是一次性帧数据：

| 字段 | 含义 |
| --- | --- |
| `MoveAxisX` | 水平移动轴，提交时限制在 `[-1, 1]` |
| `JumpStartRequested` | 本帧请求开始跳跃 |
| `JumpSustainRequested` | 本帧仍按住跳跃，用于可变跳高和释放截断 |

CharacterGraph 读取 `Pending` 发布黑板快照，Movement 随后调用 `Consume()`。消费后缓冲恢复为空命令，因此 Controller 必须每个物理帧提交当前意图。

### 4.2 CharacterActionRequest

Action 请求包含：

| 字段 | 含义 |
| --- | --- |
| `ActionId` | 要匹配的角色图 Input 节点逻辑 ID，如 `dash`、`attack` |
| `Priority` | 多个待处理请求之间的仲裁优先级 |

需要特别区分两种优先级：

- `CharacterActionRequest.Priority` 只决定同一帧保留哪个外部请求。
- `CharacterSkillChainNodeData.Priority` 决定 Action 候选顺序以及技能能否中断当前技能。

请求优先级不会提升技能中断权限。AI 即使提交很高的请求优先级，也不能绕过 Action 本身的策略。

## 5. CharacterGraph

根图资源是 `res://assets/graphs/character_graph.tres`。当前结构为：

```text
Dash Input   -> Dash Action
Attack Input -> Attack Action

Locomotion (default composite state)
```

Input 节点是全局触发源，不是 HFSM 状态：

- 没有输入端口，不能从 AnyState 或普通状态连接到它。
- 只有一个 `Triggered` 输出，可以连接多个 Action。
- Input 与 Action 保持分离，输入配置不会进入技能资源。
- 连线负责附加当前状态、移动模式、冷却或黑板等条件。
- 图中不使用状态 Tag；接地、移动、当前 Action 和排除状态分别使用明确的 Movement 值、黑板条件、Action ID 和稳定状态 ID 表达。

### 5.1 Input 节点配置

`CharacterInputActionNodeData` 支持：

| 字段 | 作用 |
| --- | --- |
| `ActionName` | `InputMap`/`InputModule` 中的逻辑 Action 名称 |
| `ActionId` | AI 请求匹配 ID；为空时回退到 `ActionName` |
| `TriggerMode` | `Pressed`、`Released`、`Held` 或 `Axis` |
| `HandlerLayer` | InputModule 输入层，如 `Combat` |
| `ConsumeInput` | 成功进入 Action 后是否消费物理输入 |
| `BufferTime` | Pressed 输入缓冲窗口 |
| `HoldTime` | Held 模式要求的按住时间 |
| `AxisDeadzone` | Axis 模式死区 |
| `AxisThreshold` | Axis 模式触发阈值 |
| `ValueScale` | 写入 Input value 黑板前的缩放 |
| `InvertValue` | 写入 Input value 黑板前反向 |

Axis 是否触发按原始强度绝对值与 `max(Deadzone, Threshold)` 比较；缩放和反向作用于成功接受后写入的 `Character.Input.{ActionId}.Value`。

物理输入只在以下条件全部满足后消费：

1. Input 节点已触发。
2. 目标连线条件通过。
3. 目标 Action 的 `CanEnter()` 通过。
4. HFSM 实际完成状态切换。

因此，处于冷却或被高优先级技能阻挡时，不会错误吃掉本次物理输入。

### 5.2 路由编译与候选选择

CharacterGraph 首次使用某张 `CharacterGraphAsset` 时，将 `Input -> Transition -> Action` 编译为缓存。每帧只扫描当前激活的 CharacterGraph scope，并复用 scope 和候选缓冲。

当多个候选同时有效时，按以下顺序比较：

1. Action `Priority`，高者优先。
2. 匹配到 AI 请求时的 request `Priority`，高者优先。
3. Transition `Priority`，高者优先。
4. 子图深度，更深的活动 scope 优先。
5. Action 稳定节点 ID，按字典序确定最终稳定结果。

物理输入候选和 AI 请求候选分别记录来源，AI 请求优先级不会污染其他物理输入候选。

## 6. Action 生命周期

`CharacterSkillChainNodeData` 既是 HFSM Action 状态，也是整条技能链的执行策略来源。

### 6.1 Action 策略

| 字段 | 含义 |
| --- | --- |
| `ActionId` | Action 的稳定逻辑名称 |
| `SkillResourcePaths` | 按顺序执行的技能资源列表 |
| `Priority` | Action 与技能中断优先级 |
| `BlocksMovement` | 执行期间禁止普通水平移动 |
| `BlocksJump` | 执行期间禁止普通跳跃 |
| `CanInterrupt` | 该 Action 是否允许中断已经运行的技能 |

节点通过这些字段构造 `SkillExecutionPolicy`。同一条技能链中的每个 Skill 段都使用同一份 Action 策略；同一个 `SkillResource` 被不同 Action 复用时，可以获得不同策略。

当前示例策略为：

| Action | Priority | 锁移动 | 锁跳跃 | 可中断其他技能 |
| --- | ---: | --- | --- | --- |
| Dash | 100 | 是 | 是 | 是 |
| Attack | 50 | 是 | 是 | 否 |

所以 Dash 可以中断 Attack，而 Attack 不能中断 Dash。

### 6.2 技能链执行

Action 进入时：

1. 清空 `Character.LastSkillCompletionLabel`。
2. 创建该 Action 的链运行时。
3. 使用 Action 的 `SkillExecutionPolicy` 启动列表中的第一个 Skill。
4. 每个 Skill 完成后，根据返回标签决定继续或结束。

结果规则：

- 正常段完成后进入下一个资源。
- `Cancelled` 或 `Interrupted` 结束整条链，Action 结果为 `Cancelled`。
- 无法启动某一段时，Action 结果为 `Failed`。
- 全部段完成后，结果默认为 `Finished`，并保留最后一个返回标签。
- Action 退出时停止仍在运行的当前 Skill，避免时间线泄漏到其他状态。

### 6.3 中断规则

中断判断集中在 `SkillManagerComponent2D`：

```text
requested.Priority < active.Priority  -> 拒绝
requested.CanInterrupt == false       -> 拒绝
requested.Priority >= active.Priority
  且 requested.CanInterrupt == true   -> 停止当前技能并启动新技能
```

`CharacterSkillChainNodeData.CanEnter()` 和实际 `StartSkill()` 使用同一套判断，避免图认为可以进入、运行时却使用另一套规则。

### 6.4 自动返回

CharacterGraph 在第一次从普通状态进入 Action 时记录该 HFSM runtime 的原状态：

- Action 正常完成且存在有效完成连线时，由 HFSM 按完成连线跳转。
- 没有完成连线时，自动回到触发 Action 前的状态。
- 原状态无法恢复时，回到图的默认状态。
- Action 被另一个 Action 中断时，不覆盖最初的返回目标。
- 离开 Action 所属 runtime/subgraph 时，Action 的 `OnExit()` 会停止技能，失效的恢复记录会被清理。

当前 Dash/Attack 没有额外完成连线，因此完成后回到进入前的 `Locomotion`。

## 7. Skill 系统

### 7.1 静态资源与运行时职责

| 类型 | 职责 |
| --- | --- |
| `SkillResource` | 只保存 `SkillId`、`DisplayName`、`Cooldown` 和 `SkillFlowGraphAsset` |
| `SkillExecutionPolicy` | 本次启动使用的优先级、移动锁、跳跃锁和中断能力 |
| `SkillRuntime` | 单个 Skill 在角色身上的运行状态、冷却时间、FlowGraph、返回标签和临时数据 |
| `SkillManagerComponent2D` | 技能索引、Runtime 创建、启动检查、中断、tick、停止及冷却持久化 |

执行策略不保存在 `SkillResource`。这让 Dash 时间线等可复用内容不会绑定某一种角色控制策略。

### 7.2 技能发现和索引

SkillManager 初始化时递归扫描 CharacterGraph：

1. 查找所有 `CharacterSkillChainNodeData.SkillResourcePaths`。
2. 递归扫描 Composite State 的子图。
3. 使用 visited 集合避免子图循环。
4. 按资源路径、资源自身路径和稳定 `SkillId` 缓存资源。
5. 按稳定 `SkillId` 创建/查找 SkillRuntime。

场景不再维护重复的技能数组。非 CharacterGraph 系统若要启动技能，必须先调用 `RegisterSkillPath()`，启动时显式传入策略；无特殊策略时使用 `SkillExecutionPolicy.Default`。

不同资源不能使用相同 `SkillId`，图验证和运行时注册都会报告冲突。同一路径可以在技能链中重复，用于重复段连招。

### 7.3 Skill FlowGraph

Skill FlowGraph 只描述“技能已经启动以后”发生的内容，例如：

- 时间区间和持续时间。
- 动画、速度覆盖、命中或其他动作。
- 条件分支。
- `Finished`、`Cancelled` 等返回标签。

简单技能可以只放一个 `SkillTimelineNodeData`：当图中没有显式 Entry 且只有一个 Timeline 时，该 Timeline 是隐式入口；没有后续连线时流程自动完成。复杂技能仍可使用显式 Entry、Return 和分支。

### 7.4 冷却

冷却属于 SkillRuntime/SkillManager，而不是 Action 或 FlowGraph：

- 启动成功时立即设置 `CooldownReadyTime`。
- `CanStart()` 同时检查资源图、运行状态、冷却和中断规则。
- 保存时将绝对 ready time 转换为 `cooldown_remaining`。
- 恢复时使用当前时间重新计算 ready time。

## 8. 统一移动组件

`CharacterMovementComponent2D` 合并了原 Move、Jump、Gravity、BodyMotor 和 Coordinator 的职责，是角色物理移动的唯一入口。

### 8.1 移动模式

当前模式包括：

| 模式 | 含义 |
| --- | --- |
| `Walking` | Body 当前接触地面 |
| `Falling` | Body 当前未接触地面，包括上升和下落阶段 |
| `Disabled` | 移动组件禁用，普通运动停止 |

`JumpUp`、`InAir`、`IsFalling` 和 `Land` 是 Locomotion HFSM 的语义/动画状态，不是额外的物理 MovementMode。

### 8.2 水平移动

每帧计算：

```text
targetVelocityX = authorizedMoveInputX * MoveSpeed
control = IsOnFloor ? 1 : AirControl
rate = 有输入 ? Acceleration * control : Deceleration
velocityX = MoveToward(velocityX, targetVelocityX, rate * delta)
```

当活动 Skill 策略锁定移动时：

- `RawMoveInputX` 仍保留原始命令，便于观察输入。
- `MoveInputX` 变为 0，普通水平加速停止。
- 技能仍可在同一帧用 velocity override 明确覆盖速度。

### 8.3 跳跃

跳跃由两个容错窗口组成：

- Jump Buffer：提前按下跳跃后，在 `JumpBufferTime` 内落地仍可起跳。
- Coyote Time：离开平台后，在 `CoyoteTime` 内按下跳跃仍可起跳。

满足两者时将 Y 速度设为 `JumpVelocity`。Godot 2D 中负 Y 为向上，因此默认值为负数。

若 `CutJumpOnRelease = true`，玩家在上升阶段释放跳跃后，当前负 Y 速度乘以 `JumpCutMultiplier`，得到短跳效果。

### 8.4 重力和地面

- 空中每帧增加 `Gravity * delta`，并限制到 `MaxFallSpeed`。
- 地面向下速度会归零。
- `FloorSnapLength` 写入 `CharacterBody2D.FloorSnapLength`，增强斜坡和地面吸附。
- `HasGroundAhead()` 使用射线检测前方落脚点，供 AI 边缘转向使用。

### 8.5 技能速度覆盖

Skill FlowGraph 可调用 `RequestVelocityOverride()` 提交本帧覆盖：

| 字段 | 含义 |
| --- | --- |
| `Velocity` | 请求速度 |
| `OverrideHorizontal` | 是否覆盖 X |
| `OverrideVertical` | 是否覆盖 Y |
| `Priority` | 多个本帧覆盖之间的优先级 |

低于已存在覆盖优先级的请求会被忽略；同优先级的后提交请求可以替换前一个。覆盖只持续当前物理帧，Movement 结束后清空，时间线必须在需要覆盖的每一帧持续提交。

当前 Dash 时间线提交水平速度 `760`。

### 8.6 Body 与朝向

- `PhysicsBody` 是实际 `CharacterBody2D`，负责速度和碰撞。
- 初始化和读档后将 Body 同步到 Owner。
- `MoveAndSlide()` 后将 Owner 全局位置同步为 Body 结果，再把 Body 本地位置归零。
- 获得有效水平输入时更新 `Facing`。
- 优先翻转 `VisualRoot.Scale.X`，没有 VisualRoot 时才翻转 Owner。

### 8.7 默认 Movement Profile

`CharacterMovementProfile` 的代码默认值为：

| 参数 | 默认值 |
| --- | ---: |
| MoveSpeed | 280 |
| JumpVelocity | -720 |
| JumpBufferTime | 0.12 s |
| CoyoteTime | 0.10 s |
| Gravity | 1600 |
| MaxFallSpeed | 900 |
| FloorSnapLength | 12 |
| Acceleration | 1800 |
| Deceleration | 2200 |
| AirControl | 0.65 |
| CutJumpOnRelease | true |
| JumpCutMultiplier | 0.45 |

场景可覆盖这些值。当前 Player 的 `MoveSpeed` 为 300、`Gravity` 为 1650；AI 的 `MoveSpeed` 为 220、`Gravity` 为 1650。

## 9. Locomotion 子图

`res://assets/graphs/character_locomotion_hfsm.tres` 是普通 HFSM 子图，当前有 7 个节点和 9 条连线：

```text
Idle <-> Run

local AnyState -> JumpUp
local AnyState -> InAir
local AnyState -> IsFalling
JumpUp        -> InAir
local AnyState -> Land
Land          -> Run
Land          -> Idle
```

局部 AnyState 只存在于 Locomotion 内，用稳定状态 ID 条件限定来源：

- Jump：Idle/Run/Land 中收到 jump start。
- Leave Ground：Idle/Run 离地且 Y 速度尚未进入明显下落。
- Fall：Idle/Run/JumpUp/InAir 离地且 Y 速度大于 120。
- Land：JumpUp/InAir/IsFalling 接地且 Y 速度不小于 0。

Idle/Run 使用原始 `Character.Command.MoveAxisX` 做意图状态切换；空中和落地判断使用 Movement 在上一物理帧发布的最终接地与速度结果。AnyState 不连接 CharacterGraph 的全局 Input 节点。

## 10. Blackboard 所有权

黑板键使用明确前缀区分写入者：

| Key | 类型 | 写入者 | 含义 |
| --- | --- | --- | --- |
| `Character.Command.MoveAxisX` | float | CharacterGraph | Controller 本帧原始水平命令 |
| `Character.Command.JumpStartRequested` | bool | CharacterGraph | 本帧跳跃开始请求 |
| `Character.Command.JumpSustainRequested` | bool | CharacterGraph | 本帧跳跃保持请求 |
| `Character.ActiveActionId` | string | CharacterGraph | 当前最深活动 scope 的 Action ID |
| `Character.LastSkillCompletionLabel` | string | Action | 最近技能链结果标签 |
| `Character.Movement.Mode` | string | Movement | 最终 MovementMode |
| `Character.Movement.IsOnFloor` | bool | Movement | `MoveAndSlide()` 后的接地结果 |
| `Character.Movement.MoveAxisX` | float | Movement | 经过技能锁定后的水平输入 |
| `Character.Movement.VelocityY` | float | Movement | 最终垂直速度 |

Input 节点成功接受后还会按需写入动态键：

```text
Character.Input.{ActionId}.Value
```

Locomotion 子图不重复声明这些黑板键，而是通过共享 blackboard 读取根图值，避免同名局部值遮蔽根值。

## 11. Save V2

`CharacterPersistenceComponent2D` 以 `ISaveSection` 接入 Save V2：

| 项目 | 值 |
| --- | --- |
| SectionKey | `characters` |
| EntryKey | `PersistentIdOverride`，否则使用 `GameObject2D.PersistentId` |
| 角色 section schema | 1 |

### 11.1 保存内容

- 稳定 `persistent_id`。
- 全局位置 X/Y。
- 全局旋转。
- 朝向 `Facing`。
- `PersistentFlags`。
- 每个稳定 `SkillId` 对应的剩余冷却时间。

### 11.2 不保存内容

- 输入快照和 InputModule 消费状态。
- `CharacterCommand2D`、Action 请求和命令缓冲。
- 当前 CharacterGraph/HFSM 节点。
- Action 自动返回目标。
- 当前技能链段、FlowGraph 节点和时间线进度。
- 速度、MovementMode、接地结果、Jump Buffer 和 Coyote Timer。
- 临时移动/跳跃锁和本帧速度覆盖。

读档后的角色从图默认语义状态重新开始，而不是从保存瞬间的执行帧继续。这避免图结构升级、时间线变化或碰撞环境变化导致恢复出非法运行现场。

### 11.3 恢复过程

1. SaveModule 按稳定 EntryKey 找到角色 section。
2. 恢复 Owner 位置和旋转。
3. `SyncBodyToOwner()` 同步 CharacterBody2D，避免 Owner 与碰撞体分离。
4. 恢复朝向和持久化 flags。
5. SkillManager 使用初始化时从角色图建立的 `SkillId` 索引恢复冷却。

图资源和节点 ID 应保持稳定。角色图 schema 变化通过图资源迁移处理，不应依赖保存临时节点 ID 来延续执行现场。

## 12. 新增一个角色 Action

以新增 `HeavyAttack` 为例：

1. 创建 Skill FlowGraph，例如 `res://assets/skills/heavy_attack_flow.tres`。
2. 简单技能放一个 `SkillTimelineNodeData`；复杂技能增加 Entry、分支和 Return。
3. 创建 `SkillResource`，设置稳定且全项目唯一的 `SkillId`、显示名、冷却和 Graph。
4. 在 CharacterGraph 新增 `CharacterSkillChainNodeData`，设置稳定 `ActionId`。
5. 使用节点中的资源文件选择列表添加 SkillResource。列表顺序就是链执行顺序，不手填逗号路径。
6. 在 Action 上配置 Priority、BlocksMovement、BlocksJump 和 CanInterrupt。
7. 新增或复用 `CharacterInputActionNodeData`，配置 InputMap Action、触发模式、层和缓冲。
8. 连接 `Input -> Action`，把地面、空中、黑板等限制配置在 transition condition 上。
9. AI 触发相同行为时调用 `RequestAction("heavy_attack", requestPriority)`，无需新增另一套技能入口。
10. 运行图验证，确认资源存在、类型正确、包含有效 FlowGraph，并且 `SkillId` 不冲突。

SkillManager 会自动扫描并注册新 Action 引用的技能，不需要回到 Player/AI 场景维护技能数组。

技能列表的文件选择器只浏览 `.tres`/`.res`，只接受实际类型为 `SkillResource` 的资源。条目显示 `DisplayName`，Tooltip 显示完整 `res://` 路径，并支持添加、清空、删除、上移和下移；空条目不会写入 GraphJson。

## 13. 常见问题与排查

### Input 有响应但 Action 没进入

依次检查：

- Input 节点的 `ActionName`/`ActionId` 是否匹配。
- `PhysicalInputEnabled` 是否符合角色类型。
- Input -> Action 连线是否存在且可用。
- 连线条件是否读取了正确的 Command 或 Movement 黑板键。
- Action 第一个 Skill 是否仍在冷却。
- 当前活动技能优先级是否更高。
- 新 Action 的 `CanInterrupt` 是否允许中断当前技能。

### Action 进入后角色仍能移动或不能移动

锁定来源是当前运行 `SkillRuntime.ExecutionPolicy`，即 Action 节点策略。不要通过 HFSM 黑板写临时 `MovementLocked`/`JumpLocked`；这些旧键已不再是 Movement 的输入。

### 技能无法从存档恢复冷却

检查 `SkillResource.SkillId` 是否稳定且唯一，以及该技能是否能被 CharacterGraph 递归扫描到，或是否由外部系统调用了 `RegisterSkillPath()`。

### 图连接过多

- Input 节点直接连接 Action，不需要 AnyState 中转。
- 公共跑跳转换只放在 Locomotion 的局部 AnyState。
- Action 无特殊完成分支时不画返回线，使用自动返回。
- 简单 Skill FlowGraph 使用单 Timeline 隐式入口。

## 14. 验证与关键文件

角色运行时 smoke test：

```powershell
dotnet build yu.csproj --no-restore
godot --headless --path . res://assets/scenes/character_graph_runtime_smoke.tscn
```

`CharacterGraphRuntimeSmokeTest` 当前覆盖：角色图启动、9 条 Locomotion 连线、Attack/Dash 中断规则、技能策略锁定、Dash 同帧速度、Action 自动返回、Save V2 临时状态排除、按 SkillId 恢复冷却，以及 AI 禁用物理输入。

关键实现文件：

- `scripts/gamelogic/abilities/core/CharacterGraphComponent2D.cs`
- `scripts/gamelogic/abilities/core/CharacterCommandBufferComponent2D.cs`
- `scripts/gamelogic/abilities/core/CharacterGraphBlackboardKeys.cs`
- `scripts/gamelogic/abilities/movement/CharacterMovementComponent2D.cs`
- `scripts/gamelogic/abilities/movement/CharacterMovementProfile.cs`
- `scripts/gamelogic/hfsm/graph/nodes/CharacterInputActionNodeData.cs`
- `scripts/gamelogic/hfsm/graph/nodes/CharacterSkillChainNodeData.cs`
- `scripts/gamelogic/skills/runtime/SkillManagerComponent2D.cs`
- `scripts/gamelogic/skills/runtime/SkillRuntime.cs`
- `scripts/gamelogic/saves/CharacterPersistenceComponent2D.cs`
- `scripts/test/CharacterGraphRuntimeSmokeTest.cs`

关键资源：

- `assets/graphs/character_graph.tres`
- `assets/graphs/character_locomotion_hfsm.tres`
- `assets/skills/dash_skill.tres`
- `assets/skills/attack_skill.tres`
- `assets/scenes/player.tscn`
- `assets/scenes/ai_runner.tscn`
