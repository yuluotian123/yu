# GraphPlugin 使用说明

GraphPlugin 是一个 Godot C# 图编辑插件。它提供通用的 `GraphAsset` 资源、节点/连线数据模型、编辑器窗口、子图导航、连接编辑、复制粘贴、Undo/Redo、黑板，以及若干扩展点。

## 快速开始

1. 在 Godot Inspector 中选中一个 `GraphAsset` 或其子类资源。
2. 点击 Inspector 顶部的“打开图编辑器”按钮。
3. 在图编辑器里右键空白区域，搜索并添加允许的节点类型。
4. 从节点输出端口拖线到另一个节点输入端口创建连接。
5. 点击 `保存 (Ctrl+S)` 保存图资源。

Graph 数据会保存到资源中的三个 JSON 字段：

- `NodesJson`
- `ConnectionsJson`
- `BlackboardJson`

运行时访问时使用 `GraphAsset.Nodes`、`GraphAsset.Connections`、`GraphAsset.BlackboardEntries`，这些属性会从 JSON 中反序列化出实际列表。

## 核心类型

### GraphAsset

`GraphAsset` 是图资源基类，继承自 `Resource`。

常用成员：

- `GraphType`：图类型名，用于筛选可添加的节点。
- `Nodes`：当前图的节点列表。
- `Connections`：当前图的连线列表。
- `BlackboardEntries`：当前图的 Local Blackboard。
- `CreateConnection()`：创建新连线时调用，可在子类中返回自定义连接类型。
- `GetAllowedNodeTypes()`：控制图编辑器右键菜单里允许添加的节点类型。
- `GetCustomToolbarControls()`：给图编辑器工具栏添加自定义控件。
- `GetEditorTitle()`：控制编辑器窗口标题。
- `primeNode`：优先返回 `EntryNode`，否则返回第一个 `CanBePrime()` 的节点。

示例：

```csharp
[Tool]
[GlobalClass]
public partial class MissionGraph : GraphAsset
{
    public override string GraphType { get; set; } = "MissionGraph";
    public override GraphConnection CreateConnection() => new ConnectionWithConditon();
    public override string GetEditorTitle() => ResourcePath + "_MissionGraph Editor";
}
```

### GraphNodeData

`GraphNodeData` 是图节点数据基类。每个节点会被序列化到 `NodesJson`。

常用覆盖点：

- `GetGraphTypes()`：声明这个节点可用于哪些图类型。
- `GetDisplayName()`：节点标题。
- `GetNodeColor()`：端口颜色。
- `GetInputCount()` / `GetOutputCount()`：端口数量。
- `GetInputMaxConnections(port)` / `GetOutputMaxConnections(port)`：端口最大连接数，`-1` 表示不限制。
- `CanBePrime()`：是否能成为 `GraphAsset.primeNode`。
- `CreateUI(GraphEditorContext context)`：创建节点在编辑器里的自定义 UI，`context.GraphNode` 是当前 Godot `GraphNode`。
- `Execute(GraphExecutionContext context)`：运行时到达节点时可调用的逻辑入口，具体是否使用由业务代码决定。

示例：

```csharp
public class DialogNode : GraphNodeData
{
    public string Text { get; set; } = "";

    public override List<string> GetGraphTypes() => new() { "DialogGraph" };
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;
    public override string GetDisplayName() => "Dialog";

    public override void CreateUI(GraphEditorContext context)
    {
        var edit = new LineEdit { Text = Text, PlaceholderText = "Dialog text" };
        edit.TextChanged += value => Text = value;
        context.GraphNode.AddChild(edit);
    }
}
```

节点注册是自动完成的。插件加载时会扫描所有继承 `GraphNodeData` 的非抽象类，并按 `GetGraphTypes()` 注册到对应图类型。

### GraphConnection

`GraphConnection` 是连线数据基类。每条连接会被序列化到 `ConnectionsJson`。

常用覆盖点：

- `GetDisplayName()`：连线标签文字。
- `IsEditable()`：是否允许右键编辑连接。
- `IsAvailable`：运行时可用性判断，业务运行器可读取。
- `CreateEditUI(GraphEditorContext context)`：右键编辑连接时显示的 UI。
- `CreateConnectionLabel()`：编辑器画布上显示的连接标签。

