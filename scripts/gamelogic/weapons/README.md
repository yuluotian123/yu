# 大剑与太刀

Player 使用 `WeaponEquipmentComponent3D` 管理两个武器槽，新角色默认大剑，按 Q 循环切换已装备的大剑 / 太刀。左键使用当前武器的普攻。B 打开[武器行囊](../inventory/README.md)，支持装备与卸下，至少保留一把武器；攻击或冲刺期间切换会延迟到当前能力结束，连续按 Q 可改变或取消待切换目标。

| 武器 | 能力 ID | 动作时长 | 冷却 |
| --- | --- | --- | --- |
| 大剑 | `greatsword_attack` | 0.60 秒 | 0.75 秒 |
| 太刀 | `katana_attack` | 0.36 秒 | 0.42 秒 |

当前区分持械动作、攻击动作和节奏；伤害、命中判定与连招尚未接入。

## 资源与组件

- [武器定义](../../../assets/resources/weapons/)：`greatsword.tres` / `katana.tres` 各自引用攻击能力和动画配置。
- `WeaponDefinition` 是共享只读配置；`WeaponAnimationProfile` 记录 SpriteFrames、画布、脚底锚点和像素比例。
- `WeaponEquipmentComponent3D : Component3D` 在 Animation 初始化后、Persistence 注册前初始化。场景配置定义可用武器，运行实例持有独立槽位顺序、当前槽位和待切换槽位。
- CharacterGraph 把 `player_attack` 路由到 `TryPrimaryAttack`，把 `player_switch_weapon` 路由到 `RequestNextWeapon`。移动、跳跃和冲刺仍用原有输入链路。
- 基础 AbilitySet 只保留冲刺。装备组件初始化时授予两个唯一 ID 的普攻能力；切换不撤销能力、不重建 AbilityRuntime，因此不会刷新冷却。
- 切换整体更新武器动作配置；`CharacterAnimationInstance.RefreshPlayback` 强制重新播放同名动画，保留 Locomotion 状态机。两套资源均为 224×224 帧、约 96px 人物高度、脚底 (112,192)，不移动物理碰撞体。
- 两套普攻沿用现有 Ability Timeline，阻止移动和跳跃；新武器的动作时长与时间轴必须一致。

## 存档

角色分区 schema 5 保存所有权、`equipment.equipped_weapon_id` 和 `equipment.slots`（null 表示已卸下的槽位），冷却继续由 AbilitySystem 保存。恢复时先应用所有权、装备和外观，再恢复冷却；待切换请求及进行中的动作不保存。

旧档缺少装备数据或武器 ID 无效时默认大剑。schema 1–3 的 `attack` 冷却迁移为 `greatsword_attack`，保留既有坐标迁移行为；迁移不修改传入的原存档对象。

## 验证

运行 Spacelevel，使用 Q 检查切换、左键检查两把武器的攻击与独立冷却；攻击期间按 Q 检查延迟切换和取消队列。按 B 检查装备、卸下及当前武器外观，确认脚底对齐、攻击时长与存档恢复。

太刀源图及图集位于 [amber_fox_katana](../../../assets/textures/sprites/amber_fox_katana/)，生成提示词与锚点在同名 [资源目录](../../../assets/resources/sprites/amber_fox_katana/)。重建命令见 [角色美术文档](../../../docs/art/character_sprites.md)。
