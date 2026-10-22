# NPC 世界旅行、活动与离屏模拟配置

更新：2026-10-23。自主跨关卡旅行、路段恢复、离屏事件推进、活动上下文和远处候选已接入现有角色图。Goal、手动 Plan、目标选择器和 Action 的职责保持不变。

## 运行结构

```text
WorldDefinition
  ├─ Levels → 保存关卡自动生成对象、入口、角色目录
  └─ Routes → 关卡间道路、方向、知识和权限

WorldPopulation：唯一角色身份、逻辑位置、旅行和事件队列
  ├─ 现场：原角色 Scene，物理移动和区域交互
  └─ 远处：轻量 EC 代理，同一角色数据、GOAP、Action Graph

活动上下文 → Goal → 手动 Plan 的行动集合
目标选择器 → 本次计划固定身份 → Action 目标输入
移动 → 路段推进 → 到达 → 公共对象操作 → 成功时提交结果
```

远处代理只装配 WorldActor、CharacterData 和 AiAgent，不实例化完整角色 Scene，也不加载远处关卡。代理仍是 EC 宿主，不是零成本的纯数据记录。世界层负责调度与显示实例，不另建一套生活 AI。

## 1. 角色 Scene

1. 在可复用 NPC Scene 的 GameObject3D 根上添加 `WorldActorComponent3D`。已有 `CharacterDataComponent3D.Profile`、`AiAgentComponent3D.Graph`、移动、导航和表现组件继续使用。
2. 每个长期角色需要唯一且稳定的 `PersistentId`。同一个角色不能同时摆在多个关卡；复用外观 Scene 制作另一个角色时，在实例上给它不同的角色身份。世界对象的自动摆放身份规则不变。
3. `ScenePathOverride` 通常留空，自动记录 NPC 的 Scene 路径；需要指定用于恢复表现的角色 Scene 时才填写。该 Scene 也必须具有 WorldActor。
4. 设置 `DetailedDistance=30`、`AbstractDistance=45`、`MinimumResidencySeconds=2`，或根据镜头调整。详细距离必须小于抽象距离，二者构成迟滞；驻留时间防止反复切换。
5. 将角色 Scene 拖入登记关卡并保存。编辑器自动生成角色名册、初始关卡和位置、速度、图、Profile 与角色参数覆盖。角色目录只保存角色资源引用和 Scene 路径，不加载角色 Scene。

启用 WorldActor 后，角色数据、位置、Agent 和检查点统一进入世界 population 存档，运行时自动关闭这几个组件自己的重复存档注册。未添加 WorldActor 的原有现场 NPC 继续走原有存档和 LOD，不自动获得世界旅行能力。

## 2. 关卡入口和道路

先完成 [世界配置与关卡切换](README.md) 中的地点、关卡、对象与入口配置。在路线两端关卡各放一个 GameObject3D，添加 `WorldSpawnPointComponent3D`：

| 字段 | 例子 | 含义 |
| --- | --- | --- |
| `Entry` | `town_road` | 关卡内唯一入口键 |
| `MarkerPath` | `Arrival` | 子节点 Marker3D 的路径，保存时编译其关卡坐标 |

在 `WorldDefinition.Routes` 添加 `WorldRouteDefinition`：

| 字段 | 配置 |
| --- | --- |
| `Id / DisplayName` | 稳定道路身份及名称，例如 `town_to_workshop` |
| `From / To` | 起点和终点 WorldLevelDefinition |
| `FromEntry / ToEntry` | 两端的 Entry 键；必须存在于已保存关卡目录 |
| `TravelHours` | 正数，世界游戏小时，例如 `0.25` 表示 15 游戏分钟 |
| `Bidirectional` | 是否允许双向通行 |
| `Enabled` | 静态启用状态 |
| `Public` | 公开路线；关闭后角色必须在 KnownRoutes 中知道它 |
| `Permission` | 可选角色表达式，例如钥匙、任务事实或角色权限 |
| `UseExplicitPositions` | 通常关闭；特殊数据制作才开启并填写两端坐标 |

路线图与地点树分别配置。可连接 A→B→C；系统按旅行小时找可通行路径，另计角色到入口、途经关卡不同入口之间、出口到目标的本地移动时间。不会直接相减不同关卡的坐标。本地移动估算按目录速度；现场实际移动仍受导航约束，估算不保证绕障路径精确。

运行时用 `world.Routes.SetOpen(routeId, false)` 封路，`true` 重新开放；关闭状态进入存档。旅行推进时重新验证当前路段；封路或权限失效会使当前行动失败并重新规划。非公开路线在角色 `CharacterWorldDefinition.KnownRoutes` 配置初始知识，也可通过运行时知识授予。