示例：

```csharp
public class WeightedConnection : GraphConnection
{
    public int Weight { get; set; } = 1;

    public override string GetDisplayName() => $"Weight: {Weight}";

    public override Control CreateEditUI(GraphEditorContext context)
    {
        var spin = new SpinBox { MinValue = 0, MaxValue = 100, Value = Weight };
        spin.ValueChanged += value => Weight = (int)value;
        return spin;
    }
}
```

## 编辑器功能

### 打开图编辑器

选中 `GraphAsset` 或其子类资源后，Inspector 会显示“打开图编辑器”按钮。点击后会打开 `GraphCanvasEditorWindow`。

### 添加节点

在画布空白区域右键，弹出可搜索节点列表。列表来自当前图的 `GetAllowedNodeTypes()`。

默认行为：

- 当前图类型对应的节点会出现。
- `GetGraphTypes()` 返回 `"All"` 的节点会出现在所有图中。
- 选择节点后会在鼠标位置创建节点。

### 创建和删除连接

从一个节点输出端口拖到另一个节点输入端口即可创建连接。

连接创建时会检查：

- 是否已经存在同样的连接。
- 输出端口是否达到 `GetOutputMaxConnections(port)`。
- 输入端口是否达到 `GetInputMaxConnections(port)`。

删除连接：

- 右键点击连接，选择删除。
- 或鼠标悬停在连接附近时按 `Delete`。

### 编辑连接

右键点击连接，选择编辑。如果连接的 `IsEditable()` 返回 `true`，会打开 `CreateEditUI(GraphEditorContext context)` 生成的编辑窗口。

确认后会：

- 刷新连接标签。
- 保存当前图资源。

### 保存、清空、撤销重做

工具栏功能：

- `保存 (Ctrl+S)`：保存当前图资源。
- `清空`：清空当前图节点和连接。
- `Blackboard`：打开黑板编辑面板。

快捷键：

- `Ctrl+S`：保存。
- `Ctrl+Z`：撤销。
- `Ctrl+Shift+Z` 或 `Ctrl+Y`：重做。

当前 Undo/Redo 已接入添加节点、添加连接、删除连接、粘贴节点等编辑行为。

### 复制粘贴

GraphEdit 的复制/粘贴请求已接入。

当前行为：

- 复制选中的节点类型和位置。
- 粘贴时在原位置基础上偏移 `(50, 50)`。

注意：当前粘贴逻辑会创建同类型的新节点，但不会完整恢复自定义字段。代码中已经保存了 `nodeJson`，如果需要完整复制节点数据，可以扩展 Clipboard 逻辑，在粘贴时反序列化并恢复字段。

## 子图

`SubGraphNodeData` 是内置子图节点，`GetGraphTypes()` 返回 `"All"`，因此所有图都可以添加。

子图节点功能：

- 保存一个 `SubGraphPath`。
- 编辑器中会显示“绑定/更换子图资源”按钮。
- 可选择已有 `.tres` 图资源，也可以创建新图资源。
- 绑定后可以点击“进入子图”。
- 进入子图时会自动保存当前图，并显示面包屑导航。
- 返回父图或跳转面包屑节点时，也会先保存当前图。

运行时可通过：

```csharp
GraphAsset subGraph = subGraphNode.GetSubGraph();
```

读取绑定的子图资源。

## Blackboard

GraphPlugin 支持两层黑板：

- Global Blackboard：场景中的 `GraphBlackboardNode`，需要手动挂到当前场景树。
- Local Blackboard：每个 `GraphAsset` 自己保存的局部黑板，数据写入该图资源的 `BlackboardJson`。

读取优先级由 `GraphBlackboardRuntime` 决定：

1. 当前图 Local Blackboard。
2. 父图 Local Blackboard。
3. 场景中的 Global Blackboard。

同名 key 会被更内层的 Local Blackboard 覆盖。

### 编辑黑板

打开任意 `GraphAsset` 的 Graph 编辑器后，工具栏里有 `Blackboard` 按钮。

