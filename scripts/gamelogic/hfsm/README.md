# GameLogic HFSM 与角色图

GameLogic HFSM 基于 GraphPlugin `StateGraph`，负责状态切换、条件、子图和黑板。角色行为使用 `CharacterGraphAsset` 扩展：输入节点是全局触发器，Action 节点仍是 HFSM 状态。

## 角色图结构

```text
Dash Input   -> Dash Action
Attack Input -> Attack Action

Locomotion (Composite State)
```

- `CharacterInputActionNodeData` 没有输入端口，不属于状态，也不从 Any State 连出。
- Input 节点只输出 `Triggered`，可以通过多条条件连线复用到多个 Action。
- `CharacterSkillChainNodeData` 是实际 Action 状态，负责技能链、优先级、中断和移动锁定。
- Action 完成且没有有效完成连线时，自动恢复触发前的根状态；恢复目标失效时回到默认状态。
- 玩家输入和 AI `CharacterActionRequest` 最终走同一条 Action 路由。

默认资源：

```text
res://assets/graphs/character_graph.tres
res://assets/graphs/character_locomotion_hfsm.tres
```

## 运行时组件

- `HfsmComponent2D`：通用 HFSM 组件。
- `HfsmRuntime`：状态图运行时，注入 `GameObject2D` 和组件上下文。
- `CharacterGraphComponent2D`：扫描全局 Input、路由 Action、处理中断与自动返回。
- `CharacterCommandBufferComponent2D`：合并移动命令并缓冲逻辑 Action 请求。
- `SkillManagerComponent2D`：更新技能 FlowGraph 和耐久 cooldown。
- `CharacterMovementComponent2D`：统一执行移动、跳跃、重力、地面检测和 `MoveAndSlide()`。

物理帧优先级顺序：

```text
Controller / AI -> CharacterGraph -> SkillManager -> CharacterMovement
```

## Input 节点

Input 节点保存逻辑 Action 名称，不保存物理按键。物理绑定继续由 Godot `InputMap` 和 `InputModule` 管理。

支持：

- Pressed / Released / Held / Axis
- 输入层和成功后的输入消费
- BufferTime、HoldTime
- Axis deadzone、threshold、scale 和 invert
- 黑板条件与连线条件
- 独立 `ActionId`，供 AI 提交 `CharacterActionRequest`

输入只会在 Action 成功进入后消费。

## Action 与技能链

`CharacterSkillChainNodeData.SkillResourcePaths` 通过资源选择器维护，不接受手填路径。列表顺序就是执行顺序，允许重复资源。

Action 进入前会检查首个技能 cooldown；执行中可按 Action 优先级切换。当前默认关系为：

```text
Dash (100) > Attack (50)
```

Dash 可以打断 Attack，Attack 不能打断 Dash。离开 Action 或其所属子图会取消仍在运行的技能。

## 状态条件

StateGraph/HFSM 不再提供 Tag。语义由明确状态和条件表达：

- grounded / airborne：`MovementMode` 或 `IsOnFloor`
- moving：移动输入或速度
- dashing / attacking：`Character.ActiveActionId`
- movement / jump lock：Action 或 `SkillResource` 策略
- Any State 排除：状态名称或稳定状态 ID

可用条件包括 bool、float、string、trigger、timer、当前状态身份，以及 Character 条件组合器。

## 子图

`HfsmCompositeStateNodeData` 继续负责子图。角色图子图通过共享黑板、输入参数和完成标签通信。父状态退出时，子图运行时会停止，活动 Action 也会收到退出并取消技能。

## Skill FlowGraph

简单技能可只放一个 `SkillTimelineNodeData`：没有 Entry 时它作为隐式入口，没有后续连线时运行时自动完成为 `Finished`。复杂技能仍可使用显式 Entry、Return、条件和分支。

## 调试

`HfsmComponent2D` 提供：

- `LogStateChanges`
- `DebugStateLabelPath`
- `CurrentStateName`
- `CurrentStatePath`

状态图运行时数据只存在于组件实例，不会写回共享图资源。

## 存档边界

Save V2 保存角色稳定 ID、位置、旋转、朝向、持久化标记和技能 cooldown。输入快照、命令缓冲、当前 Action、速度、技能时间线和图运行时节点均不保存；加载后从图默认语义状态重新开始。
