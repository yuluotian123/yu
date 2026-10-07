# 技能图与行为树基础动作库

技能 `AbilityFlowGraph` / 普通 FlowGraph 的 Action 节点与行为树 Action 叶节点共用同一套动作定义。节点右侧 Details 中添加动作；条件放在 Condition 节点中。行为树右键菜单另有 `BehaviorTree/Decorator → Timeout`。

## 名称、分类与菜单

动作与条件通过 `GraphCallableAttribute` 声明英文名称、中文译名、功能分类和支持的执行环境。公共目录 `GraphCallableCatalog` 供 Flow、行为树、单动作选择器和 Timeline 使用；搜索支持中文、英文和原类名。

| 分类 | 中文译名（英文名） |
| --- | --- |
| 黑板与数据 | 设置黑板（Set Blackboard）、累加黑板数值（Add Blackboard Number）、比较黑板（Compare Blackboard） |
| 流程 | 等待秒数（Wait Seconds） |
| 目标查询 | 寻找最近目标（Find Nearest Target）、目标有效（Target Valid）、目标距离判断（Target Within Distance） |
| 移动 | 设置朝向（Set Facing）、面向目标（Face Target）、定向移动（Move In Direction）、接近目标（Move To Target）、停止移动（Stop Movement）、往返巡逻（Patrol）、施加冲刺速度（Apply Dash Velocity） |
| 移动 / 跳跃 | 跳跃（Jump）、周期跳跃（Periodic Jump） |
| 移动 / 检测 | 处于地面（Is On Floor）、前方有地面（Ground Ahead） |
| 技能 | 释放技能（Use Ability）、取消技能（Cancel Ability）、技能可用（Can Use Ability） |
| 动画与表现 / 动画 | 请求动画（Request Animation） |
| 动画与表现 / 特效 | 刀光表现（Slash Visual）、技能颜色效果（Ability Visual Color） |
| 动画与表现 / 镜头 | 镜头震动（Camera Shake） |
| 组件访问 | 调用组件方法（Call Component Method）、读取组件属性（Get Component Property）、写入组件属性（Set Component Property） |
| 调试 | 输出日志（Log） |

状态机、HFSM、任务专用条件保留自己的分类与执行上下文，并补充中文译名。持续任务不会出现在 Timeline 菜单，依赖时间轴生命周期的刀光与颜色效果也不会出现在行为树菜单。

## 通用移动行为

- 定向移动每帧读取 Direction（-1～1），支持黑板驱动；Duration 为 0 时持续至中断，大于 0 时按时完成。
- 往返巡逻围绕任务启动时的位置，在 Distance 范围内左右移动。支持初始方向、边缘检测、探测距离、转向停顿。任务持续 Running，需要时用 Timeout 或上层分支中断。
- 跳跃默认要求落地；HoldDuration 控制按住时间，结束后释放并 Success。Success 表示输入动作完成，不表示已经落地；需要落地判定时使用“处于地面”。
- 周期跳跃在 Interval 到达且落地时请求起跳，持续 Running。Interval 必须大于 0，HoldDuration 必须大于等于 0。
- 接近目标只处理水平距离，到达成功，超时或目标失效时失败；不执行寻路或自动跳跃。
- 移动类动作默认使用 AI 优先级 100，可配置 Priority；Movement 负责最终命令优先级、跳跃锁和物理规则。
- 这些动作只依赖移动组件与黑板，不依赖 SimpleAICharacterControllerComponent3D。每次运行创建独立任务，不在资源或 Controller 中共享进度。

参数中的 `Use blackboard` 决定从常量还是黑板读取。数值参数必须为有限数值；移动和技能等待的 Timeout 必须大于 0。

## 目标表示

第一版沿用现有黑板类型：`Target` 可以是世界坐标 Vector2，或 String 类型的节点路径。Find Nearest Target 写入绝对节点路径；目标节点删除后，依赖它的动作/条件会失败。固定坐标请使用 Vector2。

Group 是 Godot 节点组名，需要将可选目标加入该组。它不推断阵营，也不执行视线或遮挡检测。Move To Target 适配现有横版 CharacterMovementComponent3D，只处理水平接近，不执行寻路、跳跃或避障。

## Reusable 与 HostBound

- Reusable：自动从当前执行宿主找兼容的组件。
- HostBound：在 Movement / Ability System 字段中从左侧 Components 拖入组件，保存类型与槽位。
- 单个 Action 节点也支持通过 Component 输入提供依赖。
- 黑板、等待、目标查询等不需要组件的动作在两种模式下行为相同。

图资源只保存配置。等待时间、当前动作索引和技能执行记录属于每个运行时；多个角色可共享同一张图。

## 完成与中断

Flow Action 节点的 0 号输出保持原有成功路径，1 号输出为 Failure。持续动作完成前不会推进成功路径。行为树对应返回 Running / Success / Failure；同一个 Action 列表内已经完成的动作不会因后续动作仍在 Running 而重复执行。

Use Ability 默认等待完成并在中断时取消自己启动的那一次技能。技能被外部取消或被新一次激活替换时返回 Failure；旧动作不会取消后来的激活。关闭 `Cancel On Abort` 后，中断或超时只停止等待，技能继续执行。请求失败不会自动授予技能；请先配置宿主的 AbilitySet。

Timeout 装饰器在超时后中断子树并返回 Failure；重新进入时重新计时。停止 Flow 或行为树运行时也会取消正在执行的任务。

Timeline 的 clip/marker 仍用于瞬时操作与专用时间轴动作。新增的瞬时动作在 clip 的 Start / marker 的 Event 执行；持续动作仅在 Action 节点中使用，编辑器会从时间轴选单过滤并在校验时拒绝。

## 最小追击攻击配置

1. 为宿主配置 CharacterMovementComponent3D、AbilitySystemComponent3D，并在 AbilitySet 中授予 `attack`。
2. 将敌方 Node2D 加入 `enemies` 组。行为树设为 Reusable，黑板声明 String `Target`。
3. Root → Sequence：Action（Find Nearest Target）→ Selector。
4. Selector 第一分支为 Sequence：Condition（Target Within Distance，100；Can Use Ability，attack）→ Action（Face Target；Use Ability，attack，Wait For Completion 开启）。
5. 第二分支为 Action（Move To Target，Arrival Distance 80，Timeout 5）。下一次树 Tick 会重新检查攻击分支。

技能图示例：Entry → Action（Find Nearest Target；Face Target）→ Timeline（已有动画/攻击表现）→ Action（Wait Seconds）→ Return；把每个 Action 的 Failure 口接到失败处理或结束节点。技能运行时拥有自己的黑板，第一版 Use Ability 不自动传入行为树的 Target，需要在技能图内自行查询。

## 验证

编译后在实际业务图和场景中检查动作执行、取消、目标失效以及资源实例隔离。

测试场景执行完毕自动退出，不用于游戏主场景。

## CharacterGraph 中的入口

CharacterGraph 使用直接组件成员节点调用、读取和写入组件；Action 菜单隐藏对应包装类型。技能使用专用“激活技能”节点，保留关系边和打断窗口，避免通过通用 Use Ability 绕过规则。普通 Flow 和行为树保持完整动作库。

Sequence、Wait Event 和 Publish Event 已成为通用 Flow 节点。Wait Event 只接收进入节点后的新事件，默认 5 秒超时，提供 Received/Timeout 出口。
