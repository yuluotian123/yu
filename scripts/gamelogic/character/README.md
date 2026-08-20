# Character Runtime

角色执行层按职责拆分：

```text
graph/       玩家专用 CharacterGraph（基于 FlowGraphAsset）
input/       输入提供者接口
movement/    玩家与 AI 共用的移动意图和物理实现
animation/   Locomotion HFSM 与动画请求仲裁
```

CharacterGraph 使用 Flow runtime 来支持多入口事件、Delay、WaitEvent 和异步 Ability 节点，但不使用 StateGraph/HFSM 语义。AI 不挂载 CharacterGraph，直接从 BehaviorTree 调用 Movement 和 AbilitySystem。

Ability 的授予、冷却、优先级和 Timeline 位于 [abilities](../abilities/README.md)。完整结构见 [角色系统](../../../docs/CHARACTER_SYSTEM.md)。
