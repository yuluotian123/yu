# GraphPlugin

GraphPlugin 是项目内的通用图编辑与运行时框架，基于 Godot 4 C# 实现。它统一处理图资源、节点与连线、黑板、子图、验证、序列化和编辑器工具；HFSM、Skill、Mission 等具体业务语义放在 `scripts/gamelogic/` 中扩展。

README 用于快速了解和接入插件。架构、扩展规则与工作流细节请查看 [`docs/`](docs/README.md)。

## 主要能力

- **Flow Graph**：从入口节点开始传播，支持顺序/并行连线、条件、Delay、Action、Return 和 Timeline。
- **State Graph**：支持状态进入/更新/退出、触发器、Any State、完成转换、优先级、状态标签和 Composite State。
- **Behavior Tree**：提供 Root、Composite、Decorator、Condition、Action 节点以及有序子节点执行。
- **Blackboard**：支持图本地、父图和场景全局作用域，以及父子 Runtime 间的变量访问。
- **SubGraph**：支持编辑器内子图导航，以及 Flow、State 和业务 Runtime 的父子运行时结构。
- **Validation**：在保存或启动前检查节点、连线、端口、入口和黑板等结构问题。
- **Editor Tools**：包含节点搜索、Inspector、Explorer、黑板、Timeline、复制粘贴、Undo/Redo 和子图导航。
- **Runtime Scope**：通过 `IGraphRuntimeScope` 统一描述父子 Runtime，供跨子图黑板操作和后续调试工具扩展。

## 目录与职责

```text
addons/GraphPlugin/
├── GraphPlugin.cs          # Godot EditorPlugin 入口
├── editor/                 # 仅编辑器使用的窗口、面板、控件与服务
├── runtime/
│   ├── core/               # 通用图模型、注册、序列化、验证与运行时索引
│   ├── blackboard/         # 本地、父级和场景全局黑板
│   ├── flow/               # Flow Graph 资源、节点、连线与 Runtime
│   ├── state/              # State Graph 资源、状态节点、转换与 Runtime
│   └── behavior_tree/      # Behavior Tree 资源、节点与 Runtime
└── docs/                   # 架构、扩展和工作流专题文档
```

边界约定：

- `runtime/core/` 只保存通用图能力，不依赖具体玩法或角色逻辑。
- `editor/` 只负责 Godot 编辑器体验，不进入游戏运行时语义。
- Flow、State、Behavior Tree 只实现各自通用执行规则。
- HFSM、Skill、Mission 等项目业务图放在 `scripts/gamelogic/`，通过继承插件资源或 Runtime 接入。
- Action、Condition、Task 等业务对象应保持可复用，不应直接写进编辑器窗口或 core。

## 快速开始

### 1. 启用插件

插件配置位于 `addons/GraphPlugin/plugin.cfg`，项目已在 `project.godot` 中启用该插件。若在其他项目中接入，需要在 Godot 的 **Project Settings → Plugins** 中启用 `GraphPlugin`。

### 2. 创建图资源

在 Godot FileSystem 面板中创建资源，并选择需要的资源类型：

- `FlowGraphAsset`
- `StateGraphAsset`
- `BehaviorTreeGraphAsset`
- 项目业务层提供的 `HfsmGraphAsset`、`SkillFlowGraphAsset` 或 `MissionGraph`

选中资源后，Inspector 顶部会显示 **Open Graph Editor**。进入编辑器后创建节点、连接端口、配置黑板，并通过工具栏保存。

> 图资源以 `GraphAsset.GraphJson` 作为唯一图数据源。不要手工同步另一套节点或连线字段。

### 3. 在运行时执行图

#### Flow Graph

```csharp
[Export] private FlowGraphAsset _flowGraph;

private FlowGraphRuntime _flowRuntime;

public override void _Ready()
{
    _flowRuntime = new FlowGraphRuntime(_flowGraph);
    _flowRuntime.Start();
}

public override void _Process(double delta)
{
    _flowRuntime?.Update(delta);
}

public override void _ExitTree()
{
    _flowRuntime?.Stop();
}
```

#### State Graph

```csharp
[Export] private StateGraphAsset _stateGraph;

private StateGraphRuntime _stateRuntime;

public override void _Ready()
{
    _stateRuntime = new StateGraphRuntime(_stateGraph);
    _stateRuntime.Start();
}

public override void _PhysicsProcess(double delta)
{
    _stateRuntime?.Update(delta);
}

public void Jump()
{
    _stateRuntime?.Trigger(Jump);
}

public override void _ExitTree()
{
    _stateRuntime?.Stop();
}
```

`Start()` 默认使用 `StateGraphAsset.InitialStateName` 或图中的默认状态，也可以传入状态名称。运行期间可使用 `ChangeState()`、`CurrentStateHasTag()` 和黑板 API。

#### Behavior Tree

