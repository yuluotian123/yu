# 项目优化路线图

本文档汇总截至 2026-07-26 对项目插件、框架模块和主要业务模块的静态审查结果，包括 GraphPlugin。优化项按风险和收益排序；性能类建议应先测量再实施，不能仅凭代码形态判断为实际瓶颈。

## 优先级

- **P0：正确性与数据安全**。可能造成资源无法加载、索引错误、存档损坏或文档误导。
- **P1：生命周期与稳定性**。降低异步、热重载、模块依赖和异常传播风险。
- **P2：性能与可维护性**。减少热路径分配、反射成本和大型类耦合。
- **P3：工程体验**。统一命名、日志、设置、文档和验证流程。

## P0：正确性与数据安全

### GraphPlugin：为图 JSON 增加可恢复错误边界

**涉及文件**：

- `addons/GraphPlugin/runtime/core/GraphJsonHelper.cs`
- `addons/GraphPlugin/runtime/core/GraphAsset.cs`

**问题**：

- `JsonNode.Parse()`、枚举转换、反射赋值和对象创建异常会直接向调用层传播。
- 错误缺少资源路径、JSON 字段和目标类型上下文，编辑器难以定位损坏资源。
- `EnsureDocument()` 失败时没有保留“加载失败”状态，调用方无法区分空图和坏图。

**建议**：

- 增加 `TryDeserialize<T>(..., out T value, out GraphSerializationError error)`。
- 错误对象包含资源路径、schema、JSON path、目标类型和内部异常。
- 编辑器打开坏图时进入只读恢复界面，保留原始 JSON，不自动覆盖。
- Runtime 在验证前发现反序列化失败时拒绝启动并输出单条结构化错误。

**验收**：损坏 JSON、未知枚举、缺失类型和错误字段类型均不会导致编辑器崩溃，也不会覆盖原资源。

### GraphPlugin：稳定多态类型标识

**涉及文件**：

- `addons/GraphPlugin/runtime/core/GraphJsonHelper.cs`
- `addons/GraphPlugin/runtime/core/GraphTypeRegistry.cs`
- `addons/GraphPlugin/docs/serialization.md`

**问题**：`$type` 当前写入 CLR 简单类名；不同命名空间同名类会互相覆盖，类重命名也会破坏旧资源。

**建议**：

- 为序列化类型定义稳定 ID，不直接使用 `Type.Name`。
- 注册表同时保存稳定 ID、CLR 类型、历史 alias 和冲突诊断。
- schema 升级时迁移旧简单类名，并保留可回滚备份。
- 注册重复 ID 时立即失败，不采用后注册覆盖。

**验收**：同名类型可共存，类型重命名后旧图仍可迁移加载。

### GraphPlugin：消除运行时索引失效窗口

**涉及文件**：

- `addons/GraphPlugin/runtime/core/GraphAsset.cs`
- `addons/GraphPlugin/runtime/core/GraphDocument.cs`
- `addons/GraphPlugin/editor/services/GraphCommandService.cs`
- `addons/GraphPlugin/editor/services/GraphSnapshotService.cs`

**问题**：`Nodes`、`Connections` 和 `BlackboardEntries` 暴露可变 `List`。外部直接修改并漏调 `MarkDirty()` 时，`GraphRuntimeIndex` 会继续返回旧数据。

**建议**：

- 对外暴露 `IReadOnlyList<T>`。
- 增加 `AddNode`、`RemoveNode`、`ReplaceDocument`、`AddBlackboardEntry` 等集中变更 API。
- 每次结构变更统一递增 revision，并让索引记录构建 revision。
- 编辑器 Undo/Redo 和快照恢复只通过变更 API 操作。

**验收**：任何图结构修改后查询结果立即一致，并有测试验证绕过 API 不再可能。

### Save Module：迁移到可写目录并使用原子保存

**涉及文件**：

- `scripts/gamelogic/saves/SaveModule.cs`
- `scripts/gamelogic/saves/ISaveModule.cs`

