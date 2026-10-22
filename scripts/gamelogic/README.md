# GameLogic

`scripts/gamelogic` 保存项目业务系统。框架层提供通用生命周期、资源、UI、事件、FSM 和图能力；GameLogic 负责角色、输入、Ability、AI、任务、存档和具体玩法语义。

## 角色目录

```text
character/
  graph/       玩家 CharacterGraph 资源、节点、运行时和组件
  input/       CharacterGraph 输入提供者接口
  movement/    移动命令、Movement 组件、模式和参数
  animation/   Animation 组件与 Locomotion 黑板键

abilities/
  runtime/     AbilitySystem、Resource、Runtime、Policy 和 Timeline
  actions/     Ability Timeline actions

player/input/  玩家 InputModule 适配组件
weapons/       武器定义、动作配置与装备切换组件
inventory/     武器行囊、装备卸下和像素风背包窗口
ai/            NPC 图定义、选择器、GOAP、Flow 执行与当前场景世界组件
npc/           NPC 导航适配与运行诊断
world/         世界目录、会话状态、关卡切换与角色世界引用
hfsm/          通用 HFSM；角色侧仅用于 Locomotion 动画
```

CharacterGraph 是 `FlowGraphAsset` 的业务扩展，不属于 HFSM。AI 不使用 CharacterGraph，而是直接调用 Movement 和 AbilitySystem。

## 文档

- [角色系统](../../docs/CHARACTER_SYSTEM.md)
- [NPC 图配置与运行指南](ai/NPC_GRAPH_GUIDE.md)
- [世界框架、关卡切换与 NPC 世界引用实施计划](world/WORLD_FRAMEWORK_PLAN.md)
- [世界配置与关卡切换指南](world/README.md)
- [Ability](abilities/README.md)
- [武器与切换](weapons/README.md)
- [背包与装备](inventory/README.md)
- [HFSM](hfsm/README.md)
- [Input](input/README.md)
- [Camera](../../docs/CAMERA_SYSTEM.md)
- [Mission](missions/README.md)
- [Save](saves/README.md)
- [天气系统](weather/README.md)
- [项目总览](../../docs/PROJECT_OVERVIEW.md)
