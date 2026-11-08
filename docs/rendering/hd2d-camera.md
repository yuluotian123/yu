# HD-2D 场景镜头

ForestTownLevel/CameraRig/Camera3D 当前使用透视投影、FOV 45°、20° 俯角；CameraOffset 为 (0, 3, 8)。Size 5.8 保留为正交模式的备用配置。
镜头位置和角度统一在 CameraRig 的 SceneCameraComponent3D 中调整：开启 UseComponentFraming，使用 CameraOffset 和 CameraRotationDegrees。Camera3D 保留投影、Size、Near/Far；hd2d_free_3d.tres 调整跟随、死区和前视。构图参数支持编辑器预览；保存前恢复 Camera3D 备用构图，避免把预览位置重复写入场景。关闭 UseComponentFraming 的其他关卡沿用原节点构图。
CameraFramingTarget 是编辑器构图时角色所在位置的参照点；它不会传送或限制角色。
森林和工坊的 SceneCameraComponent3D.FramingOriginPath 均绑定到独立的 CameraFramingTarget。只调整出生点时，移动 EntryDefault 或其子节点 Arrival 即可，不移动 CameraFramingTarget；运行时镜头会按原构图平移到玩家的新位置。FramingOriginPath 留空会把激活时玩家的位置算作构图基准，导致更改出生点后镜头仍保留原场景位置；绑定到 Arrival 则会让移动出生点同时改变构图基准。
读档后相机按同一构图跟随恢复后的角色位置，不把存档位置误当作镜头偏移基准。

Player 的 MovementSpace=Free3D：W/S 控制世界 -Z/+Z，A/D 控制 X；空格跳跃。
镜头固定朝向 -Z，因此 WASD 对应画面上下左右。斜向输入仍归一化，不会加速。
相机跟随 X/Z，跳跃 Y 使用更慢的跟随；W/S 不再用于抬低镜头。
森林 CameraRig 的 CameraOcclusionComponent3D 检测镜头与角色中心、左右和上下边缘之间的真实网格遮挡。五个采样点连续全部受挡 0.2 秒（OcclusionDelay）才触发，用射线近似判断角色整体被遮住；任一点恢复可见或切换目标都会重新计时。检测按世界空间命中距离逐层选择完整遮挡层，后方只剩部分遮挡时停止，不连带隐藏旁边的房屋。camera_occluder 分组及子网格参与检测，地面、水和角色不参与。

树木和房屋在持续完全遮挡角色后整体隐藏，同一物体的所有子网格同步渐隐，包括背面、屋顶和树冠。圆形/椭圆开窗及 CutoutHalfSize、CutoutFeather 已移除。OccludedTransparency 控制整个物体的隐藏程度，1 为完全隐藏，0 关闭这项角色遮挡处理；正式关卡和聚落预览均设为 1。OcclusionDelay 控制持续遮挡时长，FadeSeconds 控制渐隐时长，视线畅通后按 RestoreDelay 和 RestoreSeconds 恢复。共享材质、碰撞、原生阴影和 GI 几何保持原值；项目材质通过实例 shader 参数隐藏全部表面，不修改模型整体 Transparency。其他未接入该 shader 的模型保留实例透明度兼容路径。

镜头本身穿入实体时，由同一个组件的 HideCameraInteriors 处理。相机位置或近裁面四角进入房屋、实体树干后，立即隐去该栋房屋或整棵树的全部可见表面，避免看到背面墙体、屋顶内部或树干内侧；退出后按 RestoreDelay 和 RestoreSeconds 恢复。此项默认开启，独立于 OccludedTransparency 和 NearFadeDistance，不移动或旋转镜头。房屋检测使用实际碰撞体的平面范围，阁楼部分再确认头顶有真实屋顶；树木使用不透明树皮三角面构成的实体，不把透明叶片和粗略树干碰撞盒中的空气算作内部。识别 asset_category 为 building/tree 的模型根，或 camera_solid_occluder 分组；检测仍限制在 camera_occluder 搜索范围内。

