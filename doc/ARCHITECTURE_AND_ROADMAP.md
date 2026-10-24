# NPC、AI 与 World 架构和后续工作

更新：2026-10-24。本文整合原世界框架实施计划，移除实施前的代码替换表及已过时的待办描述。具体制作步骤统一维护在 [NPC 与 AI 手册](NPC_AI_GUIDE.md) 和 [世界配置手册](WORLD_GUIDE.md)。

[文档入口](README.md) · [职责](#组件与系统职责) · [状态归属](#配置与状态所有权) · [决策与执行](#决策与执行约定) · [生命周期](#生命周期与恢复) · [后续工作](#尚未实施与后续工作)

## 组件与系统职责

| 组件或系统 | 职责 |
| --- | --- |
| `CharacterDataComponent3D` | 玩家与 NPC 共用的需求、属性、记忆、关系、情绪、业务事实与世界知识 |
| `AiAgentComponent3D` | 读取角色数据，执行 Goal 选择、GOAP、运行时计划和 Action Graph |
| `WorldActorComponent3D` | 将 NPC Scene 接入唯一世界角色名册，绑定现场／代理实例并交接存档，配置精度切换 |
| `WorldPopulation` | 唯一逻辑位置、路线进度、离屏事件、关注请求以及现场角色生成和回收 |
| `WorldObjectComponent3D` | Scene 对象的类型、静态配置、自动身份与权威状态绑定 |
| `WorldInteractionComponent3D` | 现场交互区域、遮挡判定及导航参考位置；公共操作由 `WorldInteraction` 校验提交 |
| `AiWorldComponent3D` | 现场环境、交互点和角色注册，以及本环境的规划与查询调度；轻量代理也有独立环境宿主 |
| `WorldModule` | 世界目录、对象状态、路线、角色名册、唯一玩家、当前关卡和关卡切换事务 |

WorldActor 是 Scene 实例与持续存在的逻辑角色之间的连接层。旅行和离屏调度位于 WorldPopulation，Goal、Plan 和 Action 继续属于 AI。角色离开当前关卡后可以释放现场实例，逻辑名册保留状态；需要显示时再加载原角色 Scene。

```text
全局模块：资源、存档、世界时钟、世界会话
WorldSessionRoot
├─ Actors：唯一玩家
├─ Levels：当前激活关卡及现场 NPC、对象
└─ WorldLogicalActors：轻量 NPC EC 代理
```

代理只装配必要的数据和 AI 组件，禁用原生逐帧更新，由事件队列驱动同一角色图。没有为单个远处角色加载整个关卡，也没有另建一套生活行为规则。代理仍有 EC、编译图和状态成本，不能当作零成本纯数据记录。

## 配置与状态所有权

| 内容 | 唯一配置或持有方 |
| --- | --- |
| 世界、地点、关卡、道路 | WorldDefinition 与对应定义资源 |
| 对象默认能力及参数 | WorldObjectTypeDefinition.Capabilities |
| 对象摆放、名称、归属及能力覆盖 | 可复用 Scene 上的 WorldObjectComponent3D |
| 对象与入口目录、初始角色名册 | 保存登记关卡时自动生成的 WorldLevelCatalog |
| 运行库存、液体、开放覆盖、销毁与使用容量 | 世界对象状态；现场与离屏共用 |
| 行为配置 | NPC 图中的 Goal、手动 Plan、Action、选择器与活动窗口 |
| 已选目标、剩余行动、冻结输入及进度 | 每个 NPC 本次运行时计划与行动检查点 |
| WorldActor 角色的状态和位置 | 世界 population 统一存档，关闭组件自己的重复存档注册 |

对象没有手工维护的 WorldObjectDefinition；配置好的对象 Scene 拖入登记关卡并保存即可生成独立身份和目录。动态长期对象仍需生成系统提供持久身份。NPC 则要求稳定、唯一的角色 PersistentId，角色迁出后的原场景摆放不能重新生成另一个同身份实例。

名称、节点路径和坐标不作为对象存档身份。地点是语义范围，关卡是加载单位，路线图负责通行关系；不同关卡的坐标不直接相减。卸载与销毁分开表达，默认库存只在当前身份首次建立状态时应用。

## 决策与执行约定

```mermaid
flowchart LR
    D[世界目录与角色知识] --> S[目标选择器]
    C[活动：时间、地点、许可与中断规则] --> G[Goal]
    G --> P[手动 Plan：可用 Action 集合]
    P --> R[GOAP 运行时计划]
    S -->|候选与共享节点关系| R
    R -->|固定对象身份与本步输入| A[Action Graph]
    A --> W[移动或公共交互]
    W --> T[成功校验并提交]
```

- Goal 表达激活、评分和期望完成状态；活动上下文选择并约束 Goal，不增加另一套执行器。
- Plan 手动选择行动集合和适用条件。GOAP 决定使用哪些行动、顺序与重复次数；空集合不表示全部行动。
- 所有对象输入通过选择器连线。同一选择器节点在本 NPC、本次计划中共享选中身份，不同节点独立。
- 运行时计划由系统生成，不需要制作；Plan 不声明跨步骤变量。数量、时长和共用计算在 Action Graph 的局部数值中配置。
- 查询定义不保存当前选中值。候选分支、绑定和状态去重相互隔离，失效时取消或重规划，不悄悄换目标继续原步骤。
- 对象声明能力和操作，不自动创建 Goal、Action 或 Plan。Results 修改角色状态，对象业务经公共操作提交。

目录存在不授予角色知识。感知、已知身份、仍有效的观测和权威业务状态分开；Unknown 不替换成零、空闲或已可达。候选保留所属关卡、可选空间距离、旅行时间、路线和执行能力。筛选使用共享索引和有限短名单，GOAP 只比较保留下来的方案，不承诺全世界最优。

## 生命周期与恢复

进入世界先建立目录和状态，再创建会话、角色代理、关卡与玩家。继续游戏恢复同一份世界时钟、对象状态、角色名册和检查点。新游戏重新初始化会话与日历，不混入旧角色的模拟时间。

玩家关卡切换经过：校验请求 → 异步加载 → 预检入口及身份 → 保存交接状态 → 交接关卡与唯一玩家 → 等待物理／导航 → 提交并释放旧资源。请求版本、取消令牌和句柄归属处理迟到结果；准备失败保留原关卡，提交失败按事务路径回退。NPC 自主旅行只推进自己的逻辑位置，不替玩家切换关卡。

WorldActor 在现场与轻量代理之间交接角色数据、活动和受支持行动的检查点。名册决定角色所在位置与唯一实例，原始摆放只是初始来源。距离迟滞、最短驻留时间、有限关注寿命及每次 Process 的表现切换上限限制反复生成。

存档不保存任意 Flow 栈、导航路径、Node 或租约。恢复时重新验证配置、输入、对象和能力，并重新申请占用；支持串行移动、等待、计算和公共交互，不支持的自定义执行图明确取消重规划。对象和角色结果在 Action 成功边界提交，行动实例及提交标记防止重复收益。

离屏按旅行到达、等待完成、日程边界、需求阈值和冷却事件推进；任意表达式无法推导下一事件时使用保守检查间隔。快进按稳定顺序分批补算，期间限制现场交互以维持同一业务时序。所有权威状态在主线程修改；关闭游戏不会按现实离线时长自动补算。

## 已实现与支持范围

| 领域 | 已有能力 |
| --- | --- |
| 世界与对象 | 共享目录、自动对象身份、类型能力与覆盖、公共区域和玩家 E 交互 |
| 关卡与流程 | 单活动关卡、异步加载界面、唯一玩家交接、失败／取消、菜单、新游戏与继续游戏 |
| NPC 与 AI | Goal、手动 Plan、选择器共享目标、Action 图内数值、知识权限和局部快照 |
| 旅行与恢复 | 路线方向／权限／知识、多段旅行、逻辑位置、途中存档、到达生成与去重 |
| 离屏与活动 | 轻量代理、事件预算、快进、精度交接、日程／任务／紧急活动、允许操作及回退 |
| 远处执行 | 受支持串行语义行动；内置库存与液体操作共用权威状态及容量 |

验证场景、历史结果、预算和性能基线统一见 [世界手册](WORLD_GUIDE.md#状态与性能) 与 [NPC 手册](NPC_AI_GUIDE.md#选择器连线回归验证)。软毫秒预算不能抢占单个原生操作或 Scene 实例化；基础 headless 回归不代表完整玩法人数、显存峰值、长期稳定性或零泄漏已经验收。

## 尚未实施与后续工作

| 范围 | 缺口 |
| --- | --- |
| 入口的业务通行条件 | 钥匙、任务进度等由任务图判断后调用 Activate，具体接线仍需制作 |
| 复杂业务资源 | 工具、材料、库存预留、搬运订单及多资源事务需要明确的预留、提交和取消语义 |
| 任意行为的离屏后端 | 战斗、技能、动画事件和复杂自定义副作用需要各自的语义实现 |
| 多实例与流式加载 | 多关卡并行、重复住宅／地下城实例需要独立运行时实例身份及上下文验证 |
| 动态长期角色和对象 | 需要持久身份、生成／删除规则与独立存档生命周期 |
| 制作与诊断扩展 | 图内状态着色、配置预演及通用角色／威胁传感器尚未包含 |
| 完整性能验收 | 目标硬件上的总 AI CPU、同时规划、快进积压、GC、恢复、加载与显存峰值，以及长期稳定性 |

后续优化先通过 profiler 分离候选构建、GOAP、状态复制、事件推进和实例化成本，再决定快照复用、分帧或纯数据工作线程；不能只调低软预算。工作线程若引入，只计算快照，主线程验证提交，不在工作线程操作 Godot 场景与物理。

## 代码与配置入口

- [AI 定义、运行时与编辑器](../scripts/gamelogic/ai/)、[NPC 导航与诊断](../scripts/gamelogic/npc/)。
- [世界运行时](../scripts/gamelogic/world/runtime/)、[现场组件](../scripts/gamelogic/world/components/)、[目录编译器](../scripts/gamelogic/world/editor/WorldAuthoringCompiler.cs)。
- [世界配置资源](../assets/resources/world/)、[NPC 图资源](../assets/resources/npc/)。
- [NPC 验证器](../scripts/tools/npc/)、[世界验证器](../scripts/tools/world/)；临时夹具和日志位于忽略目录 `.godot/`。

游戏行为继续使用 GameObject3D／Component3D；框架协调接入 ModuleSystem。不修改 `scripts/generated/config` 中的自动生成代码。

## 设计参考

以下保留原计划的研究入口，仅作为设计背景。实现能力及性能按本项目验证，不沿用其他游戏的人数或内部实现承诺。

- 天际 Creation Kit：[Reference](https://ck.uesp.net/wiki/Reference)、[Location](https://ck.uesp.net/w/index.php?title=Location&mobileaction=toggle_view_desktop)、[Quest Alias](https://ck.uesp.net/wiki/Quest_Alias_Tab)、[Packages](https://ck.uesp.net/w/index.php?title=Bethesda_Tutorial_Packages&mobileaction=toggle_view_desktop)、[Sandbox](https://ck.uesp.net/wiki/Sandbox_(Procedure))。
- The Sims: Medieval：[GDC 开发复盘](https://www.gdcvault.com/play/1014569/AI-Development-Postmortems-Inside-DARKSPORE)。
- 天国拯救 II：[Combining GOAP and MBTs](https://www.gdcvault.com/play/1035576/Game-AI-Summit-Combining-GOAP)、[Supporting Thousands of NPCs](https://gdcvault.com/play/1035718/Supporting-Thousands-of-NPCs-in)、[开发者完整演讲](https://www.youtube.com/watch?v=yMlTT-yqdmc)。
