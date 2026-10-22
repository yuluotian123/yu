# 世界配置与关卡切换

2026-10-22，已采用 Scene 唯一配置来源、自动摆放身份和生成目录；不再创建每个对象的 Record 资源。运行代码与配置入口已落地；范围及后续任务见 [实施计划](WORLD_FRAMEWORK_PLAN.md)。

世界入口是 [default_world.tres](../../../assets/resources/world/default_world.tres)。`WorldModule` 持有共享目录、长期对象状态和唯一玩家；`AiWorldComponent3D` 只管理当前关卡的现场角色、交互点和规划。首期同时激活一个游戏关卡。

## 先运行现成示例

运行项目，在主菜单选择“新游戏”。城镇入口使用 `spacelevel.tscn`，继承 Whitebox 中的世界组件；走进 `WorkshopPortal` 进入工坊，工坊 `TownPortal` 返回城镇。

进入工坊储物箱或瓶子的交互区域后按 **E**。储物箱每次使用 1 单位物资，瓶子每次倒出 0.25 单位液体。一个操作直接执行；多个操作先显示列表，可点击按钮，或方向键选择后按 E 确认。库存示例表示物资消耗，尚未定义可放入背包的物品；倒液体也不附带饮用效果。

F8 →“世界与关卡”可选择目标关卡和入口，查看对象已加载／未加载／已销毁／记录缺失，以及 NPC 最近一次查询。F8 →“NPC”可修改现场库存和开放状态；F8 →“存档”保存槽。重新启动后选择“继续游戏”，回到保存的关卡及位置。新游戏清理会话中的旧状态，磁盘槽在显式保存时写入。正常游戏按 Esc 关闭会话并返回菜单。

也可 F6 运行带上下文的独立关卡；上下文建立测试会话并异步创建玩家。关卡不能再内嵌玩家。

项目开启物理光照单位。工坊的 WorldEnvironment 配有独立 CameraAttributesPhysical（ISO 100、f/16、1/100 秒），匹配 100,000 lux 的方向光；新增关卡也需配置曝光，否则默认光照可能使画面全白。工坊没有天空资源，环境补光使用 Color 来源。

## 1. 建立目录

在 `assets/resources/world/` 新建下列 Resource，并登记到世界入口的对应数组。新记录的稳定 Id 自动生成并保存；显示名称可修改。运行中的目录视为只读，修改配置后重新进入世界。

| 类型 | 制作内容 |
| --- | --- |
| WorldLocationDefinition | 地点名称、Parent、Tags、PublicServices；父子地点都要登记 |
| WorldLevelDefinition | 场景路径 ScenePath、语义地点 Location、DefaultEntry（默认 `default`） |
| WorldObjectTypeDefinition | 默认标签、可选能力列表 Capabilities，各能力自己的默认参数 |
| WorldLevelCatalog | 编辑器自动生成的关卡对象目录；不手动编辑 |
| WorldDefinition | 上述数组、InitialLevel、PlayerScene、Calendar |

在世界入口的检查器点击“校验世界目录”。身份重复、父级循环、缺失归属、非法容量或库存均会报错。Id、名称和场景路径职责不同：重命名对象不会改变存档身份；移动关卡文件后需要更新 ScenePath。

目录不引用 PackedScene，不会为查对象而加载目标关卡。地点是语义范围，关卡是加载单位。装饰和临时表现无需登记为长期对象。

## 2. 配置关卡与入口

1. 关卡根使用 `GameObject3D`，Components 添加 `WorldLevelContextComponent3D`，Level 选择已登记记录。
2. 关卡包含一个带 `AiWorldComponent3D` 的 GameObject3D。EnvironmentPath 指向天气宿主；没有天气组件时降雨为零。
3. 创建出生点 GameObject3D，添加 `WorldSpawnPointComponent3D`。Entry 填稳定入口键，在 MarkerPath 指定原生 Marker3D（默认 `Arrival`）。标记的位置及朝向决定落点。
4. 使用导航的关卡开启 RequireNavigation，并设置 NavigationRegionPath。切换会等待导航就绪，缺失或超时回退。仅玩家测试且无导航的关卡可关闭。
5. 创建门 GameObject3D，添加 `WorldTransitionComponent3D`。配置 TargetLevel、TargetEntry、AreaPath，Area3D 的碰撞掩码包含玩家物理层。Automatic 开启时玩家进入触发；关闭时由交互调用 `Activate()`。

