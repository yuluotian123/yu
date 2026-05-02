# 运行时接入

运行时只处理图类型自己的语义。通用数据、黑板、验证和查询索引全部来自 core。

## 公共运行时能力

所有图类型都可以使用：

- `GraphAsset.Validate(out GraphValidationResult result)`：运行前结构验证。
- `GraphAsset.GetRuntimeIndex()`：获取缓存索引。
- `GraphAsset.FindNodeById()`：按 id 查节点。
- `GraphAsset.GetOutgoingConnections()`：查输出连线。
- `GraphAsset.GetIncomingConnections()`：查输入连线。
- `GraphBlackboardRuntime`：读取本地图、父图、全局黑板。
- `GraphExecutionContext`：向节点传递图、黑板和业务对象。

## FlowGraph

`FlowGraphRuntime` 负责：

- 从 `PrimeNode` 开始执行。
- 支持多个 active node。
- 每帧 tick `IFlowNode`。
- 支持 `NodeCompletion` 指定输出端口。
- 支持 `FlowReturnNodeData` 返回标签。

启动流程：

```csharp
var runtime = new FlowGraphRuntime(graph, context);
runtime.Start();
runtime.Update(delta);
```

启动前会验证图结构，验证失败会拒绝运行。

## StateGraph

`StateGraphRuntime` 负责：

- 当前状态进入、更新、退出。
- Any State transition。
- CompletionOnly transition。
- Composite state 子图。
- Transition 优先级。
- State Tag 查询。

常用入口：

```csharp
runtime.Start();
runtime.Update(delta);
runtime.Trigger("Jump");
runtime.ChangeState("Airborne");
```

StateGraph 查询已经走 `GraphRuntimeIndex` 的节点表和连线索引。

## HFSM

HFSM 在 StateGraph 基础上扩展：

- `HfsmGraphAsset` 指定图类型为 `HfsmGraph`。
- `HfsmTransitionConnection` 扩展状态连线。
- `IHfsmStateNodeData` 和 `IHfsmPseudoNodeData` 限定 HFSM 可用节点。
- `HfsmRuntime` 把角色、组件、技能上下文放入 `GraphExecutionContext.UserData`。

HFSM 子图使用 `HfsmCompositeStateNodeData`，只接受 `HfsmGraphAsset`。

## Skill Flow

Skill Flow 继承 FlowGraph：

- `SkillFlowGraphAsset` 图类型是 `SkillFlowGraph`。
- 允许复用 FlowGraph 节点。
- 技能行为通过 action、condition、timeline 等对象组合。

这样技能图不需要重写 FlowGraph 执行器。

## MissionGraph

MissionGraph 运行时由 `MissionChainHandle` 和 `MissionChainManager` 管理：

- `MissionNode` 创建任务原型。
- `ConnectionWithConditon` 控制 Sequence/Parallel 和条件。
- `SubGraphNodeData` 用于任务链子图。
- 启动前调用 `GraphAsset.Validate()`，验证失败拒绝启动。

MissionGraph 也使用 `GraphBlackboardRuntime`，子图可以继承父图黑板。

## Blackboard 作用域

`GraphBlackboardRuntime` 查询顺序：

1. 当前图本地黑板。
2. 父图本地黑板。
3. 场景全局 `GraphBlackboardNode`。

`Fork()` 会复制本地栈，适合隔离子运行时。  
`ForkSharedLocals()` 会共享本地栈，适合 Composite State 这类父子状态协同场景。