## 3. 选择器、Plan 与 Action

以“去工坊取两份物资”为例：

1. 选择器 Source 选“世界目录”，Scope 选“整个世界”或“当前活动地点”。Information 根据角色设计选择已知对象、公开服务或获准的权威查询；目录本身不授予知识。
2. 筛选储物箱 Type、所需能力／操作和标签。需要取货时使用 `take_stock`，不能只填写一个尚未实现的操作名。
3. 同一个选择器节点同时连接“前往物资箱”和“取物资”的目标输入。本次计划固定具体对象，两个 NPC 的绑定相互独立。
4. “前往”Action Graph 使用原有 `NpcMoveToTargetAction`；预测位置为输入目标，成功条件检查 `input.target.at`。移动节点会选择本地导航或跨关卡旅行，无需另做一个“远处移动”Action。
5. 移动成本可用 `input.target.travel_hours`。普通距离上限只适合已知本地距离；跨关卡距离是未知值，启用 `MaximumDistance` 会排除它。
6. “取物资”前提检查已抵达、库存足够；目标输入资源要求加 `capacity`，执行图请求 `take_stock`，数量设为 2。需要等待则在交互前加等待节点。
7. Goal 配置完成条件，手动 Plan 选择“前往”和“取物资”两个 Action。没有自动使用全部 Action，也不需要 Plan 变量。
8. `TimeoutSeconds` 要覆盖道路时间换算后的执行秒数、入口间移动与等待。换算为 `游戏小时 × DayLengthSeconds / 24`；恢复不会重新计满超时。

候选新增成员：

| 成员 | 语义 |
| --- | --- |
| `level` | 对象所属关卡 |
| `distance` | 同关卡可选空间距离；跨关卡为 unknown |
| `travel_hours` | 可用路线的预计游戏小时，含本地接驳；没有路线或位置时为 unknown |
| `route_known` | 当前是否有获准路线和可定位目标 |
| `executable` | 当前后端是否具备可用操作能力；具体 Action 仍单独检查操作 |
| `observation_age` | 最后观测距离模拟当前时刻的游戏小时 |

候选内部另外记录成本来源，区分本地导航估计、语义本地估计、世界路线和未知路线。支持的语义操作单独列入候选 Operations；对象同时有现场专用操作时，不妨碍其余受支持操作在远处运行。已知身份不等于已知库存：规划按知识权限或仍有效的观测读取，未知值不替换成 0。观测保存来源和寿命；到期后不作为实时事实。

## 4. 活动上下文

继续在角色图的 Schedule.Windows 创建 `NpcScheduleWindow`：

| 字段 | 例子和规则 |
| --- | --- |
| `WindowId` | `work_shift`，稳定且唯一 |
| `StartHour / EndHour` | 8 / 17；支持跨午夜，相等表示全天 |
| `Tags` | 原有日程标签，仍可用于条件表达式 |
| `Priority` | 重叠活动优先级；相同优先级按稳定身份排序 |
| `Location` | 工坊地点；限制其自身与子地点中的对象 |
| `LocationBinding` | 可选角色地点绑定键，例如 workplace；填写后优先于 Location |
| `Goal` | 当前活动允许的 Goal，必须连接在角色图中；留空沿用原有 Goal 选择 |
| `FallbackGoal` | 主 Goal 被阻塞时执行的回退 Goal，同样需要配置 Plan 和 Action |
| `AllowedOperations` | 例如 take_stock；空列表表示不额外限制 |
| `CommitmentSeconds` | 最短保持时间，避免普通优先级变化来回切换 |
| `Interruptible` | 关闭时普通切换等当前行动完成后再进行 |
| `Emergency` | 紧急活动可打断承诺和不可中断设置 |
| `Activation` | 可选角色条件；不满足时该时段不成为候选 |

活动提供上下文和约束，具体顺序仍由 Goal、Plan 和 Action 决定。可在表达式中读取 `activity.id / activity.place / activity.active`。例如工坊上班活动选择工作 Goal、工坊范围与取货操作；晚间活动选择休息 Goal、住所绑定和床位选择器。没库存或路线不可达时，主 Goal 进入失败冷却，回退 Goal 可等待或做其他工作。

任务和紧急事件也复用这一层：向 Agent 调用 `RequestActivity(new NpcActivityRequest(...))`，填写 Goal 身份、地点、优先级和绝对到期游戏小时。可附加 Operations、CommitmentSeconds、Interruptible、Emergency 和 Fallback；`CancelActivity(id)` 撤销。每个角色最多保留 16 个请求，过期清理，并写入存档。请求 Goal 和回退 Goal 需要已存在于角色图，地点必须有效。