**问题**：当前存档写入 `res://saves`，导出项目中通常不可写；直接覆盖目标文件时，中断可能留下损坏 JSON。

**建议**：

- 默认写入 `user://saves/{slot}.json`。
- 读取时兼容旧 `res://saves`，成功后可迁移到新位置。
- 先写临时文件，flush/close 成功后替换目标文件，并保留最近备份。
- 校验 slot 名称，禁止路径分隔符和目录逃逸。
- 顶层加入存档 schema version、游戏版本和时间戳。

**验收**：桌面和 Android 导出环境可保存；写入中断后旧存档仍可读取；旧路径可以兼容迁移。

### ConfigPlugin：避免半生成状态

**涉及文件**：

- `addons/ConfigPlugin/ConfigConverterWindow.cs`
- `scripts/framework/config/converter/XlsxConverter.cs`
- `scripts/framework/config/converter/CSharpCodeGenerator.cs`

**问题**：JSON 与 C# 文件分别直接写入目标目录；转换中途失败可能只更新其中一份。部分类型处理异常被空 `catch` 忽略。

**建议**：

- 转换分为读取、验证、生成内存结果、写临时目录、原子替换五阶段。
- 一批文件全部验证成功后再提交输出。
- 空 `catch` 改为带表名、字段名和类型字符串的明确错误。
- 生成结果附加 schema/generator version，运行时 loader 校验兼容性。

**验收**：任意单表失败时不修改已存在输出；同一输入可稳定产生相同结果。

### 修正文档中不存在的 Runtime Debug 能力

**涉及文件**：

- `addons/GraphPlugin/docs/`
- `addons/GraphPlugin/runtime/debug/`
- GraphPlugin、HFSM、Skills、Mission README

**问题**：旧专题文档描述了 `GraphRuntimeDebugRegistry`、`EngineDebugger` bridge 和编辑器面板，但当前代码树没有对应实现，`runtime/debug/` 为空。

**建议**：

- 短期把旧专题文档标记为设计稿/未实现，禁止作为当前 API 指南。
- 若决定恢复功能，先定义最小快照协议，再同时实现 runtime、bridge、editor store 和 panel。
- 若不恢复，删除空目录和失效文档，保留 `IGraphRuntimeScope` 作为通用父子 Runtime 接口。

**验收**：所有入口文档都能明确区分“当前可用”和“规划设计”。

## P1：生命周期与稳定性

### ModuleSystem：显式化模块依赖和初始化失败处理

**涉及文件**：`scripts/framework/core/ModuleSystem.cs`

**问题**：模块通过接口名称懒创建，依赖顺序分散在首次调用位置；`OnInit()` 失败时缺少注册回滚；`Type.GetType()` 对程序集拆分敏感。

**建议**：

- 引入启动期模块清单或依赖声明，统一构建初始化顺序。
- 先创建候选实例，`OnInit()` 成功后再加入正式注册表。
- 初始化失败时反向关闭本次创建的依赖模块。
- 为显式注册和自动发现增加重复、缺失与错误类型诊断。

**验收**：模块依赖可从一个入口查看；初始化异常后可以再次启动而不残留静态状态。

### GraphPlugin：插件进入/退出与注册表热重载幂等

**涉及文件**：

- `addons/GraphPlugin/GraphPlugin.cs`
- `addons/GraphPlugin/runtime/core/GraphTypeRegistry.cs`

**问题**：`_ExitTree()` 无条件移除 Inspector plugin；静态注册表只有 `_scanned` 开关，没有明确刷新、清理和重复注册策略。

**建议**：

- `_EnterTree()` / `_ExitTree()` 对部分初始化和重复调用保持幂等。
- 移除 Inspector plugin 前检查实例有效性。
- 注册表提供 `ResetForReload()` 或按程序集 revision 刷新。
- 重复 GraphType/NodeType 注册输出来源类型和冲突原因。

### Resource Module：统一异步取消与 owner 生命周期

**涉及文件**：`scripts/framework/resource/`

