# 项目通用文档

## 概览

`yu` 是一个 Godot 4.6 + C# 项目，运行时目标是 `.NET 8`，Android 目标平台使用 `.NET 9`。项目整体采用两层组织：

- `scripts/framework`：可复用框架层，提供模块、状态机、资源、UI、事件、配置、对象池等横向能力。
- `scripts/gamelogic`：游戏业务层，提供角色组件、输入、HFSM、技能、AI、任务、存档、流程和 UI。

项目的核心思路是：框架层负责稳定的通用能力，业务层用组件和图资源组织玩法。`addons/GraphPlugin` 提供通用图编辑和运行时能力，HFSM、Skill、Mission、BehaviorTree 等业务系统在它之上定义各自的语义。

## 启动流程

主场景由 `project.godot` 的 `run/main_scene` 指向，核心入口脚本是 `scripts/gamelogic/RootModule.cs`。

启动顺序：

1. `RootModule._Ready()` 创建并初始化 `GameTime`。
2. 创建 `GameState`，并调用 `GameState.Init()`。
3. 设置 `Engine.MaxFps` 和 `Engine.TimeScale`。
4. 通过 `ModuleSystem.GetModule<T>()` 懒加载核心模块。
5. 初始化 `IProcedureModule`，注册 `PreloadProcedure`、`MainMenuProcedure`、`LevelProcedure`。
6. 从 `MainMenuProcedure` 开始进入游戏流程。

每帧运行：

- `RootModule._Process()` 更新 `GameTime`，再调用 `ModuleSystem.Process(...)` 驱动所有实现 `IProcessModule` 的模块。
- `RootModule._PhysicsProcess()` 更新物理时间。
- `RootModule._ExitTree()` 调用 `ModuleSystem.Shutdown()` 反向关闭模块。

## 框架层

框架层位于 `scripts/framework`，默认命名约定是通过接口获取模块。例如 `IFsmModule` 会由 `ModuleSystem` 推导到同命名空间下的 `FsmModule`。

### ModuleSystem

`ModuleSystem` 是框架模块的统一入口，职责包括：

- 按接口懒创建模块实例。
- 管理模块优先级。
- 每帧轮询实现 `IProcessModule` 的模块。
- 退出时按反向顺序关闭模块。

推荐约定：

- 业务代码通过接口获取模块，不直接依赖模块实现类。
- 新模块遵循 `IxxxModule -> XxxModule` 的命名规则。
- 模块之间如果有初始化顺序要求，应在文档中说明，避免隐式依赖变成隐藏问题。

### FSM 和 Procedure

`scripts/framework/fsm` 提供通用有限状态机：

- `IFsm<T>`：状态机对外接口。
- `Fsm<T>`：状态机运行时实现。
- `FsmState<T>`：状态基类。
- `FsmModule`：状态机注册、轮询和销毁。

`scripts/framework/procedure` 基于 FSM 实现主流程：

- `ProcedureBase` 是流程状态基类。
- `ProcedureModule` 持有流程状态机。
- 当前游戏流程由 `MainMenuProcedure`、`PreloadProcedure`、`LevelProcedure` 组成。

### ResourceModule

`scripts/framework/resource` 负责资源加载和生命周期管理。核心结构是：

- `ResourceHandle<T>`：通用资源句柄，保存状态、进度、错误、引用归还。
- `SceneHandle`：`PackedScene` 专用句柄，封装实例化和绑定。
- `GodotResourceLoader`：同步和异步加载。
- `ResourceCache`：LRU 缓存和框架层引用计数。
- `ResourceProfiler` / `ResourceProfilerOverlay`：资源状态观测。

推荐约定：

- 普通资源使用 `LoadAsset<T>()` 或 `LoadAssetAsync<T>()`。
- 只临时读取资源时使用 `LoadAssetOnce<T>()`。
- 场景实例化优先使用 `LoadSceneAsync()` 和 `SceneHandle`。
- 排查资源问题时优先使用 profiler 快照或日志输出。

### UIModule

`scripts/framework/ui` 负责窗口、Widget 和 UI 层级管理。核心结构是：

- `IUIModule`：UI 对外入口。
- `UIModule`：窗口集合、加载、显示、隐藏、关闭、层级计算。
- `UIWindow`：完整窗口，持有自己的 `SceneHandle`。
- `UIWidget`：窗口内复用子组件。
- `[Window]`：声明窗口层级、场景路径、全屏属性。
- `[UIBind]`：通过字段名或路径自动绑定 Godot 节点。

推荐约定：

