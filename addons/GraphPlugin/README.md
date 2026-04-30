# GraphPlugin

GraphPlugin 是项目里的通用图框架。它只提供图资源、节点、连接、黑板、编辑器和通用运行时能力，不引用 GameLogic 类型。

当前 GraphPlugin 分为三层：

- `runtime/core`：通用图数据、连接、节点工厂、执行上下文、子图、完成结果。
- `runtime/blackboard`：图黑板资源和运行时读写。
- `runtime/flow`：通用 FlowGraph，适合技能流程、任务流程、一次性执行链。
- `runtime/state`：通用 StateGraph，适合 HFSM、AI 状态机、动画状态机一类“同一时间只有一个当前状态”的图。
- `editor/windows`、`editor/controls`：GraphEdit 编辑器窗口和复用控件。
- `samples`：插件示例节点。

## Core

### GraphAsset

`GraphAsset` 是所有图资源的基类，继承自 `Resource`。

核心字段：

- `NodesJson`：节点序列化数据。
- `ConnectionsJson`：连接序列化数据。
- `BlackboardJson`：图本地黑板序列化数据。

运行时访问：

- `Nodes`
- `Connections`
- `BlackboardEntries`

常用方法：

- `GetAllowedNodeTypes()`：控制编辑器里允许添加哪些节点。
- `CreateConnection()`：创建连接，子类可返回自定义连接类型。
- `GetOutgoingConnections(nodeId, fromPort?)`：按节点和可选输出口查询连接。
- `GetIncomingConnections(nodeId, toPort?)`：按节点和可选输入口查询连接。
- `FindNodeById(nodeId)`：按 id 查找节点。

### GraphNodeData

`GraphNodeData` 是所有节点数据的基类。

常用 override：

- `GetGraphTypes()`：声明节点可用于哪些图类型。
- `GetDisplayName()`：节点标题。
- `GetNodeColor()`：节点颜色。
- `GetInputCount()` / `GetOutputCount()`：端口数量。
- `GetInputPortName(port)` / `GetOutputPortName(port)`：显式端口名称。
- `GetInputMaxConnections(port)` / `GetOutputMaxConnections(port)`：端口连接数量限制，`-1` 表示不限。
- `CreateUI(GraphEditorContext context)`：编辑器节点 UI。
- `Execute(GraphExecutionContext context)`：通用瞬时执行入口。

端口名称会显示在节点 UI 上，也会作为 slot metadata 写入 Godot `GraphNode`。

### GraphConnection

`GraphConnection` 保存一条连接：

- `FromNode`
- `FromPort`
- `ToNode`
- `ToPort`

可 override：

- `GetDisplayName()`
- `IsEditable()`
- `IsAvailable`
- `CreateEditUI(GraphEditorContext context)`
- `CreateConnectionLabel()`

### GraphExecutionContext

`GraphExecutionContext` 是运行时上下文：

- `Graph`
- `Blackboard`
- `UserData`

`UserData` 是一个对象列表，用来由业务层注入 owner、runtime、resource 等上下文：

```csharp
context.UserData.Add(ownerContext);
context.UserData.Add(runtimeContext);

var owner = context.GetUserData<OwnerContext>();
var runtime = context.GetUserData<RuntimeContext>();
```

GraphPlugin 不直接依赖业务层类型。

### NodeCompletion

`NodeCompletion` 表示一个节点完成后的输出：

- `OutputPort`
- `Label`

常用工厂：

- `NodeCompletion.Completed()`
- `NodeCompletion.Next()`
- `NodeCompletion.True()`
- `NodeCompletion.False()`
- `NodeCompletion.Return(label)`

FlowGraph 和 StateGraph 都用它表达“完成后从哪个输出口推进”。

## Blackboard

GraphPlugin 支持两类黑板：

- Global Blackboard：场景里的 `GraphBlackboardNode`。
- Local Blackboard：每个 `GraphAsset` 自己的 `BlackboardJson`。

运行时读取顺序：

1. 当前图 Local Blackboard。
2. 父图 Local Blackboard。
3. Global Blackboard。

常用 API：

```csharp
var blackboard = new GraphBlackboardRuntime();
blackboard.PushLocal(graph);

blackboard.SetValue("Speed", 8f);
float speed = blackboard.GetValue("Speed", 0f);

if (blackboard.TryGetValue("CanMove", out bool canMove))
{
}
```

子图运行时如果要共享父图局部黑板，使用：

```csharp
parentBlackboard.ForkSharedLocals();
```

## FlowGraph

FlowGraph 用于多后继、瞬时传播、等待节点 tick 的流程图。

