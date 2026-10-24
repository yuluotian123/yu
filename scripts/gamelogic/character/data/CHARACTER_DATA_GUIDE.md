# 通用角色数据

Player 和 NPC 都在 GameObject3D 的 Components 中配置 `CharacterDataComponent3D`。
`Profile` 是可复用的初始配置，`State` 是每个角色独立的运行状态；共享同一个资源不会共享需求值、关系或情绪。

## 配置入口

- Player：`assets/scenes/player.tscn` → Components → CharacterDataComponent3D → Profile。
- 当前场景 NPC：`assets/scenes/levels/forest_town_level.tscn` → Resident / Courier → Components → CharacterDataComponent3D。
- 独立 NPC 场景：`assets/scenes/npc/resident.tscn` 和 `courier.tscn`。

角色配置位于 `assets/resources/character/<角色>/profile.tres`。按模块展开：

| 模块 | 内容 |
| --- | --- |
| Identity | 显示名称、角色标签、文本/数值/布尔参数，例如 home 对应的交互点 ID |
| Needs | 需求 ID、显示名称、范围、初始值、每小时变化率、变化倍率及紧迫度规则 |
| FeatureLibrary | 可选的特征定义库覆盖；留空使用公共库 |
| Personality | 选择已有性格、偏好并覆盖个体数值；选择角色拥有的特质 |
| Relations | 覆盖各关系维度的初始值，配置联系人容量 |
| Affect | 覆盖情绪基线和自然恢复速度；当前情绪保存在运行状态中 |
| Facts | 角色自己的业务状态，例如 private.completed_visits |
| World | 具名地点／对象绑定、已知地点与具体对象，见 [世界配置指南](../../../../doc/WORLD_GUIDE.md) |

各模块可禁用或不配置。外观资源由场景的 AnimatedSprite3D 与 CharacterAnimationComponent3D 管理。
参数的实例覆盖在角色数据组件的 `ParameterOverrides` 中设置，必须引用 Profile 已声明的同类型参数。

## 角色特征定义库

公共定义库为 `assets/resources/character/definitions/common_features.tres`。新建 Profile 自动使用公共库，无需单独选择或保存库引用；只有需要另一套定义时，才在 Profile → FeatureLibrary 指定覆盖。清空覆盖即可恢复公共库。在检查器打开库资源，按性格、特质、偏好、情绪、关系分类搜索和编辑，点击“+ 新建情绪”等按钮添加共享定义。

共享定义包含名称、说明、类别、范围与默认值；情绪还包含每游戏小时的默认恢复速度。内部 ID 自动生成并保存，改名不会改变引用。特质表示有无，不配置数值范围。停用或删除被角色引用的定义会报告失效，需要修复原引用。

初始公共条目为社交倾向、谨慎程度、内向、开朗、活动偏好、心情、信任和好感。名字只定义一次，角色无需重复填写字符串。

从角色 Profile 展开 Personality、Affect 或 Relations，点击“+ 添加性格”“+ 添加偏好”“+ 添加情绪”“+ 添加关系”，搜索选择同类别定义；点击条目编辑。条目默认勾选“使用定义默认值”，取消后填写个体值。情绪的个体值同时作为初始值和自然恢复基线，可另外取消“使用定义恢复速度”并填写个体恢复速度。

未覆盖的数值维度使用库默认值；移除覆盖条目会恢复默认值。特质使用“+ 添加特质”选择，未选择的特质为否。停用整个模块后，该模块不向公式提供参数。

公式参数库使用共享定义的中文名称，包括没有个体覆盖的数值维度和角色未拥有的特质。运行时读取仍使用稳定标识，例如 `personality.sociability`、`trait.reserved`、`preference.activity`、`affect.mood` 和 `relations.trust.max`；原有示例标识保持不变。新定义的标识由编辑器自动处理，通过参数选择器引用即可。

NPC 执行图中的“改变情绪维度”“改变关系维度”也从共享定义中按名称选择维度；关系行动的联系人从角色文本参数中选择。

每个角色的情绪、关系和私有事实仍独立更新和存档，修改个体值不会修改共享定义。情绪与关系按各定义自己的范围限制；旧联系人存档缺少新建的关系维度时，按角色该维度的默认值补齐。

共享默认值或范围的修改会在下次初始化角色时生效；已经编译的运行状态不热更新。修改共享定义后保存定义库，修改角色覆盖后保存对应的 Profile。已有存档中的动态情绪和关系会按新范围验证。

## 需求与公式

`assets/resources/character/presets/common_needs.tres` 包含口渴、饥饿、娱乐、疲劳四项需求。
在 Needs 检查器使用“补充所选预设”或“+ 自定义需求”；预设合并只补充缺项，保留已有配置。
需求的变化倍率可以读取角色数据与世界数据，不能依赖当前 AI 目标、行动、日程或紧迫度。

点击每条需求资源，可展开“基础数值”和“紧迫度规则”。紧迫度规则和需求一起保存，不需要在 AI 图中另建节点或添加需求引用：

| 参数 | 用途 |
| --- | --- |
| HigherIsUrgent | 勾选表示数值越高越紧急；取消表示数值越低越紧急 |
| UseDefaultThresholds | 使用按当前需求范围缩放的默认阈值；取消后显示自定义阈值 |
| EnterThreshold | 进入紧急状态的绝对数值；高值紧急使用大于等于，低值紧急使用小于等于 |
| ExitThreshold | 解除紧急状态的绝对数值；须与进入阈值留有间隔，避免反复切换 |
| UrgencyExponent | 紧迫度曲线指数，必须大于 0；1 为线性，大于 1 时更侧重接近极端的需求 |

默认规则在高值紧急时使用范围的 65%／25% 作为进入／解除阈值，低值紧急时使用 35%／75%；指数为 1。例如范围 10～110 的高值需求，默认在 75 进入紧急、35 解除。新增自定义需求与预设需求都自带规则。预设合并保留已有需求的规则，补入的需求包含独立规则副本。

需求值通过 `needs.<id>.value` 暴露给 AI；AI 从同一条需求读取规则，提供 `needs.<id>.urgent`（是否紧急）和 `needs.<id>.urgency`（0～1 紧迫度）供目标与条件公式使用。紧急状态的进入／解除记忆仍属于 AI 控制器；角色数据的独立更新与存档不依赖 AI。
玩家是否自动处理需求由其控制方式决定，不影响需求本身的更新。

## 更新、执行与存档

角色数据组件先于控制器初始化，并使用游戏时钟更新需求和情绪；不要求角色挂有 AI。
`CharacterState.Apply` 验证并提交角色数据变更。AI 搜索只改自己的 GoapWorldState 快照，行动成功后才提交真实值。
角色数据保存在 `character_data` 存档分区。AI 的记忆和需求紧迫状态单独保存在 `npc_agent` 分区，取消计划不会清除角色数据。

此结构直接使用新的配置和存档结构，不保留旧角色数据节点、旧脚本路径或旧存档格式的兼容读取。
