# GameLogic HFSM

GameLogic HFSM 基于 GraphPlugin StateGraph，负责状态、条件、复合状态和黑板。当前角色架构只把 HFSM 用于动画 Locomotion；CharacterGraph 已改为独立 FlowGraph，不再继承 HFSM。

## 当前职责

```text
CharacterMovementComponent2D
  -> final velocity / IsOnFloor / MovementMode
  -> CharacterAnimationComponent2D
  -> Locomotion HFSM
       Idle / Run / Jump / Fall / Land
  -> animation request arbitration
  -> AnimatedSprite2D
```

- `HfsmRuntime`：通用状态图运行时。
- `HfsmComponent2D`：可独立挂载的通用 HFSM 组件。
- `CharacterAnimationComponent2D`：内部启动 Locomotion HFSM 并发布 Movement 快照。
- `HfsmAnimationStateNodeData`：向 Animation 组件提交 Locomotion 动画请求。

默认 Locomotion 资源：[character_locomotion_hfsm.tres](../../../assets/graphs/character_locomotion_hfsm.tres)。

## 与 CharacterGraph 的关系

[character_graph.tres](../../../assets/graphs/character_graph.tres) 是玩家输入与 Ability 编排图，包含生命周期、Input、Movement intent、流程和 Ability 节点。它不包含 Idle、Locomotion 或动画状态，也不供 AI 使用。

两张图通过 Movement 的最终结果间接协作：CharacterGraph 向 Movement 提交玩家意图，Movement 计算结果，Locomotion HFSM 再根据结果选动画。

完整结构见 [角色系统](../../../docs/CHARACTER_SYSTEM.md)。
