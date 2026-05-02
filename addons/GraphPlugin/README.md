# GraphPlugin

GraphPlugin 是项目内的通用图编辑与运行时插件。它负责图资源、节点/连线序列化、编辑器窗口、黑板、子图导航、验证、运行时索引，以及运行时调试通道；具体业务图语义放在业务层实现。

## 当前边界

- `runtime/core/` 保存通用图模型：`GraphAsset`、`GraphDocument`、`GraphNodeData`、`GraphConnection`、`GraphTypeRegistry`、`GraphValidationService`、`GraphRuntimeIndex`。
- `runtime/blackboard/` 保存图本地黑板、场景全局黑板和运行时作用域栈。
- `runtime/flow/`、`runtime/state/` 保存插件内置图类型的运行语义。
- `runtime/debug/` 保存运行时快照、序列化、调试注册表和 Godot debugger bridge。
- `editor/` 只服务 Godot 编辑器体验，包括画布窗口、面板、服务、搜索控件和 Runtime Debug 面板。
- HFSM、Skill、Mission 等业务图类型放在 `scripts/gamelogic/`，通过继承 `GraphAsset`、`FlowGraphAsset` 或 `StateGraphAsset` 接入插件能力。

## 文档入口

- [文档索引](docs/README.md)
- [整体架构](docs/architecture.md)
- [编辑器架构](docs/editor-architecture.md)
- [运行时接入](docs/runtime.md)

## 使用原则

- 图资源只以 `GraphAsset.GraphJson` 作为运行数据源。
- 新节点优先继承 `GraphNodeData`，用声明式方法描述端口、分类、搜索关键字和编辑 UI。
- 新图类型通过稳定的 `GraphType` 字符串和 `CreateConnection()` 扩展，不改编辑器窗口。
- 运行时需要跨子图黑板写入或 Runtime Debug 时，实现 `IGraphRuntimeScope`。
- 业务对象放进 action、condition、task、runtime handle 等可复用类，不塞进插件窗口或 core。