每个关卡的入口键、宿主 PersistentId 和交互点身份必须各自唯一。入口不能靠同名节点猜测。切换会保留同一个玩家实例，同步物理、导航与镜头，并提供 750 ms 落地重触发保护。重复请求被拒绝；取消只作用于准备阶段。提交安排在 ProcessFrame，避免从 Area3D 物理回调直接修改物理树。

入口的钥匙、任务进度与角色权限等业务条件后续由任务图实现，本组件不增加独立条件字段。需要任务图控制的门应关闭 Automatic，再由任务图判断后调用 Activate，避免区域自动触发绕过业务规则；任务图的具体接线仍属后续工作。

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

1. 在 ObjectTypes 登记类型，例如 `types/storage.tres`。展开 Capabilities，设置库存 InitialAmount、容量 Slots、开放时段等默认参数。相同开／关时间表示全天开放，支持跨午夜。
2. 创建以 GameObject3D 为根的 Scene，Components 添加 WorldObjectComponent3D，ObjectType 选择类型。编辑器补齐类型声明的能力组件；保存前也会同步并验证。不再填写 Record、LocalType 或对象 Id。
3. 填写 DisplayName、ExtraTags、OwnerId；Location 可留空，摆入关卡后继承关卡默认地点。需要特殊初值时，在“能力覆盖”选择类型已有能力，点击“添加所选能力覆盖”，展开 Overrides 修改复制后的资源；覆盖不会改动 Type。
4. 添加模型与实体碰撞。类型声明 Interaction 时，增加 `InteractionArea` Area3D 和 CollisionShape3D；另加 `UsePosition` Marker3D，作为导航参考，放在交互范围内、实体碰撞外。
5. 在 WorldInteractionComponent3D 设置 InteractionAreaPath、UsePositionPath、RequireLineOfSight、OcclusionMask。默认路径分别为 `InteractionArea`、`UsePosition`。没有参考节点时 UsePositionPath 留空。
6. 保存为独立 Scene，例如现成的 [storage.tscn](../../../assets/scenes/world/storage.tscn) 和 [bottle.tscn](../../../assets/scenes/world/bottle.tscn)。以后直接拖进已登记关卡并保存即可。

工坊把同一个 storage.tscn 摆了两次。模板库存覆盖为 50，两份实例分别从 50 开始，消耗其中一份不会改变另一份。存档恢复优先于初值，重载关卡不会重新补发。瓶子仅声明液体与交互能力，没有营业时间或使用名额。

### 自动身份、复制与模板更新

- 拖入模板、Ctrl+D 复制、复制整个房间模板，均自动获得独立摆放身份。嵌套 Scene 内的对象按各层摆放键组合，不要求开启 Editable Children。
- 保存、运行及导出前会同步配置和目录。编辑器监听节点新增、场景与资源保存；没有逐帧扫描项目的烘焙任务，也不需要手动创建、绑定或烘焙按钮。
- 节点改名、移动坐标、保存后重开、更新模板内容不会重新分配既有摆放键；撤销／重做恢复相同实例的键。节点名、路径、坐标不充当身份。
- 目录生成使用独立场景快照，保留编辑器已打开 Scene 的组件资源。生成目录位于 `assets/resources/world/generated/<关卡Id>.tres`；这是自动产物，与 Scene 一起提交 Git，不在 Inspector 手动维护。运行时只读目录，不实例化未加载关卡。
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

NPC 执行图添加“执行对象交互”（NpcInteractAction），Input 选择本行动连接选择器的目标输入，操作按名称选择，Amount 直接填写本次数量或公式。固定数量 5 不需要新增 Action 普通输入。需要与等待等节点共用计算结果时，在本执行图声明 Float 局部变量，再用“计算并保存图内数值”赋值，公式通过 `local.<变量名>` 读取；每个角色、每次执行独立。完整步骤见 NPC 图指南“数量、时长与图内计算”。

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

Objects 与 KnownObjects 的检查器按对象名称、关卡和身份后缀提供列表；Objects 的绑定键输入后按 Enter 确认。选择器的固定对象列表也读取同一生成目录，无需复制 Id。

