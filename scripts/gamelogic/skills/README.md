# GameLogic Skills

Skill 系统把角色技能拆成资源、运行时、管理组件和 FlowGraph。

当前 Dash 和 Attack 已从旧组件迁移为 Skill：

- `res://assets/skills/dash_skill.tres`
- `res://assets/skills/dash_flow.tres`
- `res://assets/skills/attack_skill.tres`
- `res://assets/skills/attack_flow.tres`

## 文件结构

- `SkillResource.cs`：技能静态资源。
- `SkillFlowGraphAsset.cs`：技能 FlowGraph 资源类型。
- `runtime/SkillRuntime.cs`：单个技能在某个角色身上的运行时实例。
- `runtime/SkillManagerComponent2D.cs`：角色技能管理组件。
- `actions/`：技能 FlowGraph 可用 action。

## 设计边界

Skill 系统负责：

- 技能资源加载。
- 技能 cooldown。
- 技能 runtime 生命周期。
- 启动和 tick 技能 FlowGraph。
- 给 FlowGraph 注入 owner、HFSM、SkillRuntime、SkillResource 等上下文。

Skill FlowGraph 负责：

- timeline。
- 位移、表现、命中窗口等 action。
- Return label。

Skill FlowGraph 不负责：

- 判断 cooldown 是否可用。
- 启动 cooldown。
- 决定 HFSM 是否能进入技能状态。

这些都由 `SkillManagerComponent2D` 和 `SkillHfsmStateNodeData` 处理。

## SkillResource

字段：

- `SkillId`
- `DisplayName`
- `Cooldown`
- `Graph`

示例：

```text
res://assets/skills/dash_skill.tres
```

引用：

```text
SkillId = "dash"
Cooldown = 0.45
Graph = res://assets/skills/dash_flow.tres
```

## SkillManagerComponent2D

`SkillManagerComponent2D` 挂在角色 `GameObject2D.Components` 上。

职责：

- 注册 `Skills` 数组里的技能资源。
- 按 path 或 `SkillId` 缓存技能。
- 判断 cooldown。
- 启动 `SkillRuntime`。
- 每帧 tick active skills。
- 停止技能。

优先级：

```csharp
ComponentPriority.Motor + 1
```

这样技能 timeline 会在 movement/gravity 之后、motor 之前运行，Dash 写入的速度不会被移动组件覆盖。

当前 player 场景已经挂了：

```text
SkillManagerComponent2D
Skills = [dash_skill, attack_skill]
```

## SkillRuntime

`SkillRuntime` 表示某个角色身上的一个技能实例。

保存：

- `SkillResource Resource`
- `FlowGraphRuntime FlowRuntime`
- `CooldownReadyTime`
- `IsRunning`
- `IsCompleted`
- `LastReturnLabel`
- runtime data 字典

生命周期：

1. `CanStart(now)` 检查 cooldown 和 graph。
2. `Start(hfsmRuntime, now)` 创建 FlowGraphRuntime，并写入 cooldown。
3. `Update(delta)` tick FlowGraph。
4. FlowGraph Return 或无 active node 时标记完成。
5. `Stop()` 停止 FlowGraph，触发 Cancel action。

## FlowGraph UserData

技能 FlowGraph 启动时会注入：

- `SkillManagerComponent2D`
- `SkillRuntime`
- `SkillResource`
- `HfsmRuntime`
- `HfsmComponent2D`
- `GameObject2D`

Action 中可以这样取：

```csharp
var skill = context.GetUserData<SkillRuntime>();
var owner = context.GetUserData<GameObject2D>();
var hfsm = context.GetUserData<HfsmRuntime>();
```

Timeline action 还可以取：

```csharp
var timeline = context.GetUserData<FlowTimelineContext>();
float progress = timeline.NormalizedTime;
```

## 内置 Skill Actions

当前 action 定义在：

```text
scripts/gamelogic/skills/actions/
```

### SkillApplyDashVelocityAction

