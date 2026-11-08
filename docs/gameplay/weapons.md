# 武器与行囊系统

主角当前仅使用大剑，初始物品和装备均只有大剑，第二个装备槽为空。左键触发大剑普攻，玩家不再绑定武器切换操作。人物设定和动作预览统一见[主角人物档案](../characters/protagonist.md)。

上、中、下段如何结合 Weapon 与 Skill/Ability，见[大剑架势接入方案](combat-stances.md)。当前已接入三段待机与六向过渡，默认中段；Shift + 滚轮上／左键／滚轮下直选上／中／下段。普通左键普攻，手柄采用 R1/RB 配合上、左、下三个面键直选。换势无额外冷却，开始时播放 0.32 秒周身细小金色粒子。

| 武器 | 能力 ID | 动作时长 | 冷却 |
| --- | --- | --- | --- |
| 大剑 | `greatsword_attack` | 1.20 秒 | 1.35 秒 |

大剑攻击阻止移动和跳跃；独立弧形刀光由能力时间轴的 `Blade Trail` 轨控制，范围为 0.34–0.42 秒，随角色左右朝向翻转，动作结束或取消时隐藏。当前伤害、命中判定与连招尚未接入。

## 资源与组件

- [大剑定义](../../assets/resources/weapons/greatsword.tres)引用攻击能力和动画配置。`WeaponDefinition` 为共享只读配置；`WeaponAnimationProfile` 保存 SpriteFrames、画布、脚底锚点和像素比例。
- `StanceMoveAnimations` 将各段映射为 `run_high/mid/low`；配置非空时必须覆盖全部架势且引用循环动画，留空则继续使用基础 `run`。`ActionFrames` 保存独立动作集，与基础、待机和过渡资源按装备实例合成。切换时同时更新 `idle` 和 `run` 的别名，再刷新一次播放。
- `WeaponEquipmentComponent3D : Component3D` 在动画组件初始化后、持久化注册前初始化。实例独立维护两个固定槽位、当前槽位、架势、换势动作和待切换请求；通用装备 API 保留，当前玩家只配置大剑。
- CharacterGraph 将 `player_attack` 连接到 `TryPrimaryAttack`，排除 `player_stance_mid`，避免 Shift + 左键误触普攻。键鼠 `player_stance_high/mid/low` 和手柄三组 modifier 节点均调用 `RequestStance`。`StanceSwitchEffectComponent3D` 监听实际换势开始，触发每个角色独立的周身粒子场景。玩家不绑定武器切换和冲刺。
- 基础 AbilitySet 为空，装备组件授予大剑普攻。通用切换 API 不重建能力实例或刷新冷却；攻击期间发起的有效装备切换会等待能力结束。
- 当前动画和显示规格以[主角人物档案](../characters/protagonist.md)及[大剑动画配置](../../assets/resources/weapons/greatsword_animation.tres)为准。

本轮先制作并接入双手大剑三段移动，沿用各段握持位置、短步幅与同一套移动物理；后续三段各一招普攻按新动作自身节奏确定时长和冷却，上表旧普攻参数不作为制作限制。

## 物品所有权与行囊

`InventoryComponent3D` 维护所有权、容量和格子位置，默认 12 格，以武器 ID 去重。`TryAddWeapon`、`Contains`、`Find` 提供物品接口；`GetCellIndex`、`GetWeaponAtCell`、`MoveWeaponToCell` 管理位置。背包窗口、右下角按钮与 B 键入口已移除，组件本身不创建界面或修改输入锁。

`EquipWeapon` / `UnequipSlot` / `RequestNextWeapon` 负责通用装备操作。装备和卸下不改变所有权，最后一把武器不能卸下。仅有一个已装备槽位时，`RequestNextWeapon` 返回 `false`。

`StandaloneModuleDriver` 在 F6 直接运行关卡时推进模块；正常入口已有 RootModule 时不重复更新。

## 存档恢复

角色分区格式第 5 版保存 `inventory.weapon_ids`、可选的 `inventory.positions`、`equipment.equipped_weapon_id` 、`equipment.slots` 和可选的 `equipment.stance`。装备使用 `loadout_version = 1`，`null` 表示空槽。先恢复所有权及格子位置，再恢复装备和外观，最后恢复能力冷却；不保存待切换请求或进行中的动作。

读取旧存档时，只恢复当前场景 `StartingWeapons` 和装备目录中可用的武器。旧档中的太刀会被忽略；若过滤后没有武器或有效装备，回退到当前初始大剑。不存在的格子位置按顺序分配，重复或越界位置放到空格。格式第 1–4 版继续兼容；旧 `attack` 冷却迁移为 `greatsword_attack`，已有坐标迁移保持不变，不修改传入的原始存档对象。

## 验证

从主场景进入森林关卡，确认只装备大剑，Q 不切换，左键大剑普攻与刀光正常；检查脚底对齐、攻击时长，以及旧存档中的太刀被忽略后能恢复大剑。完整存档格式见[存档系统](saves.md)。