遮挡检查在正式森林关卡中进行：移动到建筑和树木后方，观察多层遮挡、渐隐恢复及切换目标后的状态。穿入保护的独立验证脚本为 `scripts/tools/rendering/validate_camera_interiors.gd`，覆盖实际十栋房屋和三种树干、邻近模型不误隐、近裁面穿墙、镜头切换及原覆盖值还原。
树干、建筑和叶片材质在 `IN_SHADOW_PASS` 中跳过整体隐藏与近处渐隐，叶片保留原始纹理 Alpha 裁切。近处渐隐使用实例 uniform 索引 8，角色遮挡与镜头穿入共用 camera_solid_hide（索引 11），取两种隐藏强度的较大值；旧窗口参数索引 9、10 已移除。切换镜头或销毁组件时恢复原始覆盖值，不写回模型资源。整体隐藏不改变模型可见性、原生阴影、GI 几何或碰撞。
平台的可见尺寸与碰撞一起扩展为 44×32 米，保留原地面高度。
角色保持竖直的 Y 轴 billboard，使用当前已有的左右像素动作；当前资源没有四/八方向背面动画。

## 调参位置与作用

选中 Camera3D 或 CameraRig 可查看节点的 Editor Description 中文说明；自定义导出参数的中文 XML 注释位于对应 C# 属性上。
以下数值是当前 ForestTownLevel 的配置，未在资源里显式保存的属性使用脚本默认值。单位为米，X/Y/Z 均为世界轴。

| 参数 / 位置 | 当前值 | 调整效果 |
| --- | --- | --- |
| SceneCameraComponent3D / UseComponentFraming | true | 启用组件构图；关闭后使用 Camera3D 的备用位置和旋转。 |
| SceneCameraComponent3D / CameraOffset | (0, 3, 8) | 相对于构图参照点/运行时跟随目标的世界空间位置，单位米；不随帧数累积。 |
| SceneCameraComponent3D / CameraRotationDegrees | (-20, 0, 0) | 世界空间欧拉角；X 接近 0 更平视，更负则更俯视。28° 对照会引入过多树冠，因此使用 20°。 |
| Camera3D / Projection | Perspective | 透视投影，物体大小随距离变化。 |
| Camera3D / FOV | 45 | 透视视野角度；越小视野越窄、人物越大。 |
| Camera3D / Size | 5.8 | 仅正交模式生效，越小人物越大。 |
| Camera3D / Near | 0.001 | 最近裁切距离。 |
| CameraRig / CameraOcclusionComponent3D / NearFadeDistance | 1.0 | 正交近处表面渐隐范围，单位米；当前透视模式不启用，0 关闭，编辑器可预览。 |
| CameraRig / CameraOcclusionComponent3D / HideCameraInteriors | true | 相机或近裁面穿入房屋、实体树干时隐去全部表面，离开后恢复，保留阴影、GI 和碰撞。 |
| CameraRig / CameraOcclusionComponent3D / OccludedTransparency | 1 | 角色持续完全被遮挡后，隐藏整栋房屋或整棵树；0 关闭。 |
| CameraRig / CameraOcclusionComponent3D / OcclusionDelay | 0.2 | 五个检测点连续受挡的触发时长，单位秒。 |
| Camera3D / Far | 204 | 最远可见距离。 |
| Profile / BaseOffset | (0, 0.8, 0)，脚本默认 | 焦点基准偏移；初始化时会补偿以保持场景构图，不是拉近镜头的参数。 |
| Profile / DeadZone | (0.32, 0.4, 0.32) | 死区总宽度，两侧各一半；越大镜头越少随小幅移动。0 关闭死区，平滑仍生效。 |
| Profile / FollowSmooth | (7, 4, 7) | 各轴跟随响应速度；越大越快，0 直接到位。Y 较慢可缓和跳跃起伏。 |
| Profile / LookSmooth | 5 | 前视偏移过渡速度；越大越快，0 直接到位。 |
| Profile / ZoomSmooth | 5 | SetViewHeight 的 Size 过渡速度，不改变 FOV 或实际拍摄距离。 |
| Profile / TeleportDistance | 6.4 | 单次位移达到此值时立即跟随；0 关闭瞬移检测。 |
| Profile / LookAheadDistance | 0.3 | 朝角色面向方向前视；0 关闭，停止移动仍保留朝向。 |
| Profile / VerticalVelocityLookDistance | 0.08 | 跳跃/下落时最大向上/下前视距离；0 关闭。 |
| Profile / VerticalVelocityReference | 7.6 | 竖直速度达到此值（米/秒）时前视达到上限；越大越不敏感。 |
| Profile / EnableManualLook | false | 手动抬低镜头开关；Free3D 模式始终跳过此功能。 |
| Profile / LookUpAction / LookDownAction | camera_up / camera_down | 非 Free3D 模式下手动观察使用的输入动作。 |
| Profile / ManualLookDistance | 0.8 | 手动观察最大高度偏移，有输入时替代跳跃前视。 |