```csharp
[Export] private BehaviorTreeGraphAsset _behaviorTree;

private BehaviorTreeRuntime _behaviorRuntime;

public override void _Ready()
{
    _behaviorRuntime = new BehaviorTreeRuntime(_behaviorTree);
    _behaviorRuntime.Start();
}

public override void _PhysicsProcess(double delta)
{
    BehaviorTreeStatus status = _behaviorRuntime.Update(delta);
}

public override void _ExitTree()
{
    _behaviorRuntime?.Stop();
}
```

Behavior Tree 需要一个有效的 `BehaviorRootNodeData`，子节点顺序由 `BehaviorTreeConnection.Order` 决定，并以节点位置作为稳定排序补充。

### 4. 使用上下文与黑板

需要向节点传入角色、组件或业务服务时，创建 `GraphExecutionContext`：

```csharp
var context = new GraphExecutionContext(graph);
context.UserData.Add(actor);

var runtime = new FlowGraphRuntime(graph, context);
runtime.Start();
```

Runtime 提供统一黑板入口：

```csharp
runtime.SetValue(Target, target);
Node3D currentTarget = runtime.GetValue<Node3D>(Target);
runtime.SetGlobalValue(AlertLevel, 2);
```

默认查询顺序为当前图本地黑板、父图本地黑板、场景全局 `GraphBlackboardNode`。详细作用域规则见[黑板作用域](docs/blackboard-scope.md)。

## 扩展插件

### 创建节点

新节点优先继承 `GraphNodeData` 或对应图类型的节点基类，并通过声明式方法提供：

- 稳定的 `NodeType`。
- 节点显示名、分类和搜索关键字。
- 输入/输出端口及最大连接数。
- 可使用该节点的 `GraphType` 列表。
- 必要的节点编辑 UI 和运行时接口。

完整步骤与示例见[创建节点](docs/create-node.md)。

### 创建图类型

新图类型应：

1. 继承 `GraphAsset`、`FlowGraphAsset` 或 `StateGraphAsset`。
2. 定义不会随类名重构而改变的 `GraphType` 常量。
3. 覆盖 `CreateConnection()` 返回该图使用的连线类型。
4. 注册 `GraphTypeDefinition`，并让节点声明允许使用的图类型。
5. 将具体运行语义放在独立 Runtime 中，不修改通用编辑器窗口。

完整步骤见[创建图类型](docs/create-graph-type.md)。

### 接入父子 Runtime

管理子图的 Runtime 应实现 `IGraphRuntimeScope`。这样可以自动接入：

- 父子 Runtime 遍历。
- 跨子图黑板写入。
- 后续运行时调试工具所需的统一遍历入口。

运行时接入方式见[运行时接入](docs/runtime.md)。

## 保存、验证与序列化

- `GraphAsset.GraphJson` 保存 `GraphDocument`，其中包含 schema、图类型、节点、连线、黑板和编辑器状态。
- 编辑器保存时会把当前文档写回 JSON；运行时从同一字段反序列化，不维护旁路数据源。
- Runtime 的 `Start()` 会验证图结构，验证失败时拒绝启动并输出问题。
- 节点、连线、Action、Condition 和黑板值依赖多态类型信息；重命名序列化类型时必须注册旧名称到新名称的别名。
- 修改 schema 时应增加显式迁移流程，不能只修改当前反序列化代码。

格式和兼容规则见[序列化格式](docs/serialization.md)。

## Runtime Debug 状态

现有专题文档中保留了基于 `EngineDebugger`、`GraphRuntimeDebugRegistry` 和编辑器调试面板的架构设计，但当前代码树中没有对应实现，`runtime/debug/` 目录也是空的。因此这些 API 目前不能直接使用，不应按旧文档示例接入。

如果后续恢复该功能，应优先复用已有的 `IGraphRuntimeScope` 父子 Runtime 模型，并同步更新运行时、编辑器面板和专题文档。

## 当前注意事项

- 不要绕过图编辑命令随意修改 `Nodes`、`Connections` 或 `BlackboardEntries`；修改后必须确保调用 `MarkDirty()`，否则运行时索引可能没有及时重建。
- `$type` 当前依赖 CLR 类型名称。新增类型时避免同名类；重命名已有类型前先设计别名或资源迁移。
- Runtime 热路径应避免每帧创建临时集合；新增查询优先复用 `GraphRuntimeIndex`。
- 图资源应在运行前通过验证，不要假设编辑器保存过的资源一定合法。
- 插件仍保留少量 `GraphCanvas` 历史命名；新代码和文档统一使用 `GraphPlugin`，除非引用现有类型名。
- `docs/` 中的 Runtime Debug 内容超前于当前实现，使用前必须先核对对应代码是否存在。

## 文档导航

推荐按以下顺序阅读：

1. [文档索引](docs/README.md)
2. [整体架构](docs/architecture.md)
3. [文件夹结构](docs/folder-structure.md)
4. [创建图类型](docs/create-graph-type.md)
5. [创建节点](docs/create-node.md)
6. [编辑器架构](docs/editor-architecture.md)
7. [序列化格式](docs/serialization.md)
8. [运行时接入](docs/runtime.md)
9. [编辑器工作流](docs/workflow.md)
10. [黑板作用域](docs/blackboard-scope.md)