- 打开窗口统一走 `ShowUI()` 或 `ShowUIAsync()`。
- 关闭窗口用 `CloseUI()`；需要短时间保留状态时用 `HideUI()`。
- 不在业务层绕过 `UIModule` 手动创建窗口节点。
- UI 事件订阅优先使用 `AddUIEvent()`，让生命周期自动清理订阅。

### EventModule

`scripts/framework/event` 提供同步事件派发：

- 使用 `int eventId` 作为事件标识。
- 支持无参和 1 到 4 个泛型参数的 `Action`。
- 回调中订阅或退订由内部脏数据机制收口。

推荐约定：

- 事件 ID 集中放在业务事件定义文件中。
- 跨模块通知可以使用事件；有强依赖的数据流不建议滥用事件隐藏调用链。

### ConfigModule

`scripts/framework/config` 负责配置表加载：

- `ConfigModule` 缓存已加载配置表。
- `JsonConfigLoader` 从配置目录读取 JSON。
- `ConfigTable<T>` 提供按 ID 和全表查询。
- `scripts/generated/config` 存放生成的配置行类型。

推荐约定：

- 不手改 `scripts/generated/config` 下的生成代码。
- 需要扩展配置行为时，通过非生成代码、partial、helper 或生成器侧修改完成。
- 热重载通过 `ReloadTable<T>()` 或 `ReloadAll()`。

### ObjectPoolModule

`scripts/framework/pool/objectpool` 提供两类对象池：

- 纯 C# 对象池：管理实现 `IObjectPoolItem` 的普通对象。
- Node 对象池：通过 `PackedScene` 实例化 Godot Node，回收时隐藏并保留在父节点下。

推荐约定：

- 高频创建销毁的运行时对象优先考虑对象池。
- Node 池统一通过 `IObjectPoolModule` 创建，资源加载交给 `ResourceModule`。

## 游戏层

游戏层位于 `scripts/gamelogic`，负责把框架能力组合成具体游戏行为。

### GameObject 和 Component

`GameObject2D` / `GameObject3D` 是业务对象根节点，组件以 Godot `Resource` 的形式配置在对象上。

核心规则：

- 组件继承 `Component2D` 或 `Component3D`。
- 组件通过 `Priority` 决定更新顺序。
- `GameObject2D` 在 `_Process()` 和 `_PhysicsProcess()` 中按优先级驱动组件。
- 组件之间通过 `Owner.GetComponent<T>()` 获取彼此。

当前角色能力主要分布在 `scripts/gamelogic/abilities`：

- `CharacterMoveComponent2D`：移动意图和水平移动。
- `CharacterJumpComponent2D`：跳跃意图和跳跃处理。
- `CharacterGravityComponent2D`：重力处理。
- `CharacterBodyMotorComponent2D`：`CharacterBody2D` 移动与碰撞提交。
- `SpriteAnimationComponent2D`：动画表现。

### Input

`scripts/gamelogic/input` 对 Godot `InputMap` 做业务封装：

- 支持基础 action 名和带 `|group` 后缀的真实 InputMap 名。
- 支持输入层，如 Global、Combat、UI、Camera。
- 支持输入消费，避免多个系统重复处理同一次输入。
- 支持 buffer、hold time、模拟输入事件。

推荐约定：

- 业务代码使用基础 action 名，不依赖带分组后缀的真实 InputMap 名。
- UI、战斗、相机等系统按输入层消费输入。
- 需要连招、跳跃缓冲等体验时优先使用 InputModule 的 buffer 能力。

### HFSM

`scripts/gamelogic/hfsm` 是基于 `GraphPlugin` 状态图的角色分层状态机。

职责边界：

- HFSM 负责状态切换、blackboard 条件和复合状态；CharacterGraph 负责输入与技能 Action 接入。
- 具体移动、跳跃、技能时间线、伤害结算不放在 HFSM 中。
- Controller 负责写入输入和环境类 blackboard key。

当前角色图资源示例：

- `res://assets/graphs/character_graph.tres`
- `res://assets/graphs/character_locomotion_hfsm.tres`

### Skill

`scripts/gamelogic/skills` 把角色技能拆成资源、运行时、管理组件和 FlowGraph。

核心类型：

- `SkillResource`：技能静态配置。
- `SkillFlowGraphAsset`：技能流程图资源。
- `SkillRuntime`：单个技能在角色身上的运行时状态。
- `SkillManagerComponent2D`：技能注册、cooldown、启动、tick、停止。
- `actions/`：技能 FlowGraph 可执行动作。

当前 Dash 和 Attack 已迁移到 Skill：

