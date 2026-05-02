# GraphPlugin V2 文档

这组文档描述当前已经落地的 V2 架构。V2 的目标不是把系统做重，而是让核心模型稳定、编辑器职责清楚、业务图类型能按统一规则扩展，并且能在运行中观察 Flow、State、HFSM、Skill 等图的真实状态。

## 阅读顺序

1. [整体架构](architecture.md)：先理解 runtime、editor、业务层边界。
2. [文件夹结构](folder-structure.md)：确认每类代码应该放在哪里。
3. [创建图类型](create-graph-type.md)：新增 Flow、State、HFSM、Mission 之外的图类型。
4. [创建节点](create-node.md)：新增节点、端口、搜索分类和编辑 UI。
5. [编辑器架构](editor-architecture.md)：窗口、服务、面板、Undo/Redo 的职责。
6. [序列化格式](serialization.md)：`GraphJson`、`$type`、类型重命名和资源迁移。
7. [运行时接入](runtime.md)：Flow、State、HFSM、Mission 如何执行图。
8. [编辑器工作流](workflow.md)：日常编辑、黑板、子图、验证和 Explorer。

## 当前已落地

- `GraphAsset.GraphJson` 是图资源唯一运行数据源。
- `GraphDocument` 保存 `SchemaVersion`、`GraphType`、`Nodes`、`Connections`、`BlackboardEntries`、`EditorState`。
- `GraphTypeRegistry` 负责图类型、节点类型、类型别名和反序列化类型解析。
- `GraphValidationService` 在保存和运行前统一验证结构。
- `GraphRuntimeIndex` 缓存节点表、入边和出边。
- `GraphCanvasEditorWindow` 已瘦身，黑板、子图、连接编辑、保存、剪贴板、Explorer、Timeline、Runtime Debug 拆为独立服务或面板。
- `IGraphRuntimeScope` 统一描述父子图运行时树，供跨子图黑板写入和 Runtime Debug 使用。
- Runtime Debug 已通过 Godot `EngineDebugger` 把运行时快照推到编辑器侧，可显示 active node、当前状态、黑板、UserData、Timeline 和事件。

## 扩展原则

- core 只放通用图能力，不放具体业务语义。
- editor 只放工具体验，不进入运行时。
- Flow、State、HFSM、Mission 只保留自己的运行语义。
- 新节点优先继承 `GraphNodeData` 并覆盖少量声明式方法。
- 复杂业务对象放进 action、condition、task 等可复用类，不塞进编辑器窗口。
- 新运行时若管理子图，应实现 `IGraphRuntimeScope`，让黑板写入和调试视图自动跨层级工作。
