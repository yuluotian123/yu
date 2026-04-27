# GameLogic HFSM 使用说明

`scripts/gamelogic/hfsm` 是基于 GraphPlugin 的层级有限状态机实现。GraphPlugin 负责图资源、编辑器、连线和黑板；GameLogic HFSM 负责解释这些数据，并在运行时切换状态。

## 核心类型

- `HfsmGraphAsset`：HFSM 图资源，保存状态节点、过渡连线和 Local Blackboard。
- `HfsmStateNodeData`：普通状态节点。
- `HfsmCompositeStateNodeData`：复合状态节点，继承 GraphPlugin 的 `SubGraphNodeData`，可引用子 `HfsmGraphAsset`。
- `HfsmTransitionConnection`：状态过渡，支持优先级和条件列表。
- `HfsmRuntime`：每个运行实例自己的状态机运行时。
- `HfsmComponent2D`：通用组件包装，可直接挂到 `GameObject2D.Components`。
- `HfsmTagRegistry`：全局 tag 注册表资源，默认路径是 `res://assets/config/hfsm_tag_registry.tres`。

## 全局 Tag 注册

状态 tag 不在单张图里注册，而是使用全局资源：

```text
res://assets/config/hfsm_tag_registry.tres
```

在 Godot Inspector 中打开这个资源，直接编辑 `Tags` 数组即可。每个元素是一个 `HfsmTagDefinition` 子资源，展开后可以填写：

每个 tag 包含：

- `Key`：代码和图里保存的稳定 key，例如 `grounded`、`airborne`、`can_move`。
- `DisplayName`：编辑器显示名。
- `Layer`：可选。相同 layer 下的 tag 互斥，例如 `Locomotion` 里只能选择 `grounded` 或 `airborne` 之一。
- `Description`：说明。
- `DisplayOrder`：同组显示排序。

状态节点仍然把已选择 tag 保存为逗号分隔字符串，旧图资源可以继续使用；但编辑器会优先从全局注册表生成 layer 下拉和普通 tag 勾选项。

运行时查询：

```csharp
if (runtime.CurrentStateHasTag("airborne"))
{
    // 当前状态属于空中语义
}
```

组件查询：

```csharp
var fsm = Owner.GetComponent<CharacterFSMComponent2D>();
if (fsm.CurrentStateHasTag("grounded"))
{
    // 当前状态属于地面语义
}
```

## 创建 HFSM 图

1. 在 Godot 中创建 `HfsmGraphAsset` 资源。
2. 打开 Graph 编辑器。
3. 右键添加 `HfsmStateNodeData` 或 `HfsmCompositeStateNodeData`。
4. 给一个状态勾选 `Default`，或在 `HfsmGraphAsset.InitialStateName` 填入初始状态名。
5. 从状态输出端口连到另一个状态输入端口，右键连线编辑过渡条件。

`HfsmGraphAsset` 是共享配置资源。很多角色或怪物可以引用同一张图；每个角色只创建自己的 `HfsmRuntime`。当前状态、触发器、状态时间和黑板运行时值都保存在 runtime 中，不会写回图资源。

## 过渡条件

内置条件：

- `HfsmAlwaysCondition`：永远满足。
- `HfsmTriggerCondition`：调用 `runtime.Trigger("Name")` 后，在本帧满足。
- `HfsmBoolCondition`：读取黑板 bool key 并比较。
- `HfsmFloatCondition`：读取黑板 float/int key 并比较。
- `HfsmTimerCondition`：当前状态进入后经过指定秒数。

黑板条件使用 `GraphBlackboardKeyReference`。编辑器会从当前图 Local Blackboard、父图 Local Blackboard 和 Global Blackboard 收集候选 key，并按类型过滤。

## Player / AI 共用角色状态图

示例图资源：

```text
assets/graphs/character_ground_air_hfsm.tres
```

这张图包含两个状态：

- `Grounded`：tag 为 `movement,grounded`。
- `Airborne`：tag 为 `movement,airborne`。

Local Blackboard 定义：

- `IsOnFloor`：由 `CharacterBodyMotorComponent2D.IsOnFloor` 写入。
- `JumpStartRequested`：由玩家输入或 AI 输入写入。
- `JumpSustainRequested`：跳跃按住状态。
- `MoveAxisX`：水平移动输入。
- `VelocityY`：当前纵向速度。

过渡规则：

- `Grounded -> Airborne`：`IsOnFloor == false` 或 `JumpStartRequested == true`。
- `Airborne -> Grounded`：`IsOnFloor == true`。

Player 场景和 AI 场景都在 `CharacterFSMComponent2D.StateGraph` 上引用同一张图：

```text
res://assets/graphs/character_ground_air_hfsm.tres
```

数据流：

1. `PlayerCharacterControllerComponent2D` 或 `SimpleAICharacterControllerComponent2D` 写入 raw intent。
2. `CharacterFSMComponent2D` 把 motor 状态和 raw intent 写入 HFSM 黑板。
3. `HfsmRuntime.Update()` 根据图里的条件切换状态。
4. `CharacterFSMComponent2D` 继续批准 move/jump intent。
5. `CharacterMoveComponent2D`、`CharacterJumpComponent2D`、`CharacterGravityComponent2D` 执行实际运动。

## 状态节点读取黑板

`HfsmStateNodeData.OnEnter()` 默认会调用 `Execute(runtime.Context)`，所以自定义状态节点可以通过 `GraphExecutionContext` 读写黑板：

```csharp
public class PrintSpeedStateNode : HfsmStateNodeData
{
    public override void Execute(GraphExecutionContext context)
    {
        float moveAxis = context.Blackboard.GetValue("MoveAxisX", 0f);
        GD.Print($"Move axis: {moveAxis}");
    }
}
```

如果需要每帧逻辑，可以继承 `HfsmStateNodeData` 并覆盖：

```csharp
public override void OnUpdate(HfsmRuntime runtime, double delta)
{
    bool grounded = runtime.GetValue("IsOnFloor", false);
}
```

## 复合状态

`HfsmCompositeStateNodeData` 继承 GraphPlugin 的 `SubGraphNodeData`，所以它复用通用子图能力。进入复合状态时，runtime 会加载 `SubGraphPath` 指向的子 `HfsmGraphAsset`，并创建子 `HfsmRuntime`。

子 runtime 会继承父 runtime 的黑板层，再 `PushLocal(subGraph)`，读取顺序是：

1. 子图 Local Blackboard。
2. 父图 Local Blackboard。
3. 场景里的 Global Blackboard。

父状态退出时，子 runtime 会停止。`CurrentStatePath` 会返回类似 `Combat/Chase` 的路径。
