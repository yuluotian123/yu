# GraphPlugin 文件夹结构

GraphPlugin 的目录按“运行时核心、编辑器体验、文档示例”分层。新增代码时优先放到已有职责目录，不要把业务逻辑塞回窗口类。

## 总览

```text
addons/GraphPlugin/
├── GraphPlugin.cs
├── plugin.cfg
├── README.md
├── docs/
├── editor/
├── runtime/
└── samples/
```

## Runtime

`runtime/` 是可被游戏运行时依赖的代码。这里不能依赖 Godot Editor API，也不能引用 `EditorInterface`、`EditorUndoRedoManager`、`EditorFileDialog`。

```text
runtime/
├── blackboard/
├── core/
├── debug/
├── flow/
└── state/
```

`runtime/core/` 放通用图能力：

- `GraphAsset`：Godot Resource 入口。
- `GraphDocument`：`GraphJson` 的内部文档模型。
- `GraphNodeData`：节点数据基类。
- `GraphConnection`：连线数据基类。
- `GraphDefinitions`：图、节点、端口定义。
- `GraphTypeRegistry`：注册中心和类型解析。
- `GraphValidation`：结构验证。
- `GraphRuntimeIndex`：运行时查询索引。
- `GraphRuntimeScope`：父子图运行时作用域协议和跨子图黑板写入。
- `GraphJsonHelper`：多态 JSON 序列化。

`runtime/blackboard/` 放黑板系统：

- 图本地黑板条目。
- 全局黑板节点。
- 运行时黑板作用域栈。
- Bool、Int、Float、String、Vector2、Color 等值类型。

`runtime/flow/` 和 `runtime/state/` 只放图类型自己的节点和运行时语义。HFSM、Skill、Mission 这类业务图类型放在 `scripts/gamelogic/`，不要反向塞进插件 core。

`runtime/debug/` 放运行时调试通道：

- `GraphRuntimeDebugRegistry`：运行时注册、事件记录和上下文快照。
- `GraphRuntimeDebugSnapshots`：编辑器可展示的快照 DTO。
- `GraphRuntimeDebugSnapshotFactory`：从黑板、UserData、Timeline 和运行时状态创建快照。
- `GraphRuntimeDebugSerialization`：Godot `Dictionary`/`Array` 传输格式。
- `GraphRuntimeDebugBridge`：通过 `EngineDebugger` 向编辑器发送快照。

## Editor

`editor/` 只在 Godot 编辑器中使用，所有文件都应包在 `#if TOOLS` 内。

```text
editor/
├── controls/
├── panels/
├── services/
└── windows/
```

`editor/windows/` 只保留窗口壳和 Inspector 插件：

- `GraphCanvasEditorWindow`：创建 toolbar、GraphEdit，绑定信号，把具体工作转发给服务。
- `GraphCanvasInspectorPlugin`：在 Inspector 中提供打开图编辑器入口。

`editor/services/` 放无独立窗口的编辑器能力：

- `GraphCommandService`：节点和连线增删。
- `GraphSaveService`：同步位置、验证、保存。
- `GraphClipboardService`：复制粘贴节点和内部连线。
- `GraphConnectionEditorService`：连线命中、右键菜单、标签和属性编辑。
- `GraphSubGraphNavigator`：子图进入、返回、面包屑和资源绑定。
- `GraphNodeViewBuilder`：把节点数据构造成 Godot `GraphNode`。
- `GraphEditorController`：加载图到 `GraphEdit`。
- `GraphSnapshotService`：快照、恢复、清空和批量追加节点/连线。
- `GraphNodeSearchService`：节点搜索弹窗。
- `GraphEditorShortcutService`：窗口快捷键。
- `GraphRuntimeDebugEditorDebuggerPlugin`：接收游戏运行时发来的调试快照。
- `GraphRuntimeDebugRemoteStore`：保存编辑器侧最近收到的远端运行时快照。

`editor/panels/` 放可独立开关的 UI 面板：

- `GraphBlackboardPanel`：本地/全局黑板编辑。
- `GraphExplorerPanel`：节点树、验证结果和定位。
- `GraphTimelinePanel`：编辑 `FlowTimelineNodeData` 的轨道、片段和事件。
- `GraphRuntimeDebugPanel`：显示远端运行时快照，并高亮当前图里的 active node。

`editor/controls/` 放可复用小控件：

- `SearchablePopup`：分类搜索、模糊匹配和关键字搜索。
- `ReorderableListControl`：可排序列表编辑器。

## Docs And Samples

`docs/` 是中文维护文档。文档必须描述当前实现，不写已经过期的计划口径。

`samples/` 放最小示例节点，供新增图类型或节点时参考。业务项目代码不要依赖 samples。