- `res://assets/skills/dash_skill.tres`
- `res://assets/skills/dash_flow.tres`
- `res://assets/skills/attack_skill.tres`
- `res://assets/skills/attack_flow.tres`

推荐约定：

- cooldown 属于 `SkillManagerComponent2D`，不要放进 FlowGraph。
- FlowGraph 只描述技能已经开始后的时间线、表现和动作。
- HFSM 中的 Skill 状态只负责判断能否进入、启动技能和根据完成结果返回。

### AI

`scripts/gamelogic/ai` 使用 BehaviorTree 图驱动简单角色 AI。

运行方式：

- `SimpleAICharacterControllerComponent2D` 持有 `BehaviorTreeGraphAsset`。
- 行为树 action 写入本帧移动和跳跃 intent。
- Controller 在帧末将 intent 提交给移动、跳跃和 HFSM blackboard。

推荐约定：

- AI 和玩家 Controller 都只写 intent，不直接改 Motor 结果。
- 具体运动裁决放在能力组件和状态机中。

### Mission

`scripts/gamelogic/missions` 负责任务和任务链。

核心设计：

- `MissionManager<object>` 管理具体 Mission 实例。
- `MissionChainManager` 管理 `MissionGraphRuntime`。
- MissionGraphRuntime 只排队任务部署请求。
- MissionChainManager 统一 drain 部署请求，避免节点进入过程中直接修改任务系统状态。
- `MissionChainSaver` 接入 `SaveModule`，支持保存和恢复任务链运行时。

推荐约定：

- 任务图路径 `graphPath` 不包含 `.`，因为运行时 ID 使用 `graphPath.nodeId`。
- 子图由父 runtime 启动并共享 blackboard fork。
- 任务完成后由 MissionManager 回调驱动图继续推进。

### Save

`scripts/gamelogic/saves` 提供注册式保存模块：

- 实现 `ISaveable` 的对象注册到 `SaveModule`。
- 保存时逐个调用 `Save()` 并序列化状态。
- 读取时反序列化状态并调用 `Load()`。

当前默认保存目录是 `res://saves`。后续发布平台建议迁移到 `user://saves`，并保留旧路径兼容读取。

## GraphPlugin

`addons/GraphPlugin` 是项目内通用图编辑与运行时插件。

它负责：

- 图资源模型：`GraphAsset`、`GraphDocument`、`GraphNodeData`、`GraphConnection`。
- 图序列化：所有图运行数据以 `GraphAsset.GraphJson` 为准。
- 类型注册：`GraphTypeRegistry` 管理图类型、节点类型和连线类型。
- 黑板：图本地黑板、全局黑板、运行时作用域栈。
- 运行时索引：`GraphRuntimeIndex` 加速节点和连线查询。
- 图验证：`GraphValidationService` 和各业务图扩展验证。
- 编辑器：画布窗口、Inspector、搜索、调试面板。

业务层负责：

- 定义业务图类型，如 HFSM、Skill、Mission、BehaviorTree。
- 定义业务节点、action、condition。
- 定义运行时上下文和数据流。

推荐约定：

- 新节点优先继承 `GraphNodeData`，用声明式方法描述端口、分类、搜索关键字和编辑 UI。
- 新图类型通过稳定 `GraphType` 字符串和 `CreateConnection()` 扩展。
- 运行时需要跨子图黑板写入或 Runtime Debug 时，实现 `IGraphRuntimeScope`。
- 不把业务对象塞进 GraphPlugin editor 或 core。

## 开发约定

- 框架能力放在 `scripts/framework`，业务行为放在 `scripts/gamelogic`。
- Godot 脚本类继承 `Node`、`Resource` 等类型时使用 `partial class`。
- 可编辑和需要序列化的字段使用 `[Export]`。
- 项目资源路径使用 `res://`。
- 生成配置不要手工修改。
- 新增图节点时优先补充验证规则和最小 smoke test。
- 修改 C# 代码后运行：

```powershell
dotnet build yu.csproj
```

## 文档入口

- GraphPlugin：`addons/GraphPlugin/README.md`
- GraphPlugin 详细文档：`addons/GraphPlugin/docs/README.md`
- ResourceModule：`scripts/framework/resource/ResourceModule.md`
- UIModule：`scripts/framework/ui/UIModule.md`
- EventModule：`scripts/framework/event/EventModule.md`
- ConfigModule：`scripts/framework/config/ConfigModule.md`
- ObjectPoolModule：`scripts/framework/pool/objectpool/ObjectPool.md`
- InputModule：`scripts/gamelogic/input/InputModule.md`
- HFSM：`scripts/gamelogic/hfsm/README.md`
- Skill：`scripts/gamelogic/skills/README.md`
