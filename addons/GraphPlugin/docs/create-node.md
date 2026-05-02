# 创建节点

节点数据继承 `GraphNodeData`。节点负责保存可序列化字段、声明端口、提供搜索信息、创建节点内部编辑 UI，并在运行时执行自己的行为。

## 最小节点

```csharp
public sealed class DialogLineNodeData : GraphNodeData
{
    public string Text { get; set; } = string.Empty;

    public override List<string> GetGraphTypes() => new() { "DialogGraph" };
    public override string GetCategory() => "Dialog";
    public override string GetDisplayName() => "Line";
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;
}
```

只要节点有无参构造，`GraphTypeRegistry.AutoRegisterAll()` 就会发现它。

## 节点类型名

`NodeType` 默认是 CLR 类型名，例如 `DialogLineNodeData`。它会写进 `GraphJson`：

```json
{
  "$type": "DialogLineNodeData",
  "NodeType": "DialogLineNodeData"
}
```

如果重命名类，应该注册别名：

```csharp
GraphTypeRegistry.RegisterAlias("OldDialogLineNodeData", "DialogLineNodeData");
```

硬切 V2 不保留旧 factory，但类型别名仍适合短期资源迁移。

## 分类和搜索

节点搜索菜单会按 `GetCategory()` 分组，并用 `GetDisplayName()`、分类和 `GetSearchKeywords()` 做模糊搜索。

```csharp
public override string GetCategory() => "Dialog/Branch";

public override List<string> GetSearchKeywords()
    => new() { "choice", "branch", "选项", "分支" };
```

## 端口声明

基础端口 API：

```csharp
public override int GetInputCount() => 1;
public override int GetOutputCount() => 2;
public override string GetInputPortName(int port) => "In";
public override string GetOutputPortName(int port) => port == 0 ? "Yes" : "No";
```

连接规则：

```csharp
public override int GetInputMaxConnections(int port) => 1;
public override int GetOutputMaxConnections(int port) => -1;
public override int GetInputPortType(int port) => 0;
public override int GetOutputPortType(int port) => 0;
```

`GraphValidationService` 会检查端口越界、端口类型不匹配和连接数超过上限。

## 编辑器 UI

节点内部 UI 在 `CreateUI()` 中创建：

```csharp
public override void CreateUI(GraphEditorContext context)
{
    var edit = new LineEdit
    {
        Text = Text,
        PlaceholderText = "Dialog text"
    };
    edit.TextChanged += value => Text = value;
    context.GraphNode.AddChild(edit);
}
```

`GraphEditorContext` 可以访问：

- `CurrentGraph`
- `RootGraph`
- `ParentGraphs`
- `GraphEdit`
- `GraphNode`
- `NodeData`
- `GlobalBlackboard`

## 运行时行为

普通瞬时节点可以覆盖 `Execute()`：

```csharp
public override void Execute(GraphExecutionContext context)
{
    GD.Print(Text);
}
```

FlowGraph 中需要跨帧运行的节点实现 `IFlowNode`。StateGraph 中状态节点实现 `IStateNodeData`。Mission/HFSM 可以在业务层定义自己的接口，但应继续复用 `GraphNodeData` 的序列化和端口声明。

## 子图节点

继承 `SubGraphNodeData` 可获得子图绑定和进入按钮。子类应覆盖：

- `CreateSubGraphAsset()`
- `AcceptsSubGraph(GraphAsset graph)`
- `GetSubGraphTypeName()`

StateGraph 和 HFSM 的 Composite 节点就是参考实现。

## 常见坑

- 节点字段必须可被 `GraphJsonHelper` 处理：public property 或 `[JsonInclude]` 字段。
- 不要在节点数据中保存 Godot 场景节点引用；运行时对象放进 `GraphExecutionContext.UserData`。
- `CreateUI()` 只在编辑器中使用，运行时不能依赖它。
- 修改节点属性后要保存图资源，否则只会留在当前编辑器内存中。

