# HD-2D 场景镜头

Spacelevel/CameraRig/Camera3D 使用透视、30° FOV、4.1° 俯角和约 9m 拍摄距离（人物在中心下方、上方可见天空和云的横版构图）。
Camera3D 节点直接调整镜头，hd2d_free_3d.tres 调整跟随、死区和前视。
CameraFramingTarget 是编辑器构图时角色所在位置的参照点；它不会传送或限制角色。
读档后相机按同一构图跟随恢复后的角色位置，不把存档位置误当作镜头偏移基准。

Player 的 MovementSpace=Free3D：W/S 控制世界 -Z/+Z，A/D 控制 X；空格跳跃。
镜头固定朝向 -Z，因此 WASD 对应画面上下左右。斜向输入仍归一化，不会加速。
相机跟随 X/Z，跳跃 Y 使用更慢的跟随；W/S 不再用于抬低镜头。
平台的可见尺寸与碰撞一起扩展为 44×32 米，保留原地面高度。
角色保持竖直的 Y 轴 billboard，使用当前已有的左右像素动作；当前资源没有四/八方向背面动画。

## 调参位置与作用

选中 Camera3D 或 CameraRig 可查看节点的 Editor Description 中文说明；自定义导出参数的中文 XML 注释位于对应 C# 属性上。
以下数值是当前 Spacelevel 的配置，未在资源里显式保存的属性使用脚本默认值。单位为米，X/Y/Z 均为世界轴。

| 参数 / 位置 | 当前值 | 调整效果 |
| --- | --- | --- |
| Camera3D / Position | 约 9 米 | 沿相机局部 -Z 移动会拉近，+Z 移动会拉远。初始构图请直接移动节点。 |
| Camera3D / Rotation Degrees.X | -4.1° | 接近 0 更平视、偏横版；更负则更俯视。角度和位置需配合调整以对准角色。 |
| Camera3D / Projection | Perspective | 透视可体现前后移动的远近变化；Orthogonal 为正交。 |
| Camera3D / FOV | 30° | 透视视野；越小主体越大、视野越窄。默认 Keep Height 时为竖直视角。 |
| Camera3D / Size | 7 | 正交/视锥投影的尺寸，越小主体越大；不影响透视。 |
| Camera3D / Far | 500 | 最远可见距离；设太小可能裁掉天空和云。 |
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

当前场景相机位置为 (1.6, -7.15, 8.8033)，X 旋转为 -4.1°，FOV 为 30°。人物约位于画面高度的三分之二处（随动作、前视和跟随死区略有变化），上半部保留天空和云。保持角度和 Z 距离时，提高相机 Position.Y 会让人物更靠下，降低则让人物上移；调整角度时需要配合高度以避免角色贴近底边。
CameraFramingTarget 表示场景构图时的角色位置；修改角色初始位置时同步移动参照点和镜头，以保留构图。该节点不会修改存档或传送角色。
