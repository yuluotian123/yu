# GameLogic

`scripts/gamelogic` 保存项目业务系统。框架层提供通用生命周期、资源、UI、事件、FSM 和图能力；GameLogic 负责角色、输入、技能、任务、存档和具体玩法语义。

## 已有模块文档

- [Input](input/README.md)：输入分组、层级、消费、buffer 和模拟输入。
- [Save](saves/README.md)：`ISaveable` 注册与 JSON 存档槽。
- [Mission](missions/README.md)：FlowGraph 任务链、任务部署和保存恢复。
- [HFSM](hfsm/README.md)：角色层次状态机、组件状态和 Skill 状态。
- [Skills](skills/README.md)：技能资源、冷却、Runtime 和 Skill FlowGraph。

## 其他业务目录

- `abilities/`：能力与属性相关逻辑。
- `ai/`：AI 行为和感知。
- `camera/`：玩家相机组件。
- `gameobject/`：GameObject 和 Component 生命周期。
- `procedures/`：项目具体主流程状态。
- `ui/`：项目窗口和 Widget。

新增业务系统时优先复用 Framework 和 GraphPlugin，不把玩法对象写入通用框架层。

项目总览见 [`docs/PROJECT_OVERVIEW.md`](../../docs/PROJECT_OVERVIEW.md)，优化路线见 [`docs/OPTIMIZATION_ROADMAP.md`](../../docs/OPTIMIZATION_ROADMAP.md)。
