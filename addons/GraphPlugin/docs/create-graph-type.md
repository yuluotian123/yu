# 创建图类型

图类型负责定义“这个图允许哪些节点、默认创建什么连线、编辑器标题是什么、运行时如何执行”。V2 不需要创建新的 document 类，通常继承 `GraphAsset`、`FlowGraphAsset` 或 `StateGraphAsset` 即可。

## 最小图类型

```csharp
using Godot;

[Tool]
[GlobalClass]
public partial class DialogGraphAsset : GraphAsset
{
    public const string GraphTypeName = "DialogGraph";

    public override string GraphType
    {
        get => GraphTypeName;
        set { }
    }

    public override string GetEditorTitle() => "Dialog Graph Editor";
}
```

如果不覆盖 `CreateConnection()`，图会使用普通 `GraphConnection`。

## 自定义连线类型

```csharp
public sealed class DialogConnection : GraphConnection
{
    public string ConditionKey { get; set; } = string.Empty;

    public override string GetDisplayName()
        => string.IsNullOrWhiteSpace(ConditionKey) ? "Always" : $"if {ConditionKey}";
}
```

```csharp
public override GraphConnection CreateConnection()
{
    return new DialogConnection();
}
```

保存和运行前，`GraphValidationService` 会检查当前图里的连线类型是否符合 `CreateConnection()` 的返回类型。

## 注册图类型

编辑器插件启动时会调用 `GraphTypeRegistry.AutoRegisterAll()` 自动扫描节点。图类型本身如果有特殊默认连线，也可以显式注册：

```csharp
GraphTypeRegistry.RegisterGraphType(new GraphTypeDefinition
{
    GraphType = DialogGraphAsset.GraphTypeName,
    DisplayName = "Dialog Graph",
    CreateConnection = () => new DialogConnection()
});
```

显式注册适合插件内置图类型。业务图类型也可以只通过 `GraphAsset.CreateConnection()` 覆盖，减少全局启动代码。

## 控制允许节点

默认 `GraphAsset.GetAllowedNodeTypes()` 会从 `GraphTypeRegistry` 查询所有声明支持当前 `GraphType` 的节点。复杂图类型可以覆盖：

```csharp
public override List<string> GetAllowedNodeTypes()
{
    var result = new List<string>();
    result.AddRange(GraphTypeRegistry.GetNodeTypeNamesForGraphType("FlowGraph"));
    result.AddRange(GraphTypeRegistry.GetNodeTypeNamesForGraphType(GraphTypeName));
    return result.Distinct(StringComparer.Ordinal).ToList();
}
```

`SkillFlowGraphAsset` 就采用这种方式复用 FlowGraph 节点，同时允许 Skill 专用节点。

## 现有图类型参考

- `FlowGraphAsset`：通用流程图，默认普通连线。
- `StateGraphAsset`：状态图，默认 `StateTransitionConnection`。
- `HfsmGraphAsset`：HFSM 图，复用 StateGraph 语义，默认 `HfsmTransitionConnection`。
- `SkillFlowGraphAsset`：技能流程图，复用 FlowGraph 节点。
- `MissionGraph`：任务链图，基于 FlowGraph，默认 `FlowConnection`。

## 常见坑

- `GraphType` 必须是稳定字符串，资源里会保存它。
- 图类型重命名后，旧资源需要迁移 `GraphJson.GraphType`。
- 自定义连线类型必须有无参构造，才能被反序列化。
- 图类型不应该持有编辑器窗口状态，缩放和滚动已经由 `GraphDocument.EditorState` 保存。