**问题**：异步句柄、SceneHandle 和 Node owner 的退出顺序复杂；调用方可能在 owner 已销毁后收到完成结果。

**建议**：

- 为异步请求增加取消 token 或显式 `Cancel()`。
- 提供 `BindTo(Node owner)`，owner 退出时自动取消并释放句柄。
- 明确 handle 的完成、失败、取消、释放状态转换和幂等规则。
- 相同路径并发加载共享底层任务，但每个调用方拥有独立引用句柄。

**验收**：场景退出、重复释放、取消后完成和加载失败均不会泄漏引用或触发迟到回调。

### UI Module：处理异步打开竞态

**涉及文件**：

- `scripts/framework/ui/UIModule.cs`
- `scripts/framework/ui/core/UIBase.cs`

**问题**：窗口加载期间可能收到重复 Show、Hide、Close 或场景退出；反射绑定失败可能延迟为空引用。

**建议**：

- 为每个窗口维护 `Closed/Loading/Visible/Hidden/Closing` 状态。
- `ShowUIAsync()` 返回可取消请求或窗口 handle。
- 关闭加载中窗口时取消资源请求，并保证完成回调不会重新挂载。
- `UIBind` 在创建阶段集中验证，错误包含窗口、字段、目标类型和节点路径。

### Event Module：隔离处理器异常并增加归属解绑

**涉及文件**：`scripts/framework/event/`

**问题**：同步回调中单个订阅者异常可能阻断后续订阅者；整数 ID 与载荷签名没有统一注册约束。

**建议**：

- 为事件 ID 保存首次注册的 delegate signature，后续不一致时立即报错。
- 根据项目策略选择逐处理器异常隔离或 fail-fast，并在 README 中固定。
- 增加 subscription handle 或 owner 绑定，Node 退出时自动解绑。
- 提供只读调试快照，显示事件 ID、签名和订阅数量。

### Input Module：固定 action 分组与消费语义

**涉及文件**：

- `scripts/gamelogic/input/InputModule.cs`
- `scripts/gamelogic/input/InputLayer.cs`

**问题**：action 解析、分组、层级、采样、消费、buffer 和模拟输入集中在大型实现中，字符串规则难以发现和验证。

**建议**：

- 把 InputMap 解析、帧状态采样和消费策略拆为独立组件。
- 启动时验证 action 名称与分组规则，并输出一次性报告。
- 集中定义 action/layer 常量或生成类型化键。
- 明确暂停、时间缩放、物理帧和普通帧对 buffer/hold time 的影响。

### Config Module：防止同表并发重复加载

**涉及文件**：`scripts/framework/config/ConfigModule.cs`

**问题**：同步加载、异步加载、预加载和重载共享缓存，需保证同一表的并发请求不会重复创建或互相覆盖。

**建议**：

- 使用按表类型保存的 in-flight task。
- 明确 reload 期间读取旧表还是等待新表。
- `SetLoader()` 后要求显式 reload，或阻止存在已加载表时切换。
- 加载完成后统一执行重复 ID、引用字段和必填字段验证。

### Mission Module：补齐生命周期 API

**涉及文件**：

- `scripts/gamelogic/missions/IMissionModule.cs`
- `scripts/gamelogic/missions/MissionModule.cs`
- `scripts/gamelogic/missions/core/MissionChainManager.cs`

**问题**：公共接口只有 `StartChain()` 且无返回值；资源失败、重复启动和任务链状态无法查询。`Process()` 当前为空但模块仍加入帧更新列表。

**建议**：

- `StartChain()` 返回 result/runtime ID，包含资源和验证失败原因。
- 增加 `StopChain`、`HasChain`、`GetChainState` 和重复启动策略。
- 如果无需逐帧更新，移除 `IProcessModule`；否则把 Runtime 更新明确放入 `Process()`。
- 启动前验证 MissionGraph、任务原型、子图路径和存档兼容 ID。

## P2：性能与可维护性

### GraphPlugin：减少反射扫描和序列化元数据开销

