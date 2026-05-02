# 编辑器工作流

GraphPlugin 编辑器基于 Godot `GraphEdit`。资源在 Inspector 中打开后，会弹出 `GraphCanvasEditorWindow`。

## 打开图

在 Godot Inspector 中选中 `GraphAsset` 或子类资源，然后点击插件提供的打开入口。窗口会：

1. 清空当前 `GraphEdit`。
2. 从 `GraphJson` 读取节点、连线、黑板和编辑器状态。
3. 创建节点视图。
4. 恢复连线、缩放和滚动位置。

## 添加节点

在画布空白处右键打开节点搜索。

搜索支持：

- 节点显示名。
- 分类。
- 节点额外关键字。
- 模糊匹配。

节点列表按 `GraphNodeData.GetCategory()` 分组。

## 删除节点

选中节点后使用 GraphEdit 的删除操作。删除节点会同时删除相关连线，并进入 Undo/Redo。

## 连线和断线

拖拽端口创建连线。GraphAsset 会检查：

- 端点节点是否存在。
- 端口是否越界。
- 端口类型是否一致。
- 输入/输出端口连接数是否超过上限。
- 是否已经存在同样端点的连线。

右键点击连线可打开菜单：

- `Edit Connection`：编辑连线属性。
- `Delete Connection`：删除连线。

连线中点附近会显示 `GraphConnection.CreateConnectionLabel()` 创建的标签。

## 复制粘贴

复制选中节点时，`GraphClipboardService` 会同时复制选中节点之间的内部连线。粘贴时会：

- 为每个节点生成新 id。
- 按固定偏移移动位置。
- 把内部连线端点重映射到新 id。

粘贴操作进入 Undo/Redo。

## 黑板

点击 toolbar 的 `Blackboard` 打开黑板面板。

面板包含：

- `Global`：当前编辑场景中的 `GraphBlackboardNode`。
- `Local`：当前图资源的本地黑板。

保存本地黑板会写入当前图的 `GraphJson`。保存全局黑板会标记当前场景未保存。

## 子图

继承 `SubGraphNodeData` 的节点会自动注入：

- `Enter SubGraph`：进入已绑定子图。
- `Bind SubGraph Resource`：选择或创建子图资源。

进入子图前会自动保存当前图。顶部面包屑显示父图链，可返回或跳转到上层图。

## Explorer

点击 toolbar 的 `Explorer` 打开图浏览器。

Explorer 提供：

- 按分类展示节点。
- 双击节点定位到画布。
- 展示当前图验证结果。

## 保存和验证

点击 `Save` 或按 `Ctrl+S` 保存。保存流程：

1. 同步节点位置。
2. 同步缩放和滚动。
3. 验证图结构。
4. 写入 `GraphJson`。
5. 保存 `.tres`。

验证错误会阻止保存。常见错误：

- 节点 id 重复。
- 未知节点类型。
- 悬空连接。
- 端口越界。
- 端口类型不兼容。
- 连接数超过端口上限。
- 连线类型不匹配。
- 黑板 key 重复。

