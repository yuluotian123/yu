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
ai/            BehaviorTree Controller 和 Actions
hfsm/          通用 HFSM；角色侧仅用于 Locomotion 动画
```

CharacterGraph 是 `FlowGraphAsset` 的业务扩展，不属于 HFSM。AI 不使用 CharacterGraph，而是直接调用 Movement 和 AbilitySystem。

## 文档

- [角色系统](../../docs/CHARACTER_SYSTEM.md)
- [Ability](abilities/README.md)
- [HFSM](hfsm/README.md)
- [Input](input/README.md)
- [Mission](missions/README.md)
- [Save](saves/README.md)
- [项目总览](../../docs/PROJECT_OVERVIEW.md)
