# NPC、AI 与 World 文档

更新：2026-10-24。NPC 行为、AI 图、行动恢复、世界对象、关卡切换及离屏模拟统一在本目录维护。

## 阅读顺序

| 手册 | 内容 |
| --- | --- |
| [NPC 与 AI 配置](NPC_AI_GUIDE.md) | 角色图、Goal、手动 Plan、Action、选择器连线、表达式、行动检查点及 NPC GM |
| [世界、关卡与 NPC 模拟配置](WORLD_GUIDE.md) | 地点与对象类型、可复用 Scene、交互区域、知识、关卡切换、角色名册、道路、活动与离屏模拟 |
| [架构与后续工作](ARCHITECTURE_AND_ROADMAP.md) | 组件职责、状态所有权、生命周期、实现边界、代码入口及尚未实施的范围 |

制作新关卡时先读世界手册的目录、入口和对象配置；制作 NPC 行为时先读 NPC／AI 手册。需要 NPC 跨关卡活动时，再按世界手册的角色 Scene、道路、活动与离屏配置接入。

## 常见入口

| 要做的事 | 从这里开始 |
| --- | --- |
| 配置 Goal、Plan 和 Action | [图的连接](NPC_AI_GUIDE.md#图的连接)、[配置计划](NPC_AI_GUIDE.md#配置计划) |
| 多个 Action 共用目标 | [选择器与输入](NPC_AI_GUIDE.md#配置选择器与输入) |
| 配置取货数量、等待时间及公式 | [数量、时长与图内计算](NPC_AI_GUIDE.md#数量时长与图内计算) |
| 保存旅行或休息进度 | [行动检查点与恢复](NPC_AI_GUIDE.md#行动检查点与恢复) |
| 制作可复用的箱子、床或瓶子 | [对象类型与能力](WORLD_GUIDE.md#3-按类型能力配置对象) |
| 配置交互区域与玩家 E 键 | [操作与交互区域](WORLD_GUIDE.md#配置操作与交互区域) |
| 配置玩家关卡切换 | [关卡与入口](WORLD_GUIDE.md#2-配置关卡与入口) |
| 配置 NPC 自主旅行、上班和离屏活动 | [NPC 旅行、活动与离屏模拟](WORLD_GUIDE.md#npc-旅行活动与离屏模拟) |
| 查看性能基线与验证入口 | [现场与目录性能](WORLD_GUIDE.md#状态与性能)、[离屏预算与验收](WORLD_GUIDE.md#6-离屏预算诊断和验收) |
| 理解 WorldActor 为什么存在 | [组件与系统职责](ARCHITECTURE_AND_ROADMAP.md#组件与系统职责) |

## 相关基础资料

- [通用角色数据](../scripts/gamelogic/character/data/CHARACTER_DATA_GUIDE.md)：需求、性格、关系、情绪与业务事实。
- [角色系统](../docs/CHARACTER_SYSTEM.md)：玩家与 NPC 共用的执行层。
- [通用图动作](../docs/GRAPH_BASIC_ACTIONS.md)：Flow 基础节点和执行规则。
- [居民美术](../docs/art/npc_resident.md)、[跑腿员美术](../docs/art/npc_courier.md)：角色素材与制作记录。

文档中的性能和回归数字保留原测试日期。本次整理不代表重新进行了完整运行时或目标硬件验收。新增配置说明应更新对应手册及本页入口，避免再次散落到实现目录。