直接解析 dash 方向并写入 `CharacterBodyMotorComponent2D.Velocity`。

方向来源优先级：

- `CharacterMoveComponent2D.ApprovedIntent`
- `CharacterMoveComponent2D.RawIntent`
- HFSM blackboard `MoveAxisX`
- `CharacterMoveComponent2D.InputX`
- `CharacterMoveComponent2D.Facing`

常用参数：

- `Speed`
- `StopVerticalVelocity`
- `MoveAxisBlackboardKey`

### SkillSetVisualModulateAction

修改或恢复 `VisualRoot` 的颜色。

Dash 用它实现冲刺时的闪色表现。

### SkillSlashVisualAction

Attack 用的斩击表现 action。

模式：

- `Show`
- `Update`
- `Hide`

`Update` 会读取 `FlowTimelineContext.NormalizedTime` 调整斩击透明度和缩放。

### SkillCameraShakeAction

读取 `CameraShakeProfile` 资源，并直接调用角色身上的 `PlayerCameraComponent2D.Shake()`。

常用资源：

- `res://assets/camera_shakes/light_hit.tres`
- `res://assets/camera_shakes/heavy_hit.tres`
- `res://assets/camera_shakes/dash_burst.tres`

常用参数在 `CameraShakeProfile` 中配置：

- `Duration`
- `Amplitude`
- `Frequency`
- `RotationAmplitudeDegrees`
- `DecayPower`

## Dash Skill Flow

资源：

```text
res://assets/skills/dash_flow.tres
```

结构：

```text
Entry
  -> Timeline 0.16s
      Start:
        CameraShake(dash_burst)
        SetVisualModulate(true)
      Update:
        ApplyDashVelocity
      Complete:
        SetVisualModulate(false)
      Cancel:
        SetVisualModulate(false)
  -> Return Finished
```

## Attack Skill Flow

资源：

```text
res://assets/skills/attack_flow.tres
```

结构：

```text
Entry
  -> Timeline 0.22s
      Start:
        CameraShake(light_hit)
        Slash Show
      Update:
        Slash Update
      Complete:
        Slash Hide
      Cancel:
        Slash Hide
  -> Return Finished
```

## HFSM 接入

HFSM 图中使用 `SkillHfsmStateNodeData`：

```text
Dash:
  SkillResourcePath = res://assets/skills/dash_skill.tres

Attack:
  SkillResourcePath = res://assets/skills/attack_skill.tres
```

入口 transition 只判断输入请求：

```text
Any State -> Dash: DashStartRequested == true
Any State -> Attack: AttackStartRequested == true
```

技能节点 `CanEnter()` 会调用 manager 判断 cooldown。

返回 transition 用 completion-only：

```text
Dash -> Locomotion: CompletionOnly
Attack -> Locomotion: CompletionOnly
```

## 新增一个技能

推荐步骤：

1. 创建一个 `SkillFlowGraphAsset`。
2. 用通用 Flow 节点描述技能流程：Entry、Timeline、Action、Condition、Return。
3. 如有业务行为，新增 `GraphActionBase` 子类。
4. 创建 `SkillResource`，填写 `SkillId`、`DisplayName`、`Cooldown`、`Graph`。
5. 把 `SkillResource` 加到角色的 `SkillManagerComponent2D.Skills`。
6. 在 HFSM 图中添加 `SkillHfsmStateNodeData`，填写 `SkillResourcePath`。
7. 添加入口 transition 和 completion-only 返回 transition。

## 注意事项

- 不要在 Skill FlowGraph 里做 cooldown ready condition。
- 不要在 Skill FlowGraph 里做 start cooldown action。
- cooldown 属于 `SkillManagerComponent2D`。
- FlowGraph action 应该只做“技能已经开始后”的行为。
- 如果技能需要在 movement 之后覆盖速度，让它通过 `SkillManagerComponent2D` tick，而不是在 HFSM state update 里直接 tick。
