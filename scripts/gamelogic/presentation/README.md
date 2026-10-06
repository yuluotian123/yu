# 原生 3D 世界与精灵人物

SpaceLevel、玩家和 AI 使用原生 3D 坐标及物理。行为仍由 GameObject3D 的 Components 数组驱动，原生节点不承载独立玩法脚本。

- 玩家：CharacterMovementComponent3D 控制 CharacterBody3D；CharacterAnimationComponent3D 驱动 AnimatedSprite3D，复用原 SpriteFrames 和动画图。
- AI：SimpleAICharacterControllerComponent3D 使用同一移动组件与图动作；Sprite3D 保留原平面形象。
- 静态平台：保留 GameObject3D，直接在场景中配置 MeshInstance3D 与 StaticBody3D/CollisionShape3D，无需碰撞同步组件。移动或旋转平台根节点时，子节点一起变化；修改网格尺寸时，需同步调整碰撞形状。只有移动、坍塌等玩法行为才需要组件。
- 相机：CameraRig/Camera3D 直接保存在关卡中。SceneCameraComponent3D 自动绑定 Player 并处理跟随；ICameraModule 只路由当前相机控制，不生成节点。支持正交/透视、切换目标、定点观察、变焦、手动上下观察和震屏。详见 [场景相机](../../../docs/CAMERA_SYSTEM.md)。

## 横版与自由移动

在 Player 的 Components 中展开 CharacterMovementComponent3D，将 MovementSpace 从 SideView 改为 Free3D 即可切换。默认 SideView：A/D 横移、空格跳跃，物理锁定当前 Z。Free3D：A/D 控制 X，W/S 控制 Z，空格仍跳跃。切回横版时锁定当前位置的 Z，不瞬移回原平面。自由模式不占用 W/S 做相机观察。

两种模式共用加速、减速、跳跃缓冲、土狼时间、重力与 3D 碰撞。自由移动对角线归一化，冲刺使用水平朝向向量，动画依据移动输入总量切换。AI MoveToTarget 支持 X/Z 追踪；Patrol 仍保留横向巡逻语义。自由移动使用当前固定角度相机与单套精灵，不包含绕角色旋转镜头或八方向美术。

## 坐标、资产与存档

世界参数统一使用米，Y 向上，跳跃初速度为正。旧像素单位按 100px = 1m 一次性迁移到场景、移动参数、技能和震屏配置中。Sprite3D.pixel_size = 0.01 只决定美术尺寸。无需 SubViewport、Camera2D 或 2D 物理代理。

角色存档 schema 3 保存 XYZ、旋转和水平朝向。schema 1/2 的 XY 像素位置只在读取旧档时转换一次，技能冷却和 flags 保持兼容。场景 PersistentId 保留。

## 验证

运行 `dotnet build yu.csproj` 构建，然后打开 [spacelevel.tscn](../../../assets/scenes/spacelevel.tscn) 检查落地、跳跃、横版与自由移动、碰撞、冲刺、动画、AI、相机和存档。帧动画回归入口为 `scripts/test/greatsword_animation_smoke.gd`。
