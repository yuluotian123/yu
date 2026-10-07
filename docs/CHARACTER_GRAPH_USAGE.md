# 角色系统与图使用说明

本文以当前代码为准，说明玩家角色、CharacterGraph、Locomotion HFSM、Ability 和组件 Property 节点的使用方式。

## 系统分工

```text
Player Input / AI
        |
        v
CharacterGraph --------------------> AbilitySystem
        |                                  |
        +---- Movement intent              +---- Ability Timeline
                    |                                  |
                    v                                  v
             CharacterMovement -----> CharacterAnimation
                    |                     Locomotion HFSM
                    +--------------------------+
```

| 组件 | 作用 | 当前优先级 |
| --- | --- | ---: |
| `PlayerCharacterInputComponent3D` | 将输入模块转换为 `ICharacterInputProvider` | 100 |
| `SimpleAICharacterControllerComponent3D` | 驱动 BehaviorTree，并提交移动/战斗意图 | 100 |
| `CharacterGraphComponent3D` | 执行玩家输入、生命周期和 Ability 编排 | 90 |
| `AbilitySystemComponent3D` | 授权、冷却、优先级、打断和 Ability 运行时 | 55 |
| `CharacterMovementComponent3D` | 消费移动命令、处理跳跃/重力并移动 `CharacterBody3D` | 50 |
| `CharacterAnimationComponent3D` | 运行 Locomotion HFSM 并选择 `AnimatedSprite3D` 动画 | 20 |

CharacterGraph 不负责物理和 Locomotion 状态；它提交意图。Movement 计算最终结果，Animation 再读取结果并选择动画。AI 可以直接使用 Movement 和 Ability API，不需要挂载 CharacterGraph。

## 创建玩家角色

1. 创建 `GameObject3D`，并添加 `CharacterMovementComponent3D`、`CharacterAnimationComponent3D`、`AbilitySystemComponent3D`、`PlayerCharacterInputComponent3D` 和 `CharacterGraphComponent3D`。
2. 在 Movement 中配置 `BodyPath`、`VisualRootPath`、速度、跳跃和重力参数。
3. 在 Animation 中配置 `SpritePath` 和 `LocomotionGraph`。
4. 在 AbilitySystem 中配置 `AbilitySet`，确保 CharacterGraph 使用的每个 `AbilityId` 都已授予。
5. 在 CharacterGraph 中配置输入节点、Ability 节点和组件 Action 节点。

玩家场景示例：[player.tscn](../assets/scenes/player.tscn)。

## CharacterGraph 输入

CharacterGraph 是 Character 专用的 FlowGraph。常见连接如下：

```text
Axis1D(player_move_left, player_move_right)
    -> Component Call: AddMovementInput

Pressed(player_jump)
    -> Component Call: RequestJumpStart
    -> Component Call: SetJumpSustain(true)

Released(player_jump)
    -> Component Call: SetJumpSustain(false)

Pressed(player_attack) -> Character Ability: attack
Pressed(player_dash)   -> Character Ability: dash
```

`Axis1D` 会先应用死区、阈值、缩放和反向设置，再将有符号值传给 Movement。跳跃按下和松开应使用两个输入节点：`RequestJumpStart` 是一次性请求，`SetJumpSustain` 负责持续状态。

## Ability

Ability 由 `AbilityResource`、`AbilitySetResource` 和 `AbilityFlowGraphAsset` 组成。

1. 创建 Ability FlowGraph 和 Timeline。
2. 创建 `AbilityResource`，设置稳定的 `AbilityId`、冷却和激活策略。
3. 将 Ability 加入角色的 `AbilitySet`。
4. 在 CharacterGraph 中添加对应 Ability 节点并连接输入。
5. 需要打断或完成后联动时，添加 `Interrupt` 或 `Completion` 关系。

Ability Timeline 可以请求动画、速度覆盖、移动锁、镜头效果和事件。AbilitySystem 会再次检查授权、冷却、并发和优先级，因此 CharacterGraph 的关系边不是最终授权来源。

需要组件的 Ability Action 使用显式组件引用。可以独立创建节点的 Action 通过 `Component Reference` 节点连入 `Component` 端口；Timeline Clip、Marker 等嵌套 Action 直接将右侧 `Components` 面板中的组件拖到 Action Inspector。Action 不会再隐式搜索宿主组件。

## Locomotion AnimGraph