**涉及文件**：

- `addons/GraphPlugin/runtime/core/GraphTypeRegistry.cs`
- `addons/GraphPlugin/runtime/core/GraphJsonHelper.cs`
- `addons/GraphPlugin/editor/controls/ReorderableListControl.cs`

**观察**：类型注册阶段遍历 AppDomain 两次，JSON 转换每次重新枚举 property/field，编辑器控件也独立扫描类型。

**建议**：

- 单次程序集扫描生成共享 type catalog。
- 缓存每个类型的序列化成员、setter/getter 和创建函数。
- 编辑器搜索控件从注册表读取类型，不重复扫描 AppDomain。
- 记录扫描耗时和类型数量，确认优化收益。

### GraphPlugin：优化 Runtime 热路径集合分配

**涉及文件**：

- `addons/GraphPlugin/runtime/flow/FlowGraphRuntime.cs`
- `addons/GraphPlugin/runtime/state/StateGraphRuntime.cs`
- `addons/GraphPlugin/runtime/behavior_tree/BehaviorTreeGraphAsset.cs`
- `addons/GraphPlugin/runtime/core/GraphRuntimeIndex.cs`

**观察**：更新和传播路径包含 `Where`、`ToList`、`OrderBy` 与按端口过滤产生的临时集合。

**建议**：

- 索引构建时缓存 `(nodeId, port)` 的连接表。
- State transition 按优先级预排序；Behavior child link 按 order/位置预排序。
- Flow 传播复用 scratch list，或直接遍历只读索引。
- 优化前后使用真实图规模做 profiler 对比。

### 拆分大型职责类

优先候选：

- `scripts/gamelogic/input/InputModule.cs`
- `scripts/framework/utility/JsonHelper.cs`
- `scripts/framework/fsm/Fsm.cs`
- `scripts/framework/resource/ResourceModule.cs`
- `addons/GraphPlugin/runtime/flow/FlowGraphRuntime.cs`
- `addons/GraphPlugin/runtime/state/StateGraphRuntime.cs`
- `addons/GraphPlugin/editor/panels/GraphTimelinePanel.cs`

拆分原则：按数据索引、生命周期、执行策略、序列化、UI 展示分离，不按文件行数机械拆分；先补测试，再移动代码。

### FSM：收敛重复重载和异常策略

**涉及文件**：`scripts/framework/fsm/`

**问题**：按泛型/Type、命名/匿名组合产生大量重复校验和通用 `Exception`。

**建议**：

- 内部统一到 `TypeNamePair` 核心路径。
- 为查询提供 `TryGetFsm`，为创建冲突提供明确异常类型。
- FSM data 从字符串/object 逐步迁移到类型化 key。
- 增加单次 update 内的最大切换次数，防止状态循环。

### Resource Cache：从条目数升级为可观测预算

**涉及文件**：`scripts/framework/resource/cache/ResourceCache.cs`

**问题**：缓存容量按条目数量限制，不同资源大小差异很大。

**建议**：

- 保留条目数上限，同时记录近似资源类型、命中率和驻留时间。
- 根据 profiler 数据决定是否需要内存预算，而不是立即实现复杂估算。
- 区分“缓存引用”和“业务 handle 引用”，在 profiler 中明确展示。

### Object Pool：明确活跃对象所有权

**涉及文件**：`scripts/framework/pool/objectpool/`

**问题**：模块关闭时只能安全释放闲置 Node，活跃 Node 的回收责任在调用方，容易形成退出期泄漏或跨场景引用。

**建议**：

- 可选提供 owner 绑定池，owner 退出时回收或销毁其全部实例。
- 为对象记录所属 pool，拒绝跨池回收和重复回收。
- profiler 输出 active/idle/overflow/released 数量。

### UI：缓存反射绑定元数据

**涉及文件**：`scripts/framework/ui/core/UIBase.cs`

