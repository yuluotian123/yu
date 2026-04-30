# GameLogic HFSM

GameLogic HFSM 是基于 GraphPlugin `StateGraph` 的角色状态机封装。GraphPlugin 提供通用状态图能力；HFSM 层负责接入 `GameObject2D`、组件、技能和角色 blackboard。

## 文件结构

- `runtime`
  - `HfsmComponent2D`：挂在角色上的状态机组件。
  - `HfsmRuntime`：继承 `StateGraphRuntime`，注入 owner 和 GameLogic 查询能力。
  - `HfsmRuntimeExtensions`：GameLogic 上下文辅助方法。
  - `IHfsmStateHandler`：组件状态回调兼容接口。
- `graph/core`
  - `HfsmGraphAsset`
  - `HfsmTransitionConnection`
- `graph/nodes`
  - `HfsmStateNodeData`
  - `ComponentHfsmStateNodeData`
  - `HfsmCompositeStateNodeData`
  - `HfsmAnyStateNodeData`
  - `HfsmReturnStateNodeData`
  - `SkillHfsmStateNodeData`
- `graph/conditions`
  - `HfsmConditionBase`
  - `HfsmAlwaysCondition`
  - `HfsmTriggerCondition`
  - `HfsmBoolCondition`
  - `HfsmFloatCondition`
  - `HfsmTimerCondition`
- `graph/tags`
  - tag registry 和 tag 下拉 UI。

## 核心职责

HFSM 负责：

- 根据 blackboard 和 transition 条件切换角色状态。
- 暴露语义 tag，例如 `grounded`、`airborne`、`dashing`、`attacking`。
- 管理复合状态子图。
- 通过 `SkillHfsmStateNodeData` 启动技能。
- 兼容 `ComponentHfsmStateNodeData` 这种组件生命周期状态。

HFSM 不负责：

- 具体移动物理。
- 具体技能 timeline。
- 技能 cooldown 存储。
- 命中检测和伤害结算。

这些逻辑应该放在 movement、skill、combat 等业务组件中。

## 运行时上下文

`HfsmRuntime` 创建时会把这些对象加入 `GraphExecutionContext.UserData`：

- 当前 `HfsmRuntime`
- 当前 `HfsmComponent2D`
- 当前 `GameObject2D`

所以节点、condition、action 可以通过：

```csharp
var hfsm = context.GetUserData<HfsmRuntime>();
var owner = context.GetUserData<GameObject2D>();
```

读取业务上下文。

## 状态切换顺序

HFSM 复用 `StateGraphRuntime` 的顺序：

1. 先检查 Any State transition，用于高优先级打断。
2. 调用当前状态 `OnUpdate()`。
3. 如果当前状态返回 completion，只检查 completion 指定输出口的 transition。
4. 如果没有 completion 推进，再检查当前状态普通 transition。

目标状态切换前会先调用：

```csharp
targetState.CanEnter(runtime)
```

`SkillHfsmStateNodeData` 用这个入口判断技能 cooldown 是否允许进入。

## Completion-Only Transition

`HfsmTransitionConnection` 继承自 `StateTransitionConnection`，支持 `CompletionOnly`。

`CompletionOnly = true` 的连接只会在当前状态返回 `NodeCompletion` 时检查。

典型用法：

```text
Dash -- Completed --> Locomotion
Attack -- Completed --> Locomotion
```

这类连接不要再写技能完成类黑板条件。

## Skill 状态

Dash 和 Attack 已迁移为 Skill：

- `res://assets/skills/dash_skill.tres`
- `res://assets/skills/attack_skill.tres`

根图中的状态：

- `Dash`：`SkillHfsmStateNodeData`，`SkillResourcePath = res://assets/skills/dash_skill.tres`
- `Attack`：`SkillHfsmStateNodeData`，`SkillResourcePath = res://assets/skills/attack_skill.tres`

`SkillHfsmStateNodeData` 的职责很薄：

- `CanEnter()`：通过 `SkillManagerComponent2D.CanStart()` 判断技能是否可进入。
- `OnEnter()`：通过 `SkillManagerComponent2D.StartSkill()` 启动技能。
- `TryGetCompletion()`：当 `SkillRuntime` 完成时返回 `NodeCompletion.Completed(label)`。
- `OnExit()`：停止正在运行的技能。

技能 cooldown 和 FlowGraph tick 都由 `SkillManagerComponent2D` 管理。

## Player 示例图

根图：

```text
res://assets/graphs/character_ground_air_hfsm.tres
```

根图状态：

- `Any State`
- `Locomotion`
- `Dash`
- `Attack`

根图 transition：

- `Any State -> Dash`：`DashStartRequested == true`
- `Any State -> Attack`：`AttackStartRequested == true`
- `Dash -> Locomotion`：completion-only
- `Attack -> Locomotion`：completion-only

Locomotion 子图：

```text
res://assets/graphs/character_locomotion_hfsm.tres
```

状态：

- `Grounded`
- `Airborne`

## 角色 Blackboard Key

定义位置：

```text
scripts/gamelogic/player/hfsm/CharacterHfsmBlackboardKeys.cs
```

当前 key：

- `IsOnFloor`
- `JumpStartRequested`
- `JumpSustainRequested`
- `MoveAxisX`
- `VelocityY`
- `DashStartRequested`
- `AttackStartRequested`

Controller 只写输入类 key。技能运行状态、return label 和 cooldown 不写回 HFSM blackboard。

## Controller 数据流

Player controller 每帧：

1. 读取输入。
2. 写移动和跳跃 intent 给 movement/jump 组件。
3. 写 HFSM blackboard：
   - 落地状态
   - 跳跃请求
   - 移动轴
   - Y 速度
   - Dash/Attack 开始请求
4. 不再查询 Dash/Attack 旧组件，也不负责判断技能 cooldown。

AI controller 也只写 movement/jump 和技能请求 key。

## 组件状态节点

`ComponentHfsmStateNodeData` 仍保留，用于需要把状态生命周期转发给某个组件的场景。

组件实现：

```csharp
public class MyStateComponent : Component2D, IHfsmStateHandler
{
    public void OnHfsmStateEnter(HfsmRuntime runtime, IHfsmStateNodeData state) {}
    public void OnHfsmStateUpdate(HfsmRuntime runtime, IHfsmStateNodeData state, double delta) {}
    public void OnHfsmStateExit(HfsmRuntime runtime, IHfsmStateNodeData state) {}
}
```

图中使用 `ComponentHfsmStateNodeData`，把 `ComponentTypeName` 填为组件类型名。

## 调试

`HfsmComponent2D` 提供：

- `LogStateChanges`
- `DebugStateLabelPath`
- `IncludeTagsInDebugText`

示例输出：

```text
HFSM: Locomotion/Grounded [grounded]
HFSM: Dash [dashing]
```

## 创建 HFSM 图

1. 创建 `HfsmGraphAsset`。
2. 添加状态节点、Any State、Return、Composite 或 Skill 节点。
3. 设置初始状态：`InitialStateName` 或节点 `IsDefault`。
4. 添加 transition，并配置条件和优先级。
5. 需要自动返回的状态使用 completion-only transition。

## 注意事项

- Any State 会先于当前状态 update 检查，适合打断。
- completion-only transition 不参与普通 transition 检查。
- 技能节点能否进入由 `SkillManagerComponent2D` 判断。
- 如果角色要使用 Skill 状态，场景中必须挂 `SkillManagerComponent2D`。
- HFSM 图资源是共享配置，运行时状态和 blackboard 值不会写回资源。
