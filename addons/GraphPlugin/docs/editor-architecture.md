# 编辑器架构

V2 编辑器的目标是窗口薄、服务清楚、面板独立。`GraphCanvasEditorWindow` 不再保存黑板窗口、连接标签、子图栈等重状态。

## 窗口职责

`GraphCanvasEditorWindow` 只做四类事：

- 创建 toolbar、breadcrumb 和 `GraphEdit`。
- 绑定 Godot 信号。
- 创建服务和面板。
- 把操作转发给服务。

窗口 partial 现在只保留入口：

- `GraphCanvasEditorWindow.Nodes.cs`：添加、删除、搜索节点。
- `GraphCanvasEditorWindow.Connections.cs`：连接/断开信号和 Undo/Redo。
- `GraphCanvasEditorWindow.Clipboard.cs`：复制粘贴入口。

黑板和子图不再保留空壳 partial。黑板全部在 `GraphBlackboardPanel`，子图全部在 `GraphSubGraphNavigator`，`ResetNavigation()` 回到主窗口作为插件入口。

## Services

| 服务 | 职责 |
| --- | --- |
| `GraphEditorController` | 清空和加载 `GraphEdit`，恢复 `EditorState`。 |
| `GraphNodeViewBuilder` | 把 `GraphNodeData` 转成 Godot `GraphNode`。 |
| `GraphCommandService` | 执行节点和连线的确定性修改。 |
| `GraphSaveService` | 同步节点位置、缩放、滚动，验证并保存资源。 |
| `GraphClipboardService` | 复制选中节点和内部连线，粘贴时重映射 id。 |
| `GraphConnectionEditorService` | 连线命中、右键菜单、属性编辑、连接标签刷新。 |
| `GraphSubGraphNavigator` | 子图进入/返回、面包屑、绑定/创建子图资源。 |
| `GraphSnapshotService` | 序列化快照、清空图、恢复图、批量追加节点和连线。 |
| `GraphNodeSearchService` | 创建节点搜索弹窗，处理分类、关键字和选择回调。 |
| `GraphEditorShortcutService` | 处理保存、撤销、重做快捷键。 |

服务不持有业务图语义，只处理编辑器工作流。

## Panels

| 面板 | 职责 |
| --- | --- |
| `GraphBlackboardPanel` | 编辑场景全局黑板和当前图本地黑板。 |
| `GraphExplorerPanel` | 展示节点树、验证结果，并定位节点。 |

面板可以打开为独立窗口，生命周期由主窗口管理。

## Controls

`SearchablePopup` 是通用分类搜索控件。节点搜索会使用：

- 节点显示名。
- 节点分类。
- `GraphNodeDefinition.SearchKeywords`。

它支持模糊匹配和分组，分组选择 bug 已修复：树节点 metadata 映射到实际 item，而不是分组内临时 index。

## Undo/Redo

Godot 的 `EditorUndoRedoManager` 仍由窗口层创建 action，因为它需要绑定 Godot 对象方法。具体修改由 `GraphCommandService` 执行。

当前进入 Undo/Redo 的操作：

- 添加节点。
- 删除节点。
- 添加连线。
- 删除连线。
- 清空图。
- 粘贴节点和内部连线。

节点属性编辑、黑板编辑和连线属性编辑目前直接修改数据并保存。若需要更细粒度 Undo，应继续在窗口层创建 action，但具体修改逻辑仍放在 service 或 panel 中。

## 保存流程

保存由 `GraphSaveService.Save()` 执行：

1. 从 `GraphNode.PositionOffset` 同步节点位置。
2. 从 `GraphEdit` 同步 zoom 和 scroll 到 `GraphDocument.EditorState`。
3. 调用 `GraphValidationService.Validate()`。
4. 通过 `GraphAsset.SaveJsonFields()` 写回 `GraphJson`。
5. 调用 `ResourceSaver.Save()` 保存 `.tres`。