**建议**：按窗口类型缓存 `[UIBind]` 字段、路径和赋值器；首次绑定时完整验证，后续仅执行节点查找和赋值。使用局部 warning suppression 处理 `CS0649`，避免压制真实警告。

## P3：工程体验与一致性

### 统一 GraphPlugin 历史命名

**涉及文件**：

- `addons/GraphPlugin/GraphPlugin.cs`
- `addons/GraphPlugin/plugin.cfg`
- `GraphCanvas*` 编辑器类

短期统一日志和描述为 GraphPlugin；类名迁移需要评估 Godot `.uid`、序列化和资源引用，不做无迁移重命名。

### 统一日志入口

`scripts/framework/utility/Logger.cs` 中的 `Debugger` 与 .NET 类型同名。建议重命名为 `GameLogger` 或引入 `ILogger`，支持分类、等级、一次性警告和测试捕获。

### Settings 命名与加载入口

`scripts/framework/settings/Settings.cs` 的公开属性使用小写命名，且缺少统一加载/验证流程。建议改为 C# PascalCase，并通过兼容导出字段或迁移脚本保护 Godot 资源。

### 工程编码与生成目录卫生

- 统一 `.cs`、`.md` 为 UTF-8。
- 修复现存乱码注释时只重写注释，不进行大范围格式 churn。
- `scripts/generated/config` 只由生成器修改。
- `tmp/` 浏览器缓存不应进入代码搜索、构建输入或版本控制。
- README、代码和 `plugin.cfg` 的版本描述保持一致。

### 文档入口

- 每个框架模块和插件 README 说明职责、快速开始、边界、生命周期和限制。
- `docs/PROJECT_OVERVIEW.md` 作为项目总览。
- 本文档作为跨模块优化清单。
- 专题文档描述规划能力时必须标记“未实现”，不能使用当前时态。

## 测试建设

### P1 核心测试

- ModuleSystem：发现、显式注册、优先级、初始化失败回滚、Shutdown 后重启。
- Config：同步/异步同表加载、重复 ID、loader 切换、生成/读取兼容。
- Event：签名冲突、回调内订阅/解绑、异常处理策略。
- FSM/Procedure：状态生命周期、数据、重复创建、循环切换、restart 失败。
- Resource：成功、失败、取消、重复释放、共享请求、缓存引用计数。
- Pool：重复回收、跨池回收、owner 销毁、overflow。
- UI：绑定成功/失败、重复打开、加载中关闭、全屏遮挡、延迟销毁。
- Input：分组解析、优先级、同级消费、buffer、hold、模拟输入。
- Save：原子写入、损坏恢复、schema migration、非法 slot、旧路径迁移。
- Mission：图验证、任务完成推进、子图、保存恢复、重复启动。

### GraphPlugin 专项测试

- JSON 多态往返、未知类型、同名类型、alias 和 schema migration。
- 节点/连线直接与命令式变更后的索引一致性。
- Flow sequence/parallel/return 和最大传播步数。
- State any/completion/priority/composite/return。
- Behavior Tree root、child order、abort 和 decorator。
- Blackboard 当前/父级/全局作用域。
- 插件启用、禁用和 C# 热重载 smoke test。

## 推荐实施顺序

1. Save 迁移与原子写入。
2. Graph JSON 错误边界、稳定类型 ID 和索引一致性。
3. 修正 Runtime Debug 失效文档。
4. ConfigPlugin 原子生成和错误报告。
5. ModuleSystem 初始化回滚与依赖清单。
6. Resource/UI 异步取消和 owner 生命周期。
7. 补核心自动化测试。
8. 基于 profiler 决定 Graph、Input、Resource 的性能优化。
9. 收敛命名、日志、Settings 和大型类职责。

## 验证基线

```powershell
dotnet build yu.csproj
git diff --check
```

涉及 Godot 编辑器插件、UI、异步资源、图运行时和输入时，还需要对应场景 smoke test。当前环境如果无法访问 NuGet 或缺少 `Godot.NET.Sdk/4.6.1`，应记录为环境阻塞，不能据此判断代码构建失败。