## 5. 离屏执行、保存和精度交接

- 代理禁用原生逐帧更新，由世界事件队列按到达、等待完成、日程边界、需求阈值、失败冷却和活动请求推进。无法从任意表达式推导下一事件时，以一游戏分钟为保守检查间隔，不宣称所有自定义条件都能精确预测触发时刻。
- 当前可恢复且可离屏运行的执行图是串行移动、等待、数值计算和公共交互。库存和液体内置操作使用同一状态与提交入口；自定义操作必须显式支持 `SupportsSemantic`，并实现不依赖现场组件的业务检查与提交。
- 技能、动画事件、物理战斗、自定义副作用和复杂分支图没有通用语义后端。现场仍可运行，转为远处后取消并重规划；不会猜测它们的结果。休息可使用等待＋成功 Results，仍按本次实际等待完成发放收益。
- 世界名册保存唯一角色位置、关卡、当前路段、路段进度、接驳剩余时间、下次事件、角色数据、活动、剩余行动与提交标记。租约、路径、Flow 栈和 Node 引用不保存，恢复时重新验证和申请。
- 现场与远处共享对象库存和使用容量。远处长等待使用随行动释放的语义租约，不会因普通 5 秒租期过期而错误共享床位；取消、失败和精度交接都会释放旧租约。
- 玩家进入 NPC 所在关卡且足够靠近时，异步加载原角色 Scene，交接数据和行动检查点。离开详细范围后再转代理；每次 Process 最多 4 次表现切换。逻辑名册压制已经迁走的初始摆放，避免原关卡再次生成同一个人。
- 主动关注可调用 `Population.RequestInterest(actorId, seconds)`：最多 16 个关注身份、单次寿命不超过 120 秒。关注只能恢复当前关卡角色，不会额外加载远处关卡。
- 快进触发分批补算期间暂停现场角色和玩家操作，避免玩家在过去事件尚未完成时修改同一库存；补算完成自动恢复。大于 0.05 游戏小时的时钟跳变进入补算状态。需要倒退时间时读取存档，不在活动会话直接减去时钟。
- 关闭游戏不会按现实离线时长补算。需要快进时推进世界时钟，事件队列处理到目标时间。世界时间、对象和所有角色应在同一个原子存档中一起恢复。

检查点细节见 [NPC 行动检查点与恢复](../ai/NPC_ACTION_CHECKPOINTS.md)。

## 6. 预算、诊断和验收

`Population.EventsPerFrame` 默认 128；`MillisecondsPerFrame` 默认 2 ms，是 Process 的事件切片软预算。单个事件不可抢占，可能越过时间预算；异步 Scene 完成后的实例化也不能由此保证低于 2 ms。现场规划、导航、射线继续使用各自预算。

F8 世界页展示角色名册、Detailed／Distant／Transit、逻辑关卡、积压和事件耗时；NPC 页可看当前活动、目标绑定和行动恢复事件。角色显示候选采用关卡空间格及有限关注集合，不每帧扫描整个人口寻找附近角色；初次建立代理、完整存档和诊断统计仍与总角色数相关。

构建后运行：

```powershell
dotnet build yu.csproj --no-restore
& '<Godot Mono 引擎路径>' --headless --path . --rendering-method gl_compatibility --audio-driver Dummy res://scripts/tools/world/world_simulation_validation.tscn
```

2026-10-23：44 项通过，覆盖三关卡两段路线、嵌套入口坐标、角色实例配置覆盖、新游戏日历起点、活动公式编辑器、途中保存、继续游戏和实时读档、离屏工作、容量竞争、到达生成、返回原关卡防重复、观测到期、封路／路线知识、活动承诺／紧急中断／回退及有界补算。临时夹具、日志与存档位于 `.godot/world-simulation-validation/`。

本机 headless 基础测试：24 名 NPC、快进 4 游戏小时、每切片最多 4 事件，全部取货完成，库存从 60 变为 12；手动采样的事件切片 P95 约 **1.09 ms**，population 序列化文本约 **18.3 KiB**。这只测简单串行旅行与取货，不包括完整人群玩法、渲染、显存、全部存档和 Scene 实例化峰值。默认 128 事件的预算需在目标硬件另测，不能据此承诺数千角色容量。

这些 headless 验证仍有退出时 ObjectDB／资源未释放的引擎诊断，原有回归也能观察到；本次不将测试通过视为完整泄漏验收。

本次不增加多关卡同时加载、重复住宅／地下城实例、运行时长期角色生成、工具／材料复杂事务和任意执行图自动抽象。现有登记角色、静态世界对象与单活动关卡已形成完整可配置闭环。