`CharacterAnimationComponent3D` 内部运行 `HfsmGraphAsset`。Locomotion 图应该只描述动画状态，例如 Idle、Run、Jump、Fall 和 Land。

Movement 通过黑板绑定向 HFSM 提供这些值：

| 黑板 Key | 组件成员 | 类型 |
| --- | --- | --- |
| `Character.Movement.Mode` | `MovementModeName` | `string` |
| `Character.Movement.IsOnFloor` | `IsOnFloor` | `bool` |
| `Character.Movement.MoveAxisX` | `MoveInputX` | `float` |
| `Character.Movement.VelocityY` | `VelocityY` | `float` |

AnimGraph 的条件读取黑板，不直接在状态条件中访问组件 Property。打开动画图黑板后，使用 `Component Binding` 选择宿主实际存在的组件和兼容成员。没有有效宿主时不能创建或修改绑定。

运行时顺序是：Animation 读取绑定值，HFSM 判断状态，Animation 请求播放动画，最后由 `AnimatedSprite3D` 播放。Ability 动画请求可以用优先级覆盖 Locomotion 请求，Ability 完成或取消后会自动恢复 Locomotion。

## Component Property Get / Set

CharacterGraph 支持通过端口直接传递 Property 值。

### Get

在编辑器右侧 `Components` 面板中选择：

```text
Components -> 宿主 -> 组件 -> Properties -> Get xxx
```

创建的 `Component Get` 有一个 `Execute` 输入和一个值输出。它在运行时读取 Property，并将值放到输出端口。

### Set

如果组件成员声明为可写，在同一位置会出现：

```text
Properties -> Set xxx
```

`Component Set` 有 `Execute` 输入和一个值输入。值可以来自 `Component Get` 或其他值输出节点：

```text
Get Value -> Set Value
```

也可以在 Set 节点 Inspector 中填写 `Value (used when unconnected)`，作为没有值连线时的常量值。

`ReadWrite` 标记本身不会生成 setter。组件属性必须真的有 public/private setter，或通过单独的可写方法实现写入。像 `IsOnFloor` 这类由物理状态计算出的值通常应保持只读；移动和跳跃应调用 `SetHorizontalVelocity`、`SetVerticalVelocity`、`AddMovementInput`、`RequestJumpStart` 等 Action。

## 黑板规则

- CharacterGraph 的 Property Get/Set 通过端口传值，不再使用 `InputKey` 或 `OutputKey` 黑板中转。
- AnimGraph 使用黑板作为组件状态快照，绑定方向应为 `Component to Blackboard` 或 `Two Way`。
- 黑板条目必须有唯一 Key 和匹配的 Value 类型。
- 组件绑定只能选择当前宿主实际存在的组件；没有宿主时已有绑定会保留，但编辑控件会被禁用。
- 全局黑板属于场景中的 `GraphBlackboardNode`；本地黑板属于当前图资源。

## 编辑器操作

1. 在 Inspector 中打开 GraphAsset 或组件上的图属性。
2. 在画布空白处右键搜索节点。
3. 从 Components 面板双击 Action 或 Property 创建组件节点。
4. 拖动端口创建执行线和值线；连接前会检查端口类型。
5. 点击工具栏 `Blackboard` 编辑本地/全局黑板。
6. 点击 `Save` 或按 `Ctrl+S` 保存；验证失败时保存会被阻止。

图运行时数据保存在 `GraphAsset.GraphJson` 中。修改图资源后应保存图资源或包含场景，确保编辑器中的本地资源变更被持久化。

## 验证

项目构建命令：

```powershell
dotnet build yu.csproj
```

运行 Spacelevel 检查输入、Movement、Ability、动画变量和 AI 场景边界。
# Action 依赖模式

图资源的 `ActionDependencyMode` 决定组件如何传给 Action：

- `HostBound`：用于角色、AI 和 HFSM 图。需要组件的 Action 必须在 Inspector 拖入组件，或连接 Component Reference 节点；运行时不会自动查找宿主组件。
- `Reusable`：用于 Ability/Skill 图。资源不保存场景组件，运行时根据 Action 所需的组件类型从当前 Ability 宿主自动查找第一个匹配组件。

编辑图时在工具栏切换模式。HostBound 图保存时会拒绝缺少显式组件引用的 Action；Reusable 图中的组件引用仅作为编辑提示，不会绑定具体宿主。