- `Local` 页：编辑当前图资源的局部黑板，点击 `Save Local Blackboard` 后保存到当前 `GraphAsset`。
- `Global` 页：编辑当前编辑场景里的 `GraphBlackboardNode`。

Global Blackboard 可编辑的条件：

- 当前 Godot 编辑器必须打开一个场景。
- 当前正在编辑的场景里必须存在 `GraphBlackboardNode`。
- `GraphBlackboardNode` 必须是场景树里的节点，不是 Autoload，也不是单独的资源文件。

如果场景里没有 `GraphBlackboardNode`，Global 页只会显示提示，不会创建节点。需要手动在场景树里添加 `GraphBlackboardNode`。

保存 Global Blackboard 时，编辑器会把数据写回该节点的 `BlackboardJson`，并把场景标记为未保存。还需要保存场景，数据才会持久化到 `.tscn` / `.scn`。

### 支持的值类型

内置黑板值类型：

- `GraphBoolBlackboardValue`
- `GraphIntBlackboardValue`
- `GraphFloatBlackboardValue`
- `GraphStringBlackboardValue`
- `GraphVector2BlackboardValue`
- `GraphColorBlackboardValue`

自定义值类型需要继承 `GraphBlackboardValue` 或 `GraphBlackboardValue<T>`，并实现自己的编辑 UI。

示例：

```csharp
public sealed class GraphNodePathBlackboardValue : GraphBlackboardValue<NodePath>
{
    public override string DisplayName => "NodePath";

    public override Control CreateEditUI(GraphEditorContext context)
    {
        var edit = new LineEdit { Text = Value.ToString(), PlaceholderText = "NodePath" };
        edit.TextChanged += value => Value = new NodePath(value);
        return edit;
    }
}
```

### 运行时读取

```csharp
var blackboard = new GraphBlackboardRuntime();
blackboard.PushLocal(graphAsset);

bool canMove = blackboard.GetValue("CanMove", true);
float speed = blackboard.GetValue("Speed", 5f);

if (blackboard.TryGetValue("TargetName", out string targetName))
{
    GD.Print(targetName);
}
```

`new GraphBlackboardRuntime()` 会自动使用当前注册的 `GraphBlackboardNode.Current`。也可以显式传入：

```csharp
var blackboard = new GraphBlackboardRuntime(myBlackboardNode);
```

进入子图时调用：

```csharp
blackboard.PushLocal(subGraphAsset);
```

退出子图时调用：

```csharp
blackboard.PopLocal();
```

### 运行时新增或修改值

写入当前 Local 层：

```csharp
blackboard.SetValue("Speed", 8f);
blackboard.SetValue("CanMove", false);
blackboard.SetValue("SpawnPoint", new Vector2(10, 20));
```

如果当前有 Local 层，`SetValue` 会写到最内层 Local 运行时副本；如果没有 Local 层，则写到 Global Blackboard 节点。

显式写入 Global Blackboard：

```csharp
blackboard.SetGlobalValue("Difficulty", 2);
blackboard.SetGlobalValue("DebugColor", Colors.Red);
```

也可以直接操作节点：

```csharp
GraphBlackboardNode.Current.SetValue("PlayerName", "Yu");
int difficulty = GraphBlackboardNode.Current.GetValue("Difficulty", 1);
```

运行时写入默认只影响内存。`GraphBlackboardNode.SetValue` 不会自动保存场景，`GraphBlackboardRuntime.SetValue` 写入的 Local 层也是运行时副本，不会污染图资源。需要持久化编辑器数据时，请使用黑板面板的保存按钮。

## JSON 序列化

GraphPlugin 使用 `GraphJsonHelper` 序列化节点、连线和黑板值。

特性：

- 每个对象会写入 `$type` 字段。
- 反序列化时会按 `$type` 在当前 AppDomain 中查找具体类型。
- 支持 public 属性。
- 支持标记 `[JsonInclude]` 的字段。
- 支持基础类型、枚举、`Vector2`、`Color`、`List<T>` 和嵌套对象。

使用建议：

- 自定义节点、连线、条件、黑板值尽量提供无参构造。
- 需要持久化的数据使用 public get/set 属性。
- 私有字段需要持久化时加 `[JsonInclude]`。
- 类型名重名会影响 `$type` 查找，尽量避免不同命名空间下有同名可序列化类型。

