# GameLogic HFSM 使用说明

`scripts/gamelogic/hfsm` 是基于 GraphPlugin 的层级有限状态机实现。GraphPlugin 负责图资源、编辑器、连线和黑板；GameLogic HFSM 负责在运行时解释这些数据并切换状态。

## 当前定位

HFSM 主要负责三件事：

- 根据黑板和 transition 条件切换状态。
- 暴露当前状态语义，例如 `grounded`、`airborne`、`dashing`、`attacking`。
- 在进入、更新、退出某些状态时，把状态生命周期转发给当前 GameObject 上的组件。

移动、跳跃、冲刺、攻击等实际行为仍然放在各自的 `Component2D` 里。HFSM 不直接拖场景组件引用，也不做一套额外的 StateBehaviour 系统。

## 核心类型

- `HfsmGraphAsset`：HFSM 图资源，保存状态节点、transition 连线和 Local Blackboard。
- `HfsmComponent2D`：通用运行时组件，挂在 `GameObject2D.Components` 上并启动一张 `HfsmGraphAsset`。
- `HfsmRuntime`：每个 `HfsmComponent2D` 自己的运行时实例，保存当前状态、触发器、状态时间、黑板运行时数据和子图 runtime。
- `HfsmStateNodeData`：普通状态节点。
- `ComponentHfsmStateNodeData`：通用组件状态节点，进入、更新、退出时查找当前 GameObject 上的组件并调用 `IHfsmStateHandler`。
- `HfsmCompositeStateNodeData`：复合状态节点，引用另一张 `HfsmGraphAsset` 作为子图。
- `HfsmAnyStateNodeData`：Any State 伪节点，不会被进入，输出 transition 会在每帧参与检查。
- `HfsmReturnStateNodeData`：Return 伪节点，进入后会立即解析到它输出端连接的真实状态。
- `HfsmTransitionConnection`：状态 transition，支持优先级和条件列表。
- `IHfsmStateHandler`：组件实现的状态生命周期接口。

## 组件状态节点

新增能力时，优先让能力组件实现 `IHfsmStateHandler`，然后在图中使用 `ComponentHfsmStateNodeData`，把 `ComponentTypeName` 设为组件类名。

```csharp
public partial class CharacterDashComponent2D : Component2D, IHfsmStateHandler
{
    public void OnHfsmStateEnter(HfsmRuntime runtime, IHfsmStateNodeData state)
    {
        TryStartDash(RawIntent.DirectionX);
    }

    public void OnHfsmStateUpdate(HfsmRuntime runtime, IHfsmStateNodeData state, double delta)
    {
    }

    public void OnHfsmStateExit(HfsmRuntime runtime, IHfsmStateNodeData state)
    {
        if (IsDashing)
            CancelDash();
    }
}
```

`ComponentHfsmStateNodeData` 会从 `runtime.Owner.Owner`，也就是当前 `HfsmComponent2D` 所属的 `GameObject2D` 上查找组件。这样同一张图可以给 player 和 AI 共用，但每个角色调用的是自己身上的组件实例。

当前示例：

- `Dash` 使用 `ComponentHfsmStateNodeData`，`ComponentTypeName = CharacterDashComponent2D`。
- `Attack` 使用 `ComponentHfsmStateNodeData`，`ComponentTypeName = CharacterAttackComponent2D`。

## Player / AI 示例图

根图：

```text
res://assets/graphs/character_ground_air_hfsm.tres
```

根图只表达高层模式：

- `Any State`：从任意非动作状态进入高优先级动作。
- `Locomotion`：复合状态，子图为 `res://assets/graphs/character_locomotion_hfsm.tres`。
- `Dash`：冲刺状态，tag 为 `dashing`。
- `Attack`：攻击状态，tag 为 `attacking`。

Locomotion 子图：

```text
res://assets/graphs/character_locomotion_hfsm.tres
```

- `Grounded`：tag 为 `grounded`。
- `Airborne`：tag 为 `airborne`。

根图 transition：

- `Any State -> Dash`：`DashStartRequested == true`。
- `Any State -> Attack`：`AttackStartRequested == true`。
- `Dash -> Locomotion`：`DashFinished == true`。
- `Attack -> Locomotion`：`AttackFinished == true`。

Locomotion transition：

- `Grounded -> Airborne`：`IsOnFloor == false` 或 `JumpStartRequested == true`。
- `Airborne -> Grounded`：`IsOnFloor == true`。

