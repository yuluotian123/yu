# 世界、关卡与 NPC 模拟配置手册

更新：2026-10-24，合并世界制作、关卡切换、旅行、活动与离屏模拟说明。行为图连线见 [NPC 与 AI 手册](NPC_AI_GUIDE.md)，系统边界见 [架构与后续工作](ARCHITECTURE_AND_ROADMAP.md)。

[文档入口](README.md) · [目录](#1-建立目录) · [关卡切换](#2-配置关卡与入口) · [对象与交互](#3-按类型能力配置对象) · [角色知识](#4-配置角色世界知识) · [NPC 旅行与离屏](#npc-旅行活动与离屏模拟) · [性能](#状态与性能) · [验证](#验证)

世界入口是 [default_world.tres](../assets/resources/world/default_world.tres)。`WorldModule` 持有共享目录、长期对象状态和唯一玩家；`AiWorldComponent3D` 为现场关卡及离屏代理分别提供环境与规划上下文。当前同时激活一个游戏关卡。

## 先运行现成示例

运行项目，在主菜单选择“新游戏”。城镇入口直接使用 `assets/scenes/levels/forest_town_level.tscn`；走进 `WorkshopPortal` 进入工坊，工坊 `TownPortal` 返回城镇。正式 Resident 已接入世界角色名册，可跨关卡喝水与取货，详见 [完整配置示例](NPC_DAILY_GOALS_EXAMPLE.md)。

进入工坊储物箱或瓶子的交互区域后按 **E**。储物箱每次使用 1 单位物资，瓶子每次倒出 0.25 单位液体。一个操作直接执行；多个操作先显示列表，可点击按钮，或方向键选择后按 E 确认。库存示例表示物资消耗，尚未定义可放入背包的物品；倒液体也不附带饮用效果。

F8 →“世界与关卡”可选择目标关卡和入口，查看对象已加载／未加载／已销毁／记录缺失，以及 NPC 最近一次查询。F8 →“NPC”可修改现场库存和开放状态；F8 →“存档”保存槽。重新启动后选择“继续游戏”，回到保存的关卡及位置。新游戏清理会话中的旧状态，磁盘槽在显式保存时写入。正常游戏按 Esc 关闭会话并返回菜单。

也可 F6 运行带上下文的独立关卡；上下文建立测试会话并异步创建玩家。关卡不能再内嵌玩家。

项目开启物理光照单位。工坊的 WorldEnvironment 配有独立 CameraAttributesPhysical（ISO 100、f/16、1/100 秒），匹配 100,000 lux 的方向光；新增关卡也需配置曝光，否则默认光照可能使画面全白。工坊没有天空资源，环境补光使用 Color 来源。

## 1. 建立目录

世界配置保存在 `assets/resources/world/`。日常从 Scene 制作：关卡记录、默认地点、引用的对象类型和传送门路线由下一节的导出按钮登记。只在需要地点层级、公开服务或特殊道路时额外编辑资源。新记录的稳定 Id 自动生成并保存；显示名称可修改。运行中的目录视为只读，修改配置后重新进入世界。

| 类型 | 制作内容 |
| --- | --- |
| WorldLocationDefinition | 导出时自动创建默认地点；按需扩展 Parent、Tags、PublicServices |
| WorldLevelDefinition | 导出时自动绑定 ScenePath、Location 和 DefaultEntry（默认 `default`） |
| WorldObjectTypeDefinition | 默认标签、可选能力列表 Capabilities，各能力自己的默认参数 |
| WorldLevelCatalog | 编辑器自动生成的关卡对象目录；不手动编辑 |
| WorldDefinition | 导出自动登记上述数组与 Routes；首次导出设置 InitialLevel，PlayerScene、Calendar 按需配置 |

在世界入口的检查器点击“校验世界目录”。身份重复、父级循环、缺失归属、非法容量或库存均会报错。Id、名称和场景路径职责不同：重命名对象不会改变存档身份；移动关卡文件后需要更新 ScenePath。

目录不引用 PackedScene，不会为查对象而加载目标关卡。地点是语义范围，关卡是加载单位。装饰和临时表现无需登记为长期对象。

## 2. 配置关卡与入口

日常制作可直接使用关卡根节点 `WorldLevelContextComponent3D` 的 **“导出关卡配置”** 按钮：

1. 先把关卡保存为 Scene，放好出生点及 Arrival。新关卡的 Level 可以留空。
2. 点击导出。编辑器创建或复用关卡记录，自动绑定当前 Scene，必要时创建默认地点，登记关卡引用的对象类型和地点，并保存场景、入口／对象／NPC 目录。只有一个出生点时自动采用它；多个出生点需要有与 DefaultEntry 匹配的入口。
3. 两端关卡分别导出后，在现有 `WorldTransitionComponent3D` 选择 TargetLevel 和 TargetEntry。再导出一次，会根据自动触发的门生成 NPC 单向路线；返回门生成返回路线，无需另建 Route 资源或手填 Routes 数组。
4. 门的 `NpcTravelHours` 默认 0，表示即时跨门；需要抽象道路耗时才设置游戏小时。NPC 仍会花时间走到门口和从落点走到目标。出发位置使用门下的 UsePosition 标记，未提供时使用门宿主位置；应放在导航可达处。目的地使用 TargetEntry 对应的 Arrival。
5. 首次导出之后，保存关卡、运行和导出游戏会继续同步自动路线。移动门或目标入口会更新坐标，改名保持身份，复制门获得独立身份，删除门移除它生成的路线。

正式小镇与仓储间已按此流程从空登记表重新生成：2 个关卡、2 个默认地点、3 个对象、1 名世界 NPC、2 个入口和 2 条门路线。旧手工路线已移除。本次清理改变了关卡和地点身份，体验新配置请选择“新游戏”；正常重复导出保持身份稳定。

关卡／默认地点资源保存在世界配置所在目录的 `levels/`、`locations/`；自动路线作为世界配置的内嵌资源保存。重复导出更新原记录；将已有场景另存为新关卡会创建独立关卡记录，避免改掉原关卡的路径。场景尚未保存时，按钮会提示先保存。

文件按场景名命名，例如 `levels/forest_town_level.tres`、`locations/forest_town_level_location.tres`、`generated/forest_town_level_catalog.tres`。同名场景首次导出时使用 `_2`、`_3` 等后缀避免覆盖；后续导出复用原文件。检查器显示“森林小镇 · 关卡”“森林小镇 · 默认地点”“森林小镇 · 世界目录”，路线名称包含起点、终点和门名。稳定 Id 保存在资源内部，不需要记住 GUID。修改显示名称不会改变文件路径或身份。

同方向、同目标入口已有手工路线时，保留它的时长、开放、知识和权限设置，不再生成旁路。`Automatic=false` 的事件控制门不自动生成 NPC 路线，避免绕过任务条件；未设置目标的门会在导出结果中提示并跳过，配置完整后再运行。目标关卡应先导出，目标入口缺失会报错。生成目录和身份仍由编辑器维护。

以下为现有组件的详细配置，首次使用按钮后不必再手动创建和登记关卡记录：

1. 关卡根使用 `GameObject3D`，Components 添加 `WorldLevelContextComponent3D`，Level 选择已登记记录。
2. 关卡包含一个带 `AiWorldComponent3D` 的 GameObject3D。EnvironmentPath 指向天气宿主；没有天气组件时降雨为零。
3. 创建出生点 GameObject3D，添加 `WorldSpawnPointComponent3D`。Entry 填稳定入口键，在 MarkerPath 指定原生 Marker3D（默认 `Arrival`）。标记的位置及朝向决定落点。
4. 使用导航的关卡开启 RequireNavigation，并设置 NavigationRegionPath。切换会等待导航就绪，缺失或超时回退。仅玩家测试且无导航的关卡可关闭。
5. 创建门 GameObject3D，添加 `WorldTransitionComponent3D`。配置 TargetLevel、TargetEntry、AreaPath，Area3D 的碰撞掩码包含玩家物理层。Automatic 开启时玩家进入触发；关闭时由交互调用 `Activate()`。

每个关卡的入口键、宿主 PersistentId 和交互点身份必须各自唯一。入口不能靠同名节点猜测。切换会保留同一个玩家实例，同步物理、导航与镜头，并提供 750 ms 落地重触发保护。重复请求被拒绝；取消只作用于准备阶段。提交安排在 ProcessFrame，避免从 Area3D 物理回调直接修改物理树。

入口的钥匙、任务进度与角色权限等业务条件后续由任务图实现，本组件不增加独立条件字段。需要任务图控制的门应关闭 Automatic，再由任务图判断后调用 Activate，避免区域自动触发绕过业务规则；任务图的具体接线仍属后续工作。

新游戏、继续游戏和关卡切换统一显示 `level_loading_window.tscn` 加载界面，展示目的地、进度和加载阶段。进度结合场景与角色资源加载、场景交接及就绪检查；物理和导航准备完成后才显示 100% 并关闭。加载期间暂停玩家控制，失败、取消或关闭会话时清理界面并恢复原有控制状态。界面由 `LevelLoadingWindow` 和 UI 模块管理，位于游戏及调试面板之上。

## 3. 按类型能力配置对象

对象配置只有两层：Type 声明能力与默认参数；Scene 上的 WorldObjectComponent3D 保存显示名称、标签、所有者、可选地点和能力覆盖，其他组件实现现场能力。目录条目由编辑器生成。`WorldObjectState` 只建立已声明能力的状态，没有“所有对象都有库存、营业时间”的模型。

| 能力资源 | 类型默认字段 | 现场组件 | 状态 |
| --- | --- | --- | --- |
| WorldInteractionCapability | Operations：对象提供的操作；声明 `point` 适配器 | WorldInteractionComponent3D | Area3D 范围、遮挡检查与导航参考位置在场景配置 |
| WorldStockCapability | InitialAmount | WorldStockComponent3D | Amount |
| WorldOpeningHoursCapability | OpenHour、CloseHour | WorldOpeningHoursComponent3D | ManualOpen 可选覆盖 |
| WorldUsageCapacityCapability | Slots | WorldUsageCapacityComponent3D | 临时容量租约，不恢复旧租约 |
| WorldLiquidCapability | VolumeLimit、InitialLiquid、InitialVolume | WorldLiquidComponent3D | Liquid、Volume |

能力全部可选。同一种能力在一个类型中只能出现一次；WorldObjectComponent3D.Overrides 只能覆盖 Type 已声明的同类能力。适配器支持由能力声明，例如 `interaction` 提供 `point`，类型上不再有单一 Adapter 字段。

### 示例配置

| 对象 | Type.Capabilities | 场景组合 |
| --- | --- | --- |
| 固定休息点／床 | Interaction、UsageCapacity | 世界对象绑定、交互点、使用容量 |
| 储物服务点 | Interaction、Stock、UsageCapacity、OpeningHours | 世界对象绑定、交互点、库存、容量、开放时段 |
| 仅记录内容的瓶子 | Liquid | 世界对象绑定、液体容纳 |
| 可操作瓶子（工坊示例） | Liquid、Interaction | 世界对象绑定、液体容纳、交互区域 |

库存和液体余量是不同能力。瓶子没有营业时间、库存或使用名额。只有需要角色操作时才增加 Interaction；业务能力不会自动产生操作。拾取、背包物品、交易和休息的完整业务仍由对应系统及操作扩展。

### 创建可复用对象：只配置一次

1. 创建或复用类型，例如 `types/storage.tres`；导出关卡时会登记场景引用的类型。展开 Capabilities，设置库存 InitialAmount、容量 Slots、开放时段等默认参数。相同开／关时间表示全天开放，支持跨午夜。
2. 创建以 GameObject3D 为根的 Scene，Components 添加 WorldObjectComponent3D，ObjectType 选择类型。编辑器补齐类型声明的能力组件；保存前也会同步并验证。不再填写 Record、LocalType 或对象 Id。
3. 填写 DisplayName、ExtraTags、OwnerId；Location 可留空，摆入关卡后继承关卡默认地点。需要特殊初值时，在“能力覆盖”选择类型已有能力，点击“添加所选能力覆盖”，展开 Overrides 修改复制后的资源；覆盖不会改动 Type。
4. 添加模型与实体碰撞。类型声明 Interaction 时，增加 `InteractionArea` Area3D 和 CollisionShape3D；另加 `UsePosition` Marker3D，作为导航参考，放在交互范围内、实体碰撞外。
5. 在 WorldInteractionComponent3D 设置 InteractionAreaPath、UsePositionPath、RequireLineOfSight、OcclusionMask。默认路径分别为 `InteractionArea`、`UsePosition`。没有参考节点时 UsePositionPath 留空。
6. 保存为独立 Scene，例如现成的 [storage.tscn](../assets/scenes/world/storage.tscn) 和 [bottle.tscn](../assets/scenes/world/bottle.tscn)。以后直接拖进已登记关卡并保存即可。

工坊把同一个 storage.tscn 摆了两次。模板库存覆盖为 50，两份实例分别从 50 开始，消耗其中一份不会改变另一份。存档恢复优先于初值，重载关卡不会重新补发。瓶子仅声明液体与交互能力，没有营业时间或使用名额。

### 自动身份、复制与模板更新

- 拖入模板、Ctrl+D 复制、复制整个房间模板，均自动获得独立摆放身份。嵌套 Scene 内的对象按各层摆放键组合，不要求开启 Editable Children。
- 保存、运行及导出前会同步配置和目录。编辑器监听节点新增、场景与资源保存；没有逐帧扫描项目的烘焙任务，也不需要手动创建、绑定或烘焙按钮。
- 节点改名、移动坐标、保存后重开、更新模板内容不会重新分配既有摆放键；撤销／重做恢复相同实例的键。节点名、路径、坐标不充当身份。
- 目录生成使用独立场景快照，保留编辑器已打开 Scene 的组件资源。导出关卡的生成目录位于世界配置旁的 `generated/<关卡文件名>_catalog.tres`；这是自动产物，与 Scene 一起提交 Git，不在 Inspector 手动维护。运行时只读目录，不实例化未加载关卡。
- 选择其他类型或修改同一 Type 的能力列表后，编辑器会同步；保存模板或后台标签页时也会补齐／移除现场能力提供者，保留仍适用的组件参数；先删除不属于新类型的 Overrides。模型、碰撞、原生节点及路径仍由模板配置。
- 单个摆放需要不同名称、所有者、地点或覆盖时，将该实例的 Components 数组与 WorldObjectComponent3D 资源设为独立，再修改实例；保留 ObjectType 共享。只改变状态不需要分离任何配置。
- 摆放键属于其所在 Scene 的制作层级。把对象移到另一层 Scene 实例内部，或删除后重新拖入，属于新的摆放；不要手改 `_world_placement` 元数据。
- 运行时动态创建的对象仍须由生成系统提供持久身份；自动目录覆盖已保存 Scene 的静态摆放。纯装饰不挂世界对象组件。

目录使用 WorldCatalogEntry 纯 C# 条目，既没有手工对象 Resource，也没有到 Scene／Node 的反向引用。身份冲突和缺失配置会阻止构建／关卡激活，运行时不会随机换号。关卡文件移动后仍需更新 Level.ScenePath；复制整份关卡文件时应在新关卡重新摆放长期对象，目录校验会报告重复身份。

### 配置操作与交互区域

在 `Type.Capabilities` 的 WorldInteractionCapability 中展开 Operations，添加 WorldInteractionDefinition 的具体子类。每个操作填写稳定 Id 和显示名称；Scene 需要不同操作时，通过 Overrides 覆盖整个 Interaction 配置。复制覆盖资源时使用深复制，连同嵌套操作资源一起分离，不会修改原对象的参数。

| 操作资源 | 参数 | 必需能力 | 示例 Id |
| --- | --- | --- | --- |
| WorldTakeStockInteraction | Amount，有限正数 | stock | take_stock |
| WorldConsumeLiquidInteraction | Amount，有限正数 | liquid | pour_liquid |

RequireCapacity 表示此操作必须申请使用名额；开启时类型必须具有 usage_capacity。没有容量要求的瓶子不需要此能力。校验会拒绝重复／空操作、非法数量、缺少所需能力或缺少区域形状。

区域支持 Box、Sphere、Cylinder、Capsule，以及多个 CollisionShape3D 的并集，支持节点的旋转和缩放。禁用形状不参加判断。角色的世界位置必须位于至少一个启用形状内。区域不用物理碰撞层或 BodyEntered 事件判定权限，因此示例关闭 Monitoring 并将碰撞层／掩码设为 0；实体碰撞仍单独配置。UsePosition 仅用于自动移动，玩家与 NPC 都可以从区域的其他位置执行交互。

玩家场景挂载 PlayerWorldInteractionComponent3D，并连接 WorldInteractionHUD 的 Panel、Hint、Message、Choices 节点。InputAction 为 `player_interact`，InputMap 使用物理 E 键。目标提示每 0.1 秒更新，使用区域覆盖的空间格子查询；执行时立即重新检查。背包的 GameplayBlocked、GM 的输入层以及其他 UI 焦点都会阻止玩家操作。按住 E 不重复执行。

玩家与 NPC 都通过 `WorldInteraction.Check / Execute`。统一检查角色身份、当前关卡、已加载实例、对象状态、所有权、区域、遮挡、能力、容量和业务余量。执行临时申请容量并在结束后释放；角色已经持有的租约保留给原行动管理。操作失败不能产生部分业务修改。

NPC 执行图添加“执行对象交互”（NpcInteractAction），Input 选择本行动连接选择器的目标输入，操作按名称选择，Amount 直接填写本次数量或公式。固定数量 5 不需要新增 Action 普通输入。需要与等待等节点共用计算结果时，在本执行图声明 Float 局部变量，再用“计算并保存图内数值”赋值，公式通过 `local.<变量名>` 读取；每个角色、每次执行独立。完整步骤见 [NPC 手册：数量、时长与图内计算](NPC_AI_GUIDE.md#数量时长与图内计算)。

图节点先验证并登记包含数量的请求，行动成功且完成最终校验后提交到同一个公共入口。取消／失败不提前修改对象，同一次请求重复提交不重复消耗。数量仅属于本次请求，不会修改共享对象操作的默认 Amount；玩家 E 交互继续使用对象默认值。一个行动最多提交一个操作；多个操作拆成多个行动。交互操作及库存／液体消耗预测从执行图自动提取，同一目标不再重复配置 Predictions；Results 只写人物状态。普通输入仍可传入业务数据，固定值和计算值优先在 Graph 配置。

数量必须是有限正数，未获准读取的资源值仍为 Unknown；不符合操作要求的候选在路径估算和截断前排除，执行与提交仍重新验证当前余量、区域和权限。当前没有库存／液体的提前预留，相关资源在提交时扣减；Action 输入的资源要求仍只有使用容量。包含图内数值的行动目前要求单图、无并行且可证明交互必经成功路径，复杂分支通过多个 Action 表达。

新增操作继承 WorldInteractionDefinition，声明 RequiredCapabilities，提供自己的参数、校验以及 Check／Apply；WorldInteractionContext 同时提供 Actor 和 Target。Check 只读，Apply 必须保证失败时不修改业务状态。操作不是 EC 组件，无需为每个按钮再挂一个组件。

内置数量操作通过 ConsumedProperty 声明 `stock`／`liquid` 规划成员，WorldInteractionContext.Amount 为可选的本次请求数量；未提供时使用对象默认 Amount。当前图内自动消耗预测支持这两种成员，新增业务操作需要同时提供对应的预测与校验能力，不能只填写一个新 Id。

### 新增一种能力

继承 WorldCapabilityDefinition，定义稳定 Key、ComponentType、可选 Adapter、配置校验及状态工厂；状态继承 WorldCapabilityState，实现版本、Capture 和 Restore；现场行为继承 WorldCapabilityComponent3D。将配置加入 Type.Capabilities，并同步现场组件即可。世界存档根据能力集合捕获状态，不需要给所有对象添加新字段。对 NPC 提供新的目标类型仍需注册相应适配器和执行能力。

世界分区 `world/session` 已升为 schema 3，按对象／能力键／能力版本保存。旧 schema 1／2 直接拒绝，请从新游戏开始；不提供旧字段或旧存档转换。容量租约在取消、完成、失败时释放，读档后重新申请。

工坊瓶子示例新增了 Interaction 能力，旧示例存档的能力集合与当前定义不同，也应从新游戏开始。

## 4. 配置角色世界知识

在 `CharacterDataComponent3D.Profile.World` 新建 `CharacterWorldDefinition`。

| 字段 | 示例 |
| --- | --- |
| Locations | `home` → 住宅地点，`work` → 工坊地点 |
| Objects | `preferred_bed` → 自动生成的床对象 Id（字符串） |
| KnownLocations | 初始知道的地点 |
| KnownObjects | 初始知道的具体对象 |
| KnownRoutes | 初始知道的非公开路线身份 |

Objects 与 KnownObjects 的检查器按对象名称、关卡和身份后缀提供列表；Objects 的绑定键输入后按 Enter 确认。选择器的固定对象列表也读取同一生成目录，无需复制 Id。

具名绑定自动授予对应身份的初始知识；它不申请容量，也不表示当前看见。运行时知识、绑定变化及最后观测随角色数据保存；WorldActor 统一写入世界 population，普通现场角色保存在 `character_data` 分区。感知只处理同关卡附近对象，并记录观测时间；最后观测不自动作为实时库存使用。

知道旅店不等于知道里面每张床。地点的 PublicServices 是明确公开的服务标签；选择器启用“公开服务”后，可查询已知地点中匹配这些标签的对象。私有对象应避免配置成公开服务标签。

## 5. 配置选择器 → Action → Plan

全部目标输入继续通过选择器节点连线，同节点在本 NPC、本次计划共享选中身份。Plan 手动选择 Action 集合，不需要配置运行时计划，也不恢复 Plan 变量。

| 候选来源 | 配置及权限 |
| --- | --- |
| 角色对象绑定 CharacterBinding | ParameterKey 从 Profile.World.Objects 选择，例如 `preferred_bed` |
| 世界目录 WorldDirectory | 配置 Scope、Information、标签与条件 |
| 已知对象 KnownPoints | 当前角色实际知道的具体对象 |
| 感知对象 PerceivedPoints | 本轮感知结果；与已知对象分开 |
| 固定对象 FixedObject | 按名称选择目录记录或当前编辑场景对象；内部存稳定 Id |
| WorldPoints | 普通现场 NPC 取当前关卡交互点；WorldActor 扩展为世界目录身份。需要明确知识约束时使用 WorldDirectory 的 Information |
| 角色文本参数／当前位置 | 保留原有显式身份参数和 self.location 来源 |

世界目录的 Scope 可选当前关卡、当前语义地点、指定地点、角色地点绑定、整个世界或当前活动地点；Information 可选仅已知具体对象、包含公开服务、权威读取。普通 NPC 建议前两种；权威读取需要制作上明确授权。

选择器中的“对象适配器”是目标接入方式，目前内置场景交互点 `point`；“世界对象类型”是床、仓储点、瓶子等 WorldObjectTypeDefinition 配置，两者职责不同。

| 选择器字段 | 配置方法与规则 |
| --- | --- |
| 世界对象类型 ObjectType | 按名称选择类型；留空不限类型，填写时按稳定类型 Id 匹配 |
| 必需对象能力 RequiredCapabilities | 点击“添加要求”，从已配置的对象能力中选择；对象必须同时声明全部选项 |
| 必需交互操作 RequiredOperations | 从已配置操作中按名称选择；对象必须同时提供全部操作 Id |
| AllTags / AnyTags / ExcludedTags | 与上述条件同时生效，继续表达用途、归属等标签限制 |

类型选择后，能力／操作列表显示该类型默认配置与 Scene 覆盖提供的选项；未选择类型时显示支持当前适配器的类型及实例选项。要求数组留空即不限制。引用失效时保留旧值并提示，移除或重新选择即可。相同操作 Id 应表示相同业务语义。

例如工坊取物资：ObjectType 选择仓储类型，RequiredCapabilities 添加“库存”，RequiredOperations 添加“取用物资”（`take_stock`），再配置工作地点范围与知识权限。“前往.destination”和“取物资.source”连接这个选择器的同一输出；执行取物资的 Action 仍需配置 NpcInteractAction，OperationId 使用 `take_stock`。找可倒液体的容器时，可不限制具体类型，改选“液体”和 `pour_liquid` 操作。

这些筛选适用于全部候选来源，固定对象和角色绑定也不能绕过。目录按类型、能力及操作建立共享索引，并使用每个对象覆盖后的有效配置；现场筛选在短名单及路径估算前执行，行动执行时重新验证。操作列表是支持范围，不代表当前库存、区域、权限或容量已经满足；最终检查仍由公共交互入口负责。筛选不会执行操作、占用资源或授予知识。RequiredCapabilities 的“使用容量”仅筛选具有该能力的对象；需要实际占用时，仍在 Action 输入的“资源要求”添加“使用容量”。

`self.location` 是当前具体交互点；`self.level` 是关卡身份；`self.place` 是当前点所属地点，离开交互点时回到关卡默认地点。

未获准读取的 `target.stock/open/available` 为 Unknown。用已有“已知性 Known”表达式检查，而不是把 Unknown 当作零或空闲。当前感知、明确权威查询以及场内所有者规则可读取现场动态值。WorldActor 还可按观测来源、时间和寿命读取仍有效的观测；到期后不作为实时事实，也不会猜测尚未观测到的变化。

**自己的床**：Objects 配置 `preferred_bed`；选择器来源“角色对象绑定”；“前往.destination”和“睡觉.bed”连接同一选择器；Plan 手动勾选这两个 Action。睡觉仍配置到达条件和容量要求。

**工作地点找仓库**：Locations 配置 `work`；选择器来源“世界目录”，Scope 选“角色地点绑定”、LocationBinding 选 `work`、AllTags 填 `storage`；需要了解公开服务时选择对应 Information。前往和取货共享一个节点。货源与收货点分别使用两个节点。

**未加载床铺**：WorldActor 角色在具备获准路线与受支持行动时，可自行旅行并在离屏后端完成休息，随后恢复现场实例。普通现场 NPC 没有世界人口身份，仍要求加载目标；关闭 RequireReachable 不会绕过执行能力检查。具体配置见 [NPC 世界模拟](#npc-旅行活动与离屏模拟)。

## NPC 旅行、活动与离屏模拟

本节按角色 Scene、道路、跨关卡 Action、活动和运行交接的顺序配置，复用前文的地点、对象、知识与公共操作。

### 运行结构

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

### 1. 角色 Scene

1. 在可复用 NPC Scene 的 GameObject3D 根上添加 `WorldActorComponent3D`。已有 `CharacterDataComponent3D.Profile`、`AiAgentComponent3D.Graph`、移动、导航和表现组件继续使用。
2. 每个长期角色需要唯一且稳定的 `PersistentId`。同一个角色不能同时摆在多个关卡；复用外观 Scene 制作另一个角色时，在实例上给它不同的角色身份。世界对象的自动摆放身份规则不变。
3. `ScenePathOverride` 通常留空，自动记录 NPC 的 Scene 路径；需要指定用于恢复表现的角色 Scene 时才填写。该 Scene 也必须具有 WorldActor。
4. 设置 `DetailedDistance=30`、`AbstractDistance=45`、`MinimumResidencySeconds=2`，或根据镜头调整。详细距离必须小于抽象距离，二者构成迟滞；驻留时间防止反复切换。
5. 将角色 Scene 拖入登记关卡并保存。编辑器自动生成角色名册、初始关卡和位置、速度、图、Profile 与角色参数覆盖。角色目录只保存角色资源引用和 Scene 路径，不加载角色 Scene。

启用 WorldActor 后，角色数据、位置、Agent 和检查点统一进入世界 population 存档，运行时自动关闭这几个组件自己的重复存档注册。未添加 WorldActor 的原有现场 NPC 继续走原有存档和 LOD，不自动获得世界旅行能力。

### 2. 关卡入口和道路

先完成 [世界配置与关卡切换](WORLD_GUIDE.md) 中的地点、关卡、对象与入口配置。在路线两端关卡各放一个 GameObject3D，添加 `WorldSpawnPointComponent3D`：

| 字段 | 例子 | 含义 |
| --- | --- | --- |
| `Entry` | `town_road` | 关卡内唯一入口键 |
| `MarkerPath` | `Arrival` | 子节点 Marker3D 的路径，保存时编译其关卡坐标 |

常规传送门优先使用关卡根的“导出关卡配置”按钮自动生成路线。需要事件控制、秘密道路或特殊权限等手工道路时，在 `WorldDefinition.Routes` 添加 `WorldRouteDefinition`：

| 字段 | 配置 |
| --- | --- |
| `Id / DisplayName` | 稳定道路身份及名称，例如 `town_to_workshop` |
| `From / To` | 起点和终点 WorldLevelDefinition |
| `FromEntry / ToEntry` | 两端的 Entry 键；必须存在于已保存关卡目录 |
| `TravelHours` | 非负数，世界游戏小时；`0` 表示即时跨门，`0.25` 表示 15 游戏分钟 |
| `Bidirectional` | 是否允许双向通行 |
| `Enabled` | 静态启用状态 |
| `Public` | 公开路线；关闭后角色必须在 KnownRoutes 中知道它 |
| `Permission` | 可选角色表达式，例如钥匙、任务事实或角色权限 |
| `UseExplicitPositions` | 通常关闭；特殊数据制作才开启并填写两端坐标 |

路线图与地点树分别配置。可连接 A→B→C；系统按旅行小时找可通行路径，另计角色到入口、途经关卡不同入口之间、出口到目标的本地移动时间。不会直接相减不同关卡的坐标。本地移动估算按目录速度；现场实际移动仍受导航约束，估算不保证绕障路径精确。

运行时用 `world.Routes.SetOpen(routeId, false)` 封路，`true` 重新开放；关闭状态进入存档。旅行推进时重新验证当前路段；封路或权限失效会使当前行动失败并重新规划。非公开路线在角色 `CharacterWorldDefinition.KnownRoutes` 配置初始知识，也可通过运行时知识授予。

### 3. 跨关卡行动与远处候选

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

### 4. 活动上下文

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

### 5. 离屏执行、保存和精度交接

- 代理禁用原生逐帧更新，由世界事件队列按到达、等待完成、日程边界、需求阈值、失败冷却和活动请求推进。无法从任意表达式推导下一事件时，以一游戏分钟为保守检查间隔，不宣称所有自定义条件都能精确预测触发时刻。
- 当前可恢复且可离屏运行的执行图是串行移动、等待、数值计算和公共交互。库存和液体内置操作使用同一状态与提交入口；自定义操作必须显式支持 `SupportsSemantic`，并实现不依赖现场组件的业务检查与提交。
- 技能、动画事件、物理战斗、自定义副作用和复杂分支图没有通用语义后端。现场仍可运行，转为远处后取消并重规划；不会猜测它们的结果。休息可使用等待＋成功 Results，仍按本次实际等待完成发放收益。
- 世界名册保存唯一角色位置、关卡、当前路段、路段进度、接驳剩余时间、下次事件、角色数据、活动、剩余行动与提交标记。租约、路径、Flow 栈和 Node 引用不保存，恢复时重新验证和申请。
- 现场与远处共享对象库存和使用容量。远处长等待使用随行动释放的语义租约，不会因普通 5 秒租期过期而错误共享床位；取消、失败和精度交接都会释放旧租约。
- 玩家进入 NPC 所在关卡且足够靠近时，异步加载原角色 Scene，交接数据和行动检查点。离开详细范围后再转代理；每次 Process 最多 4 次表现切换。逻辑名册压制已经迁走的初始摆放，避免原关卡再次生成同一个人。
- 新游戏先让初始关卡中摆放的角色绑定世界身份，再开始调度；加载界面期间暂停世界时钟，避免 NPC 在玩家看到场景前已经离开。世界角色恢复检查点时，由当前宿主决定模拟精度：现场实例使用导航行走，远处代理使用抽象旅行，检查点中的旧精度不会覆盖当前宿主。
- 现场 NPC 抵达路线出口时，使用与导航相同的水平到达范围；传送门节点高于地面不会造成导航已到达、旅行却无法开始。
- 主动关注可调用 `Population.RequestInterest(actorId, seconds)`：最多 16 个关注身份、单次寿命不超过 120 秒。关注只能恢复当前关卡角色，不会额外加载远处关卡。
- 快进触发分批补算期间暂停现场角色和玩家操作，避免玩家在过去事件尚未完成时修改同一库存；补算完成自动恢复。大于 0.05 游戏小时的时钟跳变进入补算状态。需要倒退时间时读取存档，不在活动会话直接减去时钟。
- 关闭游戏不会按现实离线时长补算。需要快进时推进世界时钟，事件队列处理到目标时间。世界时间、对象和所有角色应在同一个原子存档中一起恢复。

检查点细节见 [NPC 行动检查点与恢复](NPC_AI_GUIDE.md#行动检查点与恢复)。

### 6. 离屏预算、诊断和验收

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

## 状态与性能

世界目录有地点／关卡／标签／类型／能力／操作索引，静态查询共享最多 256 个缓存结果；查询从较小的命中集合筛选，缓存键包含全部类型、能力和操作要求。已知对象查询优先遍历角色自己的知识集合。现场空间格子为 8 m，移动交互点会更新格子。

- 每个关卡的规划软预算默认 1.5 ms，包含候选构建和 GOAP 推进；单次查询／原生导航不能中途抢占。
- 会话每帧最多 8 次路径估算、32 次感知射线；单 NPC 每次最多处理 8 个附近对象，轮换扫描并短期保留可见结果。
- 候选粗探测从 16 开始，无计划后扩大到 128；GOAP 默认保留 4 个目标。预算耗尽返回 QueryBudgetPending，分帧继续，不当作已经无解。
- 路径缓存最多约 256 条／NPC，按位置、落点、导航版本和 2 秒有效期校验。目标绑定不随缓存变化。
- 快照只捕获本次相关对象；世界目录存在不触发远处视线或路径计算。

显式切换会短暂同时持有旧关卡和目标资源，异步读取仍可能在实例化及 GPU 上传时产生峰值。完整玩法人群与渲染峰值尚待目标硬件验收。

加入类型／能力／操作索引后的本机无头兼容渲染基线（2026-10-22；非发行包性能）：

| 目录规模 | 建立索引 | 首次全量命中查询 | 缓存查询平均 |
| --- | --- | --- | --- |
| 1,000 | 8.13 ms | 0.202 ms | 0.014 ms |
| 10,000 | 95.45 ms | 0.962 ms | 0.001 ms |

查询返回全部匹配记录，重复查询 20 次测缓存，尚未包括每个 NPC 的知识过滤、导航与 GOAP。样本很短，较小目录均值较高与 JIT、计时噪声有关，不代表规模反转。

以下 NPC 表保留能力组合重构时的基线，并非本次选择器筛选扩展的复测。`world_population_validation.tscn` 固定 32 个现场点，每组同时触发 3 轮“选择目标 → 两步行动”，使用真实导航估算和规划调度。每帧统计角色状态、AI 更新与模块处理的 CPU；首组包含预热，未包含绘制、动画或实际旅行。

| NPC | 3 轮完成帧数 | CPU P95 | CPU P99 | 托管分配／帧 |
| --- | --- | --- | --- | --- |
| 16 | 50 | 16.264 ms | 33.132 ms | 925.1 KiB |
| 64 | 118 | 12.597 ms | 17.719 ms | 1621.7 KiB |
| 128 | 142 | 11.454 ms | 16.478 ms | 2767.0 KiB |

三组均完成，没有规划饥饿；路径峰值 8、射线峰值 32。结果同时暴露了候选构建、GOAP 状态复制和执行快照的分配及耗时，首组有明显预热峰值。该实现是有测量依据的首期功能基线，尚不能据此承诺商业项目的人群容量。后续性能工作应先用 profiler 拆分这些峰值，再考虑快照复用、进一步分帧及状态表示优化，不能只调低软预算数值。

## 验证

2026-10-24：正式 NPC 日常 Goal 验证增至 40 项，覆盖初始关卡出生、走向出口、未完成行动的离屏／现场交接，以及逐物理帧的移动连续性。本次 60 Hz 验证采样 679 次，最大水平步长 0.037 m；检查点 55 项、世界模拟 44 项、世界框架 55 项与正式关卡／菜单／存档流程回归通过。

2026-10-24：`scripts/tools/world/world_level_export_validation.tscn` 通过 24 项，覆盖关卡／地点／类型／NPC 自动登记、单向门、返回门、零耗时、坐标更新、复制删除、嵌套 Scene 复用、手工路线保护、另存关卡与重复导出。先运行该场景生成夹具，再运行 `--editor --headless --script res://scripts/tools/world/world_level_export_editor_validation.gd`，通过 4 项实际编辑器检查，验证导出调用编辑器保存、Scene 和路线坐标一致以及连续导出。相关回归另有自动身份 14 项、世界模拟 44 项、正式 NPC 日常 Goal 31 项通过。编辑器无头缩略图仍有 Dummy 纹理诊断，不影响保存结果和断言。

`world_authoring_validation.tscn` 已通过 14 项，覆盖自动补齐能力、模板无全局身份、嵌套实例、复制顺序、改名移动、撤销重做、重开、生成目录、类型更新和独立状态；`scripts/tools/world/world_framework_validation.tscn` 已通过 55 项，覆盖目录、库存往返、卸载角色、知识权限、Unknown、独立复制、销毁禁用、空间更新、继续游戏、实时读档、失败回退、取消、会话替换、加载界面、进度完成及目录规模查询。`world_capability_validation.tscn` 已通过 20 项，覆盖能力组合、缺失能力、覆盖与独立复制、现场同步、液体事务、延迟恢复本地类型、租约重置、旧版本拒绝。`world_interaction_validation.tscn` 已通过 37 项，覆盖物理 E 输入、区域不同位置、大区域索引、操作列表、遮挡／权限／容量、NPC 公共操作提交、禁止直接写目标、独立复制及存档。`world_production_validation.tscn` 使用真实城镇、工坊、玩家、GM、物理触发门与存档，并验证库存覆盖、瓶子液体余量保存、主菜单新游戏／返回／继续链路。`world_population_validation.tscn` 提供上表压力测试。原有 `scripts/tools/npc/selector_binding_validation.tscn` 已通过 100 项，继续验证共享目标及图编辑。

先运行自动身份验证生成忽略目录中的夹具，再用编辑器模式执行 `--editor --headless --script res://scripts/tools/world/world_authoring_editor_validation.gd`，已通过 13 项实际编辑器检查：后台标签页保存时补齐缺失组件、保留交互参数、连续编译与构建保留已打开场景资源、同一 Type 增删能力后同步，以及复制时保留原身份。无头缩略图生成会报告 Dummy 纹理为空。

使用项目配套的 DDGI 引擎。世界无头逻辑验证可以用兼容渲染；实际场景中的 DDGI 会提示需要 Forward+ Vulkan Ray Query，这类运行不验证画面。测试文件及日志写在 `.godot/world-validation/`，不会改正式存档。

当前无头日志仍有系统证书、MCP 注册权限及退出时 RefCounted 存活提示；首期真实场景验证曾以 verbose 检查退出对象，本次能力重构尚未重复完整内存审计。以上通过结果指逻辑断言通过，不能作为渲染、长期内存稳定或零泄漏验收。

目录规模验证在退出前显式等待托管终结器。本机引擎在未排空终结器时曾于退出阶段的 `godotsharp_array_destroy` 崩溃；提前排空时同一组断言和进程退出均通过。该处理只用于测试清理，不在游戏循环或世界模块中强制 GC；引擎退出时序和完整内存审计仍需单独跟进。
