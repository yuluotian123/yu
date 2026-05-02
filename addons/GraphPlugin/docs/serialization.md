# 序列化格式

V2 图资源只使用 `GraphAsset.GraphJson` 一个字段。旧版 `NodesJson`、`ConnectionsJson`、`BlackboardJson` 不再作为图资源运行数据源。

## GraphJson 文档

`GraphJson` 反序列化后是 `GraphDocument`：

```json
{
  "$type": "GraphDocument",
  "SchemaVersion": 2,
  "GraphType": "FlowGraph",
  "Nodes": [],
  "Connections": [],
  "BlackboardEntries": [],
  "EditorState": {
    "$type": "GraphEditorState",
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
  "$type": "FlowTimelineNodeData",
  "NodeType": "FlowTimelineNodeData",
  "Duration": 0.2
}
```

反序列化顺序：

1. 通过 `GraphTypeRegistry.TryResolveType()` 查注册类型。
2. 查类型别名。
3. 回退到当前 AppDomain 反射查找。

## 支持的数据形态

推荐使用：

- public property。
- 标注 `[JsonInclude]` 的字段。
- `bool`、`int`、`float`、`double`、`string`。
- enum。
- `Godot.Vector2`。
- `Godot.Color`。
- `List<T>`。
- 有无参构造的普通对象。

不推荐直接使用：

- `Dictionary<TKey, TValue>`。
- Godot 场景节点引用。
- 委托、事件、运行时句柄。

复杂数据应拆成明确的可序列化类。

## 类型重命名

节点或业务对象改名后，旧资源里的 `$type` 会找不到。短期迁移可注册别名：

```csharp
GraphTypeRegistry.RegisterAlias("OldNodeName", "NewNodeName");
```

长期做法是打开资源并重新保存，让 `GraphJson` 写入新类型名。

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

