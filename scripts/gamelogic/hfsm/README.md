# GameLogic HFSM 使用说明

`scripts/gamelogic/hfsm` 提供了一套基于 GraphPlugin 的层级有限状态机。它不替换 `scripts/framework/fsm`，而是在 GameLogic 层新增一套可用图编辑器编辑的 HFSM。

## 核心类型

- `HfsmGraphAsset`：HFSM 图资源，保存状态节点、过渡连线和 Local Blackboard。
- `HfsmStateNodeData`：普通状态节点，继承 `GraphNodeData`。
- `HfsmCompositeStateNodeData`：复合状态节点，继承 GraphPlugin 的 `SubGraphNodeData`，可通过 `SubGraphPath` 引用一个子 `HfsmGraphAsset`。
- `IHfsmStateNodeData`：HFSM 状态接口，普通状态和复合状态都实现它。
- `HfsmTransitionConnection`：状态过渡，支持优先级和条件列表。
- `HfsmRuntime`：每个运行实例自己的状态机运行时。
- `HfsmComponent2D`：可挂到 `GameObject2D.Components` 的组件包装。

## 创建 HFSM 图

1. 在 Godot 中创建一个 `HfsmGraphAsset` 资源。
2. 打开 Graph 编辑器。
3. 右键添加 `HfsmStateNodeData` 或 `HfsmCompositeStateNodeData`。
4. 给一个状态勾选 `Default`，或在 `HfsmGraphAsset.InitialStateName` 填入初始状态名。
5. 从状态输出端口连到另一个状态输入端口，右键连线编辑条件。

`HfsmGraphAsset` 是共享配置资源。很多怪可以引用同一个图资源；每个怪只需要创建自己的 `HfsmRuntime`，运行时的当前状态、触发器、状态时间和黑板副本都不会写回图资源。

## 过渡条件

内置条件：

- `HfsmAlwaysCondition`：永远满足。
- `HfsmTriggerCondition`：调用 `runtime.Trigger("Name")` 后，在本帧满足。
- `HfsmBoolCondition`：读取黑板 bool 值并比较。
- `HfsmFloatCondition`：读取黑板 float 值并比较。
- `HfsmTimerCondition`：当前状态进入后经过指定秒数。

同一条连线可以放多个条件，并用 `And` 或 `Or` 组合。`Priority` 越高越先判断；同一帧只走第一条满足条件的过渡。

## 运行时使用

直接创建运行时：

```csharp
var runtime = new HfsmRuntime(graph);
runtime.Start();

runtime.SetValue("CanSeePlayer", true);
runtime.SetValue("DistanceToPlayer", 3.5f);
runtime.Trigger("Hit");

runtime.Update(delta);
GD.Print(runtime.CurrentStatePath);
```

通过组件使用：

```csharp
public override void OnInit()
{
    var hfsm = Owner.GetComponent<HfsmComponent2D>();
    hfsm.SetValue("CanSeePlayer", false);
}

public override void OnPhysicsUpdate(double delta)
{
    var hfsm = Owner.GetComponent<HfsmComponent2D>();
    hfsm.SetValue("CanSeePlayer", true);
    hfsm.Trigger("AttackRequested");
}
```

## 状态里读取黑板

`HfsmStateNodeData.OnEnter()` 默认会调用 `Execute(runtime.Context)`，所以自定义状态节点可以通过 `GraphExecutionContext` 读写黑板：

```csharp
public class PrintSpeedStateNode : HfsmStateNodeData
{
    public override void Execute(GraphExecutionContext context)
    {
        float speed = context.Blackboard.GetValue("Speed", 1f);
        GD.Print($"Speed: {speed}");
    }
}
```

如果需要每帧逻辑，可以继承 `HfsmStateNodeData` 并覆盖：

```csharp
public override void OnUpdate(HfsmRuntime runtime, double delta)
{
    bool canMove = runtime.GetValue("CanMove", true);
}
```

## 复合状态

`HfsmCompositeStateNodeData` 继承 GraphPlugin 的 `SubGraphNodeData`，所以它复用通用子图能力：

- 节点 UI 会注入“进入子图”和“绑定/更换子图资源”按钮。
- `SubGraphPath` 仍然由 `SubGraphNodeData` 保存。
- 新建子图时会创建 `HfsmGraphAsset`。
- 绑定已有子图时只接受 `HfsmGraphAsset`。

进入复合状态时，runtime 会加载 `SubGraphPath` 指向的子 `HfsmGraphAsset`，并创建一个子 `HfsmRuntime`。子 runtime 会继承父 runtime 的黑板层，再 `PushLocal(subGraph)`，所以读取顺序是：

1. 子图 Local Blackboard。
2. 父图 Local Blackboard。
3. 场景里的 Global Blackboard。

父状态退出时，子 runtime 会停止。`CurrentStatePath` 会返回类似 `Combat/Chase` 的路径。
