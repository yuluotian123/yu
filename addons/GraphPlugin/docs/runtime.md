# 运行时接入

> **未实现提示**：本文后半部分的 `GraphRuntimeDebugRegistry`、快照、bridge 和编辑器面板属于保留设计，当前代码树中没有对应实现。Flow、State、Behavior Tree、黑板和 `IGraphRuntimeScope` 内容仍与当前代码对应。

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
- `IGraphRuntimeScope`：把父图和子图 runtime 组织成可遍历的运行时树。
- `GraphRuntimeBlackboardWriter`：从根 runtime 递归查找声明了某个 key 的本地图黑板并写入。
- `GraphRuntimeDebugRegistry`：注册 runtime、记录事件、捕获 context/timeline 快照。

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

StateGraph 已实现 `IGraphRuntimeScope`。Composite State 的子运行时会出现在 `ChildScopes` 中，因此外部 `SetValue()` 可以写到真正声明 key 的子图黑板，Runtime Debug 也能显示父子状态路径。

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

MissionGraph 现在是 FlowGraph 的业务特化：

- `MissionGraph` 继承 `FlowGraphAsset`，图类型为 `MissionGraph`。
- FlowGraph 通用 `FlowConnection` 提供 Sequence/Parallel 推进时机和条件列表。
- `MissionNode` 创建 `MissionPrototype<object>`。
- `MissionSubGraphNodeData` 启动并等待 Mission 子图。
- `FlowActionNodeData` 执行一组 `GraphActionBase`，Mission action 也接入这套体系。
- `MissionGraphRuntime` 继承 `FlowGraphRuntime`，补充任务部署队列、任务完成回调、子图 runtime、保存恢复和 Mission debug metadata。
- `MissionChainManager` 管理所有 `MissionGraphRuntime`，并把 deployment request 转交给 `MissionManager<object>`。
- `MissionChainSaver` 保存 active mission、pending subgraph 和任务需求进度。
- 启动前调用 `GraphAsset.Validate()`，验证失败拒绝启动。

MissionGraph 也使用 `GraphBlackboardRuntime`，子图通过父 runtime 的 blackboard fork 继承父图黑板。`MissionGraphRuntime` 实现 `IGraphRuntimeScope`，因此 Runtime Debug 和跨子图黑板写入会沿着 Mission 子图树工作。

## Runtime Debug

运行时调试是可选能力。接入方式：

```csharp
_debugHandle = GraphRuntimeDebugRegistry.Register(owner, runtime, graph, "Skill", CreateMetadata);
```

运行时在关键事件处调用：

```csharp
GraphRuntimeDebugRegistry.RecordEvent(runtime, "NodeEntered", node.GetDisplayName(), graph, node);
GraphRuntimeDebugRegistry.CaptureContext(runtime, context, true);
```

如果 runtime 实现 `IGraphRuntimeScope`，调试面板会自动遍历子图 scope，并显示：

- active node ids 或 current state path。
- 当前图黑板和父/全局黑板快照。
- `GraphExecutionContext.UserData` 摘要。
- Timeline phase、clip、normalized time。
- 最近事件。

编辑器中打开图后启用 toolbar 的 `Runtime Debug`。若当前选中的场景节点匹配某个远端 runtime owner，面板会优先显示该 runtime；当前图与 runtime scope 匹配时会高亮 active node。

## Blackboard 作用域

`GraphBlackboardRuntime` 查询顺序：

1. 当前图本地黑板。
2. 父图本地黑板。
3. 场景全局 `GraphBlackboardNode`。

`Fork()` 会复制本地栈，适合隔离子运行时。
`ForkSharedLocals()` 会共享本地栈，适合 Composite State 这类父子状态协同场景。
