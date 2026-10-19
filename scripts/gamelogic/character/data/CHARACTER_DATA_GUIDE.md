# 通用角色数据

Player 和 NPC 都在 GameObject3D 的 Components 中配置 `CharacterDataComponent3D`。
`Profile` 是可复用的初始配置，`State` 是每个角色独立的运行状态；共享同一个资源不会共享需求值、关系或情绪。

## 配置入口

- Player：`assets/scenes/player.tscn` → Components → CharacterDataComponent3D → Profile。
- 当前场景 NPC：`assets/scenes/spacelevel.tscn` → Resident / Courier → Components → CharacterDataComponent3D。
- 独立 NPC 场景：`assets/scenes/npc/resident.tscn` 和 `courier.tscn`。

角色配置位于 `assets/resources/character/<角色>/profile.tres`。按模块展开：

| 模块 | 内容 |
| --- | --- |
| Identity | 显示名称、角色标签、文本/数值/布尔参数，例如 home 对应的交互点 ID |
| Needs | 需求 ID、显示名称、范围、初始值、每小时变化率及变化倍率 |
| Personality | 性格维度、特质和偏好 |
| Relations | 关系维度、范围、默认值和联系人容量 |
| Affect | 情绪维度、基线和自然恢复速度 |
| Facts | 角色自己的业务状态，例如 private.completed_visits |

各模块可禁用或不配置。外观资源由场景的 AnimatedSprite3D 与 CharacterAnimationComponent3D 管理。
参数的实例覆盖在角色数据组件的 `ParameterOverrides` 中设置，必须引用 Profile 已声明的同类型参数。

## 需求与公式

`assets/resources/character/presets/common_needs.tres` 包含口渴、饥饿、娱乐、疲劳四项需求。
在 Needs 检查器使用“补充所选预设”或“+ 自定义需求”；预设合并只补充缺项，保留已有配置。
需求的变化倍率可以读取角色数据与世界数据，不能依赖当前 AI 目标、行动、日程或紧迫度。

需求值通过 `needs.<id>.value` 暴露给 AI。AI 的进入/退出紧迫阈值与曲线另配在 AI 图的“需求决策规则”节点中。
玩家是否自动处理需求由其控制方式决定，不影响需求本身的更新。

## 更新、执行与存档

角色数据组件先于控制器初始化，并使用游戏时钟更新需求和情绪；不要求角色挂有 AI。
`CharacterState.Apply` 验证并提交角色数据变更。AI 搜索只改自己的 GoapWorldState 快照，行动成功后才提交真实值。
角色数据保存在 `character_data` 存档分区。AI 的记忆和需求紧迫状态单独保存在 `npc_agent` 分区，取消计划不会清除角色数据。

此结构直接使用新的配置和存档结构，不保留旧角色数据节点、旧脚本路径或旧存档格式的兼容读取。