## 可复用编辑器控件

### SearchablePopup

`SearchablePopup<T>` 是通用搜索弹窗，节点右键添加和类型选择都使用它。

示例：

```csharp
var popup = new SearchablePopup<Type>(
    types,
    type => type.Name,
    type => type.Namespace);

popup.OnItemSelected += type => GD.Print(type.Name);
popup.ShowBelow(button);
```

### ReorderableListControl

`ReorderableListControl<T>` 是通用可排序列表控件。任务图的条件列表、Action 列表等已经在使用它。

支持：

- 展开/折叠列表。
- 展开/折叠单项。
- 上移、下移、删除。
- 从类型列表中添加新对象。
- 每个元素自定义编辑 UI。

示例：

```csharp
var listControl = new ReorderableListControl<ConditionBase>(
    items: Conditions,
    buildItemUi: condition => condition.CreateEditUI(context),
    getItemLabel: condition => condition.GetType().Name,
    availableTypes: SubTypeCache.GetSubTypes<ConditionBase>(),
    factory: type => (ConditionBase)Activator.CreateInstance(type)
);

root.AddChild(listControl.Build());
```

## 创建一种新图

1. 继承 `GraphAsset` 创建图资源类型。
2. 设置唯一的 `GraphType`。
3. 继承 `GraphNodeData` 创建节点类型，并让 `GetGraphTypes()` 返回该 `GraphType`。
4. 如需自定义连接，继承 `GraphConnection`，并在图资源中覆盖 `CreateConnection()`。

最小示例：

```csharp
[Tool]
[GlobalClass]
public partial class DialogGraph : GraphAsset
{
    public override string GraphType { get; set; } = "DialogGraph";
    public override string GetEditorTitle() => "Dialog Graph Editor";
}

public class DialogLineNode : GraphNodeData
{
    public string Text { get; set; } = "";

    public override List<string> GetGraphTypes() => new() { "DialogGraph" };
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;
}
```

`GraphEditorContext` 会随编辑器 UI 创建传入，常用字段包括：

- `CurrentGraph`：当前正在编辑的图资源。
- `RootGraph`：从子图导航进入时的根图资源。
- `GraphEdit`：当前 Godot `GraphEdit`。
- `GraphNode`：当前正在创建 UI 的 Godot `GraphNode`。
- `NodeData`：当前节点数据。
- `Connection`：当前正在编辑的连接。
- `GlobalBlackboard`：当前编辑场景中的 `GraphBlackboardNode`，不存在时为 `null`。
- `BlackboardEntry`：当前正在编辑的黑板条目。

## 运行时遍历图

GraphPlugin 本身只提供数据结构和编辑器，不规定具体运行方式。业务系统可以按自己的规则解释节点和连接。

一个简单遍历示例：

```csharp
GraphNodeData current = graph.primeNode;
var blackboard = new GraphBlackboardRuntime();
blackboard.PushLocal(graph);
var context = new GraphExecutionContext(graph, blackboard);

while (current != null)
{
    current.Execute(context);

    GraphConnection nextConnection = graph
        .GetOutgoingConnections(current.Id)
        .FirstOrDefault(connection => connection.IsAvailable);

    if (nextConnection == null)
        break;

    current = graph.FindNodeById(nextConnection.ToNode);
}
```

如果图里使用子图，可在遇到 `SubGraphNodeData` 时调用 `GetSubGraph()`，再进入子图的运行逻辑。

## GameLogic HFSM

HFSM 是 GameLogic 层对 GraphPlugin 的一个使用范例和业务扩展，说明放在 `scripts/gamelogic/hfsm/README.md`。

## 当前注意事项

- `GraphBlackboardNode` 不是 Autoload，需要手动挂到场景中。
- Global Blackboard 只有在当前编辑场景中存在 `GraphBlackboardNode` 时才可编辑。
- 运行时写黑板默认不持久化到资源或场景文件。
- 复制粘贴节点目前不会完整恢复自定义字段。
- `GraphJsonHelper` 通过类型名查找 `$type`，请避免可序列化类型重名。