## 数据流

1. `PlayerCharacterControllerComponent2D` 或 `SimpleAICharacterControllerComponent2D` 读取输入或 AI 决策。
2. Controller 写入各能力组件的 raw intent，并把输入、速度、落地状态写入 HFSM 黑板。
3. `HfsmComponent2D` 更新 `HfsmRuntime`，根据图里的条件切换状态。
4. 进入 `Dash` 或 `Attack` 时，`ComponentHfsmStateNodeData` 找到对应组件并调用 `IHfsmStateHandler.OnHfsmStateEnter()`。
5. 能力组件执行真实行为，并把 `DashFinished`、`AttackFinished` 等输出写回黑板。

组件优先级保证了 controller 先写黑板，HFSM 再切换状态，能力组件最后执行实际行为：

```text
Input -> State -> Combat -> Movement -> Physics -> Motor
```

## 黑板 Key

角色示例使用的 key 定义在 `scripts/gamelogic/player/hfsm/CharacterHfsmBlackboardKeys.cs`：

- `IsOnFloor`
- `JumpStartRequested`
- `JumpSustainRequested`
- `MoveAxisX`
- `VelocityY`
- `DashStartRequested`
- `DashActive`
- `DashFinished`
- `AttackStartRequested`
- `AttackActive`
- `AttackFinished`

Controller 通常写输入类 key，能力组件通常写 active / finished 类 key。

## 调试当前状态

`HfsmComponent2D` 提供调试选项：

- `LogStateChanges`：启动和切换状态时打印当前状态。
- `DebugStateLabelPath`：指向场景里的 `Label`，运行时显示当前状态路径。
- `IncludeTagsInDebugText`：是否在调试文本里显示当前 tag。

示例显示：

```text
HFSM: Locomotion/Grounded [grounded]
HFSM: Dash [dashing]
```

## 创建 HFSM 图

1. 在 Godot 中创建 `HfsmGraphAsset` 资源。
2. 打开 Graph 编辑器。
3. 添加 `HfsmStateNodeData`、`ComponentHfsmStateNodeData`、`HfsmCompositeStateNodeData`、`HfsmAnyStateNodeData` 或 `HfsmReturnStateNodeData`。
4. 给一个状态勾选 `Default`，或在 `HfsmGraphAsset.InitialStateName` 填入初始状态名。
5. 从状态输出端连接到另一个状态输入端，右键连线编辑 transition 条件。

`HfsmGraphAsset` 是共享配置资源。多个角色可以引用同一张图；每个角色会创建自己的 `HfsmRuntime`，运行时状态和黑板值不会写回图资源。

## Transition 条件

内置条件：

- `HfsmAlwaysCondition`：永远满足。
- `HfsmTriggerCondition`：调用 `runtime.Trigger("Name")` 后，本帧满足。
- `HfsmBoolCondition`：读取黑板 bool key 并比较。
- `HfsmFloatCondition`：读取黑板 float / int key 并比较。
- `HfsmTimerCondition`：当前状态进入后经过指定秒数。

黑板条件使用 `GraphBlackboardKeyReference`。编辑器会从当前图 Local Blackboard、父图 Local Blackboard 和场景里的 Global Blackboard 收集候选 key，并按类型过滤。

## Tag Registry

全局 tag 注册资源：

```text
res://assets/config/hfsm_tag_registry.tres
```

在 Godot Inspector 中直接编辑 `Tags` 数组即可。状态节点保存的是逗号分隔的 tag 字符串，但编辑器会优先从全局 registry 生成下拉和多选菜单。

运行时查询：

```csharp
var hfsm = Owner.GetComponent<HfsmComponent2D>();
if (hfsm.CurrentStateHasTag("dashing"))
{
    // 当前处于冲刺语义状态
}
```

## 复合状态

`HfsmCompositeStateNodeData` 继承 GraphPlugin 的 `SubGraphNodeData`。进入复合状态时，runtime 会加载 `SubGraphPath` 指向的子 `HfsmGraphAsset`，并创建子 `HfsmRuntime`。

子 runtime 会继承父 runtime 的黑板层，再 `PushLocal(subGraph)`。读取顺序是：

1. 子图 Local Blackboard。
2. 父图 Local Blackboard。
3. 场景里的 Global Blackboard。

`CurrentStatePath` 会返回类似 `Locomotion/Grounded` 的路径。
