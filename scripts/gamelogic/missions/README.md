# Mission Module

Mission Module 负责启动和推进任务链。任务链使用 GraphPlugin Flow Graph 表达，Mission Runtime 把图节点转换为任务部署、条件推进、子图执行和存档状态。

## 核心结构

- `IMissionModule` / `MissionModule`：业务侧启动任务链的模块入口。
- `MissionManager<T>`：活动任务和任务系统组件管理。
- `MissionChainManager`：创建并管理 `MissionGraphRuntime`。
- `MissionGraphRuntime`：FlowGraph 任务链运行时。
- `MissionChainSaver`：任务链与任务进度存档接入。
- `MissionGraph`：任务图资源。
- `MissionNode`：创建任务原型并等待任务状态。
- `MissionSubGraphNodeData`：启动并等待任务子图。

## 快速开始

```csharp
IMissionModule missions = ModuleSystem.GetModule<IMissionModule>();
missions.StartChain(res://assets/missions/main_chain.tres);
```

`MissionModule` 会通过 `IResourceModule` 加载 `MissionGraph`，并在初始化时把 `MissionChainSaver` 注册到 Save Module。

## 创建任务链

1. 创建 `MissionGraph` 资源。
2. 使用 GraphPlugin 打开图编辑器。
3. 从 Entry 节点连接 `MissionNode`、Action、Condition 或子图节点。
4. 配置 `MissionPrototype`、推进条件和 Sequence/Parallel 连线模式。
5. 保存图并通过 `IMissionModule.StartChain()` 启动。

## 运行时数据流

```text
MissionModule
  -> MissionChainManager
  -> MissionGraphRuntime
  -> MissionDeploymentRequest
  -> MissionManager<object>
  -> Mission / Require handles
```

任务完成、移除或状态变化会通知 `IMissionSystemComponent<T>`，任务链据此继续传播 FlowGraph。

## 存档

`MissionChainSaver` 保存活动任务、任务需求进度、活动节点和待处理子图。加载前必须确保任务资源、节点 ID 和图路径仍然兼容旧存档。

## 当前注意事项

- `IMissionModule` 目前只暴露 `StartChain()`，缺少停止、查询、重复启动策略和失败结果。
- `MissionModule.Process()` 当前为空，但仍实现 `IProcessModule`，应确认是否需要保留帧更新注册。
- `StartChain()` 使用一次性同步资源加载，失败时没有向调用者返回原因。
- Mission、Graph 和 Save 三个系统耦合较深，需要增加资源验证和端到端 smoke test。
- 节点 ID、资源路径和任务 ID 都属于存档协议，重命名前必须提供迁移。
- 当前 Runtime Debug 相关旧文档没有对应实现，任务调试应暂时依赖日志和显式状态快照。

## 相关文档

- [`addons/GraphPlugin/README.md`](../../../addons/GraphPlugin/README.md)
- [`scripts/gamelogic/saves/README.md`](../saves/README.md)

