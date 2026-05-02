# GraphPlugin V2 架构

GraphPlugin V2 采用“瘦 core + 明确 editor 服务 + 业务图类型自扩展”的架构。它参考 Unity GraphView、xNode、NodeGraphProcessor、NodeCanvas，以及 Godot GraphEdit/Orchestrator/Dialogic 的常见分层，但不依赖第三方图插件。

## 设计目标

- 核心简单：图数据就是 `GraphAsset` + `GraphDocument` + 节点 + 连线。
- 边界清楚：运行时、编辑器、业务图类型各自负责自己的事。
- 扩展稳定：新增图类型和节点不需要修改窗口。
- 体验完整：分类搜索、黑板、子图、连接标签、验证、Explorer、复制粘贴都作为插件级能力存在。
- 不保留旧实现：旧 `GraphNodeFactory` 和旧三段 JSON 已删除。

## Core Layer

core 位于 `runtime/core/`，负责所有图类型共享的能力。

| 类型 | 职责 |
| --- | --- |
| `GraphAsset` | Godot Resource 入口，暴露 `GraphJson`，提供节点/连线增删查和验证入口。 |
| `GraphDocument` | `GraphJson` 的文档模型，包含版本、图类型、节点、连线、黑板和编辑器状态。 |
| `GraphNodeData` | 节点数据基类，声明端口、分类、搜索关键字、编辑 UI 和运行时回调。 |
| `GraphConnection` | 连线数据基类，保存端点和可选业务数据。 |
| `GraphTypeRegistry` | 注册图类型、节点类型、类型别名，并为反序列化解析 `$type`。 |
| `GraphNodeDefinition` | 节点定义，保存分类、端口和实例创建函数。 |
| `GraphValidationService` | 保存和运行前验证重复 id、悬空连接、端口越界、连接类型、端口上限、黑板 key。 |
| `GraphRuntimeIndex` | 预构建节点表、入边、出边，避免运行时反复扫描。 |

core 不创建 Godot 编辑器窗口，不知道具体 Flow/State/HFSM/Mission 的执行语义。

## Editor Layer

editor 位于 `editor/`，只在 `#if TOOLS` 下编译。

`GraphCanvasEditorWindow` 是窗口壳，负责：

- 创建 toolbar、breadcrumb、GraphEdit。
- 绑定 Godot 信号。
- 创建服务和面板。
- 把用户操作转发给服务。

具体逻辑拆到：

- `GraphSaveService`：同步节点位置和编辑器状态，验证后保存。
- `GraphCommandService`：执行节点/连线增删。
- `GraphClipboardService`：复制粘贴节点，保留选中节点之间的内部连线。
- `GraphConnectionEditorService`：连线命中检测、右键菜单、标签刷新、连线属性编辑。
- `GraphSubGraphNavigator`：进入/返回子图、面包屑、绑定或创建子图资源。
- `GraphBlackboardPanel`：本地图黑板和场景全局黑板。
- `GraphExplorerPanel`：节点树、验证结果和定位。
- `GraphNodeViewBuilder`：节点数据到 Godot `GraphNode` 视图。

## Runtime Layer

runtime 只保留图类型自己的语义：

- FlowGraph：Entry、Action、Condition、Delay、Timeline、Return、多 active node。
- StateGraph：当前状态、Any State、CompletionOnly、Composite SubGraph、优先级、Tag。
- HFSM：在 StateGraph 语义上接入角色、组件、技能状态。
- MissionGraph：Sequence、Parallel、SubGraph 和任务实例部署。

这些运行时都共享 core 的 `GraphRuntimeIndex`、`GraphBlackboardRuntime` 和 `GraphValidationService`。

## Business Layer

业务图类型放在 `scripts/gamelogic/`：

- HFSM 节点和连接放在 `scripts/gamelogic/hfsm/graph/`。
- Skill Flow 图资源放在 `scripts/gamelogic/skills/`。
- Mission 图和任务节点放在 `scripts/gamelogic/missions/mission_chains/`。

业务层可以覆盖 `GraphAsset.CreateConnection()` 来指定自己的连线类型，也可以覆盖 `GraphNodeData.GetGraphTypes()` 控制节点出现在哪些图类型中。

## V2 与旧版差异

旧版把节点创建、端口描述、节点 UI 和部分保存逻辑混在窗口与 `GraphNodeFactory` 中。V2 删除旧 factory，拆成：

- `GraphTypeRegistry`：注册和查询。
- `GraphNodeDefinition.CreateNode()`：创建实例。
- `GraphNodeViewBuilder`：创建编辑器视图。
- `GraphCommandService`：执行数据和视图修改。

旧 `NodesJson`、`ConnectionsJson`、`BlackboardJson` 不再作为图资源运行数据源。图资源只读取和写入 `GraphJson`。