具名绑定自动授予对应身份的初始知识；它不申请容量，也不表示当前看见。运行时知识、绑定变化及最后观测保存在 `character_data` 分区。感知只处理同关卡附近对象，并记录观测时间；最后观测不自动作为实时库存使用。

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
| 当前已加载对象 WorldPoints | 当前关卡全部交互点，允许读取现场动态状态，适合明确需要全局现场信息的规则 |
| 角色文本参数／当前位置 | 保留原有显式身份参数和 self.location 来源 |

世界目录的 Scope 可选当前关卡、当前语义地点、指定地点、角色地点绑定或整个世界；Information 可选仅已知具体对象、包含公开服务、权威读取。普通 NPC 建议前两种；权威读取需要制作上明确授权。

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

未获准读取的 `target.stock/open/available` 为 Unknown。用已有“已知性 Known”表达式检查，而不是把 Unknown 当作零或空闲。当前感知、明确权威查询以及场内所有者规则可读取现场动态值。最后观测仅用于知识记录，暂不提供过期信息的自动推断策略。

**自己的床**：Objects 配置 `preferred_bed`；选择器来源“角色对象绑定”；“前往.destination”和“睡觉.bed”连接同一选择器；Plan 手动勾选这两个 Action。睡觉仍配置到达条件和容量要求。

**工作地点找仓库**：Locations 配置 `work`；选择器来源“世界目录”，Scope 选“角色地点绑定”、LocationBinding 选 `work`、AllTags 填 `storage`；需要了解公开服务时选择对应 Information。前往和取货共享一个节点。货源与收货点分别使用两个节点。

**未加载床铺**：能解析并显示身份，现场 point 适配器返回 TargetUnloaded，关闭 RequireReachable 也不能远程执行。NPC 自主旅行、离屏收益和可恢复行动是后续阶段。

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

`world_authoring_validation.tscn` 已通过 14 项，覆盖自动补齐能力、模板无全局身份、嵌套实例、复制顺序、改名移动、撤销重做、重开、生成目录、类型更新和独立状态；`scripts/tools/world/world_framework_validation.tscn` 已通过 45 项，覆盖目录、库存往返、卸载角色、知识权限、Unknown、独立复制、销毁禁用、空间更新、继续游戏、实时读档、失败回退、取消、会话替换及目录规模查询。`world_capability_validation.tscn` 已通过 20 项，覆盖能力组合、缺失能力、覆盖与独立复制、现场同步、液体事务、延迟恢复本地类型、租约重置、旧版本拒绝。`world_interaction_validation.tscn` 已通过 37 项，覆盖物理 E 输入、区域不同位置、大区域索引、操作列表、遮挡／权限／容量、NPC 公共操作提交、禁止直接写目标、独立复制及存档。`world_production_validation.tscn` 使用真实城镇、工坊、玩家、GM、物理触发门与存档，并验证库存覆盖、瓶子液体余量保存、主菜单新游戏／返回／继续链路。`world_population_validation.tscn` 提供上表压力测试。原有 `scripts/tools/npc/selector_binding_validation.tscn` 已通过 100 项，继续验证共享目标及图编辑。

先运行自动身份验证生成忽略目录中的夹具，再用编辑器模式执行 `--editor --headless --script res://scripts/tools/world/world_authoring_editor_validation.gd`，已通过 13 项实际编辑器检查：后台标签页保存时补齐缺失组件、保留交互参数、连续编译与构建保留已打开场景资源、同一 Type 增删能力后同步，以及复制时保留原身份。无头缩略图生成会报告 Dummy 纹理为空。

使用项目配套的 DDGI 引擎。世界无头逻辑验证可以用兼容渲染；实际场景中的 DDGI 会提示需要 Forward+ Vulkan Ray Query，这类运行不验证画面。测试文件及日志写在 `.godot/world-validation/`，不会改正式存档。

当前无头日志仍有系统证书、MCP 注册权限及退出时 RefCounted 存活提示；首期真实场景验证曾以 verbose 检查退出对象，本次能力重构尚未重复完整内存审计。以上通过结果指逻辑断言通过，不能作为渲染、长期内存稳定或零泄漏验收。

目录规模验证在退出前显式等待托管终结器。本机引擎在未排空终结器时曾于退出阶段的 `godotsharp_array_destroy` 崩溃；提前排空时同一组断言和进程退出均通过。该处理只用于测试清理，不在游戏循环或世界模块中强制 GC；引擎退出时序和完整内存审计仍需单独跟进。