当前 CameraFramingTarget 为 (1.56, 0.55, 22.62)。运行时使用“玩家位置＋镜头相对构图参照点的偏移”，保留原构图、跟随死区和前视。透视模式通过 FOV 或 CameraOffset 调整人物大小；正交模式使用 Size。

近处渐隐统一由 `CameraOcclusionComponent3D.NearFadeDistance` 管理。选中 `CameraRig`，展开 Components 中的 `CameraOcclusionComponent3D` 即可调整，当前为 1.0 米，0 关闭，仅正交模式生效。组件在 `OccluderRootPath` 范围内查找 `camera_near_fade` 分组，当前包含 274 棵乔木和 10 栋房屋；模型实例不再单独保存距离。切换到其他镜头、退出组件或编辑器保存前会恢复原实例参数，保存后自动恢复预览。

这里的 0 只关闭近处表面渐隐，不关闭角色遮挡时的整体隐藏（由 OccludedTransparency 控制）或穿入保护（由 HideCameraInteriors 控制），也不会取消 Camera3D 自身的近裁面。穿入保护针对房屋与实体树干；其他模型与近裁面相交时仍可能产生平直截面，可通过 SceneCameraComponent3D 的 CameraOffset 调整距离。

正式森林和聚落预览的两个相机组件均设为 Local To Scene，让 EC 宿主直接使用场景实例中的组件。运行中在 Remote 树的 CameraRig / Components 调参即可作用到实际运行组件；否则会只改配置资源，而运行克隆继续使用启动时的旧值。Local 树是编辑器场景配置，修改后需要重新运行关卡；Remote 调参不自动保存到场景。重新生成聚落时也必须保留这两个组件的 Local To Scene 标记。

着色器 `assets/shaders/town/camera_near_fade.gdshaderinc` 只负责逐表面渲染：靠近正交近裁面的表面按固定 4×4 有序图案渐隐，当前范围下 0.25 米内清空；没有逐帧随机噪声，也不会因为某部分接近镜头就隐藏整栋旁边的房屋。阴影通道跳过此处理。角色被完整遮挡后的原有持续检测仍独立工作。

镜头位置与角度保存在 `SceneCameraComponent3D`，投影、FOV、Size 和裁切面保存在 `Camera3D`，遮挡、近处渐隐和穿入保护保存在 `CameraOcclusionComponent3D`；布局替换工具保留这些配置。草地与环境配置不随 `NearFadeDistance` 或 `HideCameraInteriors` 改变。
重新选择编辑器构图参照位置时移动 CameraFramingTarget；组件预览相机会跟随。单独改变出生位置不需要移动参照点。该节点不会修改存档或传送角色。
