# 序列化格式

V2 图资源只使用 `GraphAsset.GraphJson` 一个字段。旧版 `NodesJson`、`ConnectionsJson`、`BlackboardJson` 不再作为图资源运行数据源。

## GraphJson 文档

`GraphJson` 反序列化后是 `GraphDocument`：

```json
{
  "$type": "yu:GraphDocument",
  "SchemaVersion": 2,
  "GraphType": "FlowGraph",
  "Nodes": [],
  "Connections": [],
  "BlackboardEntries": [],
  "EditorState": {
    "$type": "yu:GraphEditorState",
    "ScrollOffset": { "x": 0, "y": 0 },
    "Zoom": 1
  }
}
```

字段含义：

- `SchemaVersion`：当前格式版本，V2 固定为 `2`。
- `GraphType`：图类型稳定名。
- `Nodes`：节点数据列表。
- `Connections`：连线数据列表。
- `BlackboardEntries`：图本地黑板。
- `EditorState`：编辑器缩放和滚动，运行时可忽略。

## 多态类型

`GraphJsonHelper` 会为对象写入 `$type`：

```json
{
  "$type": "yu:FlowTimelineNodeData",
  "NodeType": "FlowTimelineNodeData",
  "Duration": 0.2
}
```

类型解析规则：

1. 默认写入 `程序集简单名:CLR完整类型名`，不包含程序集版本；可用 `[GraphSerializationId("my.stable.id")]` 固定跨重命名 ID。
2. `GraphTypeRegistry.TryResolveType()` 先解析别名链，再查询扫描注册表。
3. 兼容现有短类名和完整 CLR 名。重名不会按扫描顺序覆盖，存在歧义时必须登记明确别名。
4. 未知类型、与目标字段不兼容的类型、非法枚举和错误字段形态都会失败，不再降级为基类或空对象。
5. 未识别的历史/扩展字段随原对象保留，重新序列化时原样写回，避免删除旧数据。

## 支持的数据形态

推荐使用：

- public property。
- 标注 `[JsonInclude]` 的字段。
- `bool`、`int`、`float`、`double`、`string`。
- enum。
- `Godot.Vector2`。
- `Godot.Color`。
- `List<T>` 和图集合使用的 `IList<T>`。
- 有无参构造的普通对象。

不推荐直接使用：

- `Dictionary<TKey, TValue>`。
- Godot 场景节点引用。
- 委托、事件、运行时句柄。

复杂数据应拆成明确的可序列化类。

## 类型重命名

节点或业务对象改名后，应保持显式 ID 不变，或在加载图之前登记旧 ID/旧类名别名：

```csharp
GraphTypeRegistry.RegisterAlias("OldNodeName", GraphTypeRegistry.GetSerializationId(typeof(NewNodeData)));
```

别名支持链式迁移，循环和重复目标冲突会报错。重新保存会写入新 ID，但不会自动批量改写资源；节点的旧 `NodeType` 也可以通过同一别名解析定义。

## 错误与索引边界

`graph.TryLoadDocument(out error)` 是编辑器和运行时的加载边界。失败时保留原始 `GraphJson`，拒绝保存和启动；直接访问 `Document` 会抛出包含资源路径和字段上下文的 `JsonException`。只有空白 JSON 表示新建空图，`null`、非法 JSON 和非 V2 schema 不会被当作空图。修复 JSON 或登记缺失别名后，重新赋值 `GraphJson` 可清除缓存错误并重试。

编辑器打开失败会显示错误并保留原来的画布。撤销快照先完成解析再清空图。当前没有原始 JSON 的专用可视化恢复界面。

`Nodes` 和 `Connections` 暴露 `IList<T>`，赋值时复制到受控集合。增删、替换、节点 ID 及连线端点变化自动更新文档结构版本；索引按版本惰性重建，持有旧索引引用的调用者也能读到新结构，不需要每次查询遍历全图。其他属性的编辑仍沿用 `MarkDirty()`。黑板运行时保留自己的作用域快照，不参与拓扑索引。

## 资源兼容策略

当前迁移采用硬切：

- 不再读取旧 `NodesJson`、`ConnectionsJson`。
- 现有资源需要保存为 V2 `GraphJson`。
- `.tres` 必须保持 Godot 文本资源格式，文件开头应为 `[gd_resource`，不能有 BOM 或 JSON 原文直接作为文件头。

## 保存入口

不要手写 `GraphJson`。编辑器保存调用：

```csharp
GraphSaveService.Save(owner, graph, graphEdit);
```

运行时或工具代码需要写回时调用：

```csharp
graph.MarkDirty();
graph.SaveJsonFields();
ResourceSaver.Save(graph, graph.ResourcePath);
```