核心类型：

- `FlowGraphAsset`
- `FlowGraphRuntime`
- `IFlowNode`
- `GraphActionBase`
- `GraphConditionBase`

内置节点：

- `FlowEntryNodeData`：入口，输出 `Next`。
- `FlowActionNodeData`：执行 action 列表，输出 `Next`。
- `FlowConditionNodeData`：判断 condition 列表，输出 `True` 或 `False`。
- `FlowDelayNodeData`：等待秒数，完成后输出 `Completed`。
- `FlowTimelineNodeData`：时间轴节点，支持 Start/Update/Event/Complete/Cancel action。
- `FlowReturnNodeData`：结束流程，输出 `Return` label。

`IFlowNode` 形态：

```csharp
public interface IFlowNode
{
    void Enter(FlowGraphRuntime runtime, GraphExecutionContext context);
    void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta);
    bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion);
    void Exit(FlowGraphRuntime runtime, GraphExecutionContext context);
}
```

`TryGetCompletion == false` 表示节点仍在运行。

FlowGraphRuntime 特性：

- 从 `Graph.primeNode` 开始。
- 瞬时节点会在同一帧继续传播。
- 等待节点进入 active list，每帧 tick。
- 一个输出口可以连接多个后继节点。
- `MaxPropagationSteps` 防止无限瞬时循环。
- `Returned` 事件接收 Return label。

## StateGraph

StateGraph 用于“同一时间只有一个当前状态”的图。

核心类型：

- `StateGraphAsset`
- `StateGraphRuntime`
- `IStateNodeData`
- `StateNodeData`
- `CompositeStateNodeData`
- `StateTransitionConnection`
- `StateConditionBase`

内置伪节点：

- `AnyStateNodeData`
- `StateReturnNodeData`

状态节点接口：

```csharp
public interface IStateNodeData
{
    bool CanEnter(StateGraphRuntime runtime);
    void OnEnter(StateGraphRuntime runtime);
    void OnUpdate(StateGraphRuntime runtime, double delta);
    bool TryGetCompletion(StateGraphRuntime runtime, out NodeCompletion completion);
    void OnExit(StateGraphRuntime runtime);
}
```

默认 `StateNodeData.TryGetCompletion()` 返回 false。需要自动推进的状态可以 override，并返回指定输出口。

StateGraphRuntime 每帧顺序：

1. 检查 Any State transition，用于高优先级打断。
2. 调用当前状态 `OnUpdate()`。
3. 如果当前状态返回 completion，只检查 completion 指定输出口的 transition。
4. 如果没有 completion 推进，再检查当前状态普通 transition。

`StateTransitionConnection.CompletionOnly = true` 的连接只会被 completion 推进使用，普通 transition 检查会跳过它。

切换到目标状态前，会调用目标状态：

```csharp
targetState.CanEnter(runtime)
```

这适合做技能冷却、状态进入门槛等判断。

## 编辑器

打开方式：

1. 在 Inspector 中选中 `GraphAsset` 或其子类资源。
2. 点击打开图编辑器按钮。
3. 在 GraphEdit 空白处右键添加节点。
4. 从输出口拖到输入口创建连接。
5. 保存资源。

编辑器能力：

- 节点搜索添加。
- 端口名称显式显示。
- 连接数量校验。
- 连接右键编辑。
- Blackboard 编辑。
- 子图进入和面包屑导航。
- 复制粘贴、Undo/Redo。
- 自动整理按钮。

## 新增一种图

最小示例：

```csharp
[Tool]
[GlobalClass]
public partial class DialogGraphAsset : GraphAsset
{
    public const string GraphTypeName = "DialogGraph";

    public override string GraphType
    {
        get => GraphTypeName;
        set { }
    }

    public override string GetEditorTitle() => "Dialog Graph";
}

public class DialogLineNodeData : GraphNodeData
{
    public string Text { get; set; } = string.Empty;

    public override List<string> GetGraphTypes() => new() { DialogGraphAsset.GraphTypeName };
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;
    public override string GetOutputPortName(int port) => "Next";
}
```

如果需要自定义连接，继承 `GraphConnection`，并在图资源里 override `CreateConnection()`。

## 注意事项

- GraphPlugin 不引用 GameLogic 类型。
- 运行时写入黑板默认只影响内存运行时，不会自动保存 `.tres` 或 `.tscn`。
- `GraphJsonHelper` 通过 `$type` 的类名查找类型，避免可序列化类重名。
- 自定义节点、连接、action、condition 尽量提供无参构造。
- 需要持久化的数据使用 public get/set 属性。
