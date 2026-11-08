<a id="entity-component-framework"></a>
# 实体与组件框架

<a id="hosts-and-components"></a>
## 宿主与组件

项目采用实体与组件组合的架构，并非将纯数据与独立系统分离的 ECS。

- `scripts/gamelogic/gameobject/GameObject2D.cs` 和 `GameObject3D.cs` 是场景宿主，负责持有带类型的导出数组 `Components` 及运行时组件实例。
- `Component2D` 和 `Component3D` 继承 Godot 的 `Resource`；它们的 `Owner` 分别为 `GameObject2D` 和 `GameObject3D`。组件基类由宿主类型决定，不取决于它所控制的子节点维度。
- 可在场景中配置的具体行为应放在 `GameLogic` 中，以 `[GlobalClass] public partial class ... : Component2D/Component3D` 实现。`Camera3D`、`Sprite3D`、`SubViewport`、`CharacterBody2D` 和 `MeshInstance3D` 等原生子节点作为场景数据，由组件控制。
- SpaceLevel 的玩法使用原生 3D 宿主与物理。“2D 角色”指精灵画面（`AnimatedSprite3D`），不代表将 2D 物理宿主投影到 3D。不要为该场景重新引入 `SubViewport`、2D 碰撞代理或 Camera2D 到 Camera3D 的桥接。

<a id="lifecycle-and-ownership"></a>
## 生命周期与所有权

- 初始化、节点查找及依赖解析放在 `OnInit`，逐帧更新放在 `OnUpdate`，物理逻辑放在 `OnPhysicsUpdate`，清理放在 `OnDestroy`。组件不实现 `_Ready`、`_Process` 或 `_PhysicsProcess`，这些钩子由宿主分发。
- 使用 `Owner.GetComponent<T>()` 获取同一宿主的其他行为；通过导出的 `NodePath` 或资源属性配置场景依赖。查找真实节点时使用 `Owner.GetNode...`。
- 宿主在必要时克隆资源定义，并在初始化前设置 `Owner`。导出资源应作为配置使用；可变运行时状态归各自实例所有。若只修改单个宿主使用的共享材质或形状，应先复制该资源。
- 宿主按 `Priority` 从高到低初始化和更新组件，数值越大越先执行。优先使用 `ComponentPriority` 中的命名值，并考虑初始化、物理和逐帧更新时的依赖关系。
- `Priority` 只决定同一宿主内的组件顺序。不同宿主的顺序需要通过 Godot 处理优先级或显式依赖安排，例如让相机宿主在角色宿主之后更新。
- `IsActive` 只控制是否更新，不控制初始化与销毁。宿主还通过 `OnSpawn` / `OnRecycle` 支持对象池；反复激活时应释放信号和引用，避免持续累积运行时子节点。
- 将组件注册到场景的带类型 `Components` 数组；明确需要动态行为时使用 `Owner.AddComponent<T>()`。不要为组件另建一套更新循环。

<a id="scene-wiring-and-migration"></a>
## 场景接线与迁移

玩家和 AI 使用 GameObject3D 宿主及 Component3D 资源。移动组件控制原生 CharacterBody3D 子节点，动画组件通过已有 SpriteFrames 控制 AnimatedSprite3D。平台使用 StaticBody3D 与 CollisionShape3D，相机组件直接控制 Camera3D。

`CharacterMovementComponent3D.MovementSpace` 默认为 SideView，即在 X/Y 平面移动并锁定当前 Z。Free3D 使用同一套重力、跳跃和碰撞实现，在 X/Z 平面移动。移动命令包含 X/Z 轴输入，应先应用运动空间约束，再归一化。输入、AI 目标选择、定向能力和动画速度均须兼容两种模式。原生世界坐标以米为单位，Y 正方向朝上；精灵的 `pixel_size` 仅决定美术缩放。

图系统通过 IGameObject / IComponent 发现、调用和绑定组件，兼容两种维度的宿主。不要在共享图基础设施中写死 Component2D。重命名组件时，应同步迁移序列化图中的组件名称、动作引用、动画路径和阈值。重命名脚本时保留脚本 UID。

角色持久化格式第 3 版保存 XYZ 世界坐标和朝向。旧格式第 1 / 2 版的像素位置仅转换一次：`(x * 0.01, -y * 0.01, 0)`。不要每帧重复旧坐标转换。GameObject3D.PersistentId 为导出属性，用于保留场景中配置的存档身份。

完成前应检查场景组件数组、实例独立状态、激活与清理、物理及逐帧执行顺序，以及图和存档的兼容性。运行 `dotnet build yu.csproj` 和相关 Godot 冒烟场景。测试运行器可以继承 `Node`，其职责是验证 EC 实现。
