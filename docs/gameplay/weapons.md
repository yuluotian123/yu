# 武器与行囊系统

主角当前仅使用大剑，初始物品和装备均只有大剑，第二个装备槽为空。左键触发大剑普攻，玩家不再绑定武器切换操作。人物设定和动作预览统一见[主角人物档案](../characters/protagonist.md)。

上、中、下段如何结合 Weapon 与 Skill/Ability，见[大剑架势接入方案](combat-stances.md)。三段各有待机、移动和一招普攻，默认中段；Shift + 滚轮上／左键／滚轮下直选上／中／下段。普通左键使用当前架势的普攻，手柄采用 R1/RB 配合上、左、下三个面键直选。换势无额外冷却，开始时播放 0.32 秒周身细小金色粒子。

| 武器 | 能力 ID | 动作时长 | 冷却 |
| --- | --- | --- | --- |
| 上段下劈 | `greatsword_attack_high` | 0.680 秒 | 0.680 秒 |
| 中段横扫 | `greatsword_attack` | 0.540 秒 | 0.540 秒 |
| 下段前刺 | `greatsword_attack_low` | 0.474 秒 | 0.474 秒 |

大剑攻击从各自架势起刀并收回，放大蓄力、伸臂、前倾与落刀幅度，出手集中在角色朝向的前方。有效出手为 68 / 48 / 54 毫秒，蓄力为 340 / 270 / 220 毫秒，含 130 / 90 / 90 毫秒蓄满顿帧；落点再顿 80 / 62 / 46 毫秒，以慢蓄、快出和短停区分重量；中段在腰胸高度横扫，下段剑身基本平行地面前刺。动作期间阻止移动和跳跃；冷却覆盖完整动作，收招后可再次攻击。刀光由独立粒子组件跟随剑刃，换势粒子仍由外置场景控制。当前伤害、命中判定与连招尚未接入。

## 资源与组件

- [大剑定义](../../assets/resources/weapons/greatsword.tres)引用攻击能力和动画配置。`WeaponDefinition` 为共享只读配置；`WeaponAnimationProfile` 保存 SpriteFrames、画布、脚底锚点和像素比例。
- `StanceMoveAnimations` 将各段映射为 `run_high/mid/low`；配置非空时必须覆盖全部架势且引用循环动画，留空则继续使用基础 `run`。`ActionFrames` 保存独立动作集，与基础、待机和过渡资源按装备实例合成。切换时同时更新 `idle` 和 `run` 的别名，再刷新一次播放。
- `WeaponEquipmentComponent3D : Component3D` 在动画组件初始化后、持久化注册前初始化。实例独立维护两个固定槽位、当前槽位、架势、换势动作和待切换请求；通用装备 API 保留，当前玩家只配置大剑。
- CharacterGraph 将 `player_attack` 连接到 `TryPrimaryAttack`，排除 `player_stance_mid`，避免 Shift + 左键误触普攻。键鼠 `player_stance_high/mid/low` 和手柄三组 modifier 节点均调用 `RequestStance`。`StanceSwitchEffectComponent3D` 监听实际换势开始，触发每个角色独立的周身粒子场景。玩家不绑定武器切换和冲刺。
- `StancePrimaryAttacks` 为各段配置独立 AbilityResource，初始化及装备时一次授予；`CurrentPrimaryAttack` 按当前已完成的架势选择技能。未配置该映射的武器回退到 `PrimaryAttack`，映射非空时须覆盖全部架势。中段保留旧 ID `greatsword_attack` 兼容存档。
- 基础 AbilitySet 为空，装备组件授予三招大剑普攻。`AttackFrames` 提供独立攻击帧集并按装备实例合成；切换架势不重建能力实例或刷新冷却，攻击期间的有效换势等待收招。
- 当前动画和显示规格以[主角人物档案](../characters/protagonist.md)及[大剑动画配置](../../assets/resources/weapons/greatsword_animation.tres)为准。

三段移动沿用短步幅与同一套移动物理；新攻击按准备、出手和回架独立定时。动作依据和来源见[双手大剑攻击参考](../art/greatsword-attack-references.md)。

### 攻击刀光

`WeaponAnimationProfile.AttackTrails` 为每招指定 `WeaponAttackTrail`：能力 ID、动画名，以及原生画布坐标中的剑刃根部／剑尖关键帧。关键帧时间由 SpriteFrames 的实际逐帧时长换算，变速播放时仍与挥刀一致。动画组件使原生精灵先于宿主更新，刀光随后读取当帧姿势，避免高速挥刀时落后一帧；移除动画组件时恢复原处理优先级。只标定向前挥出的部分，蓄力和回架不发射刀光。

[WeaponAttackEffectComponent3D](../../scripts/gamelogic/weapons/WeaponAttackEffectComponent3D.cs) 管理独立的 [CPUParticles3D 场景](../../assets/scenes/vfx/greatsword_attack_particles.tscn)。剑刃上密集短寿命亮片形成刀光，末端少量金色碎屑继续飞散；粒子在出手位置留痕，120 Hz 沿实际剑路补齐发射位置，亮片 65 毫秒、碎屑 170 毫秒淡出，碎屑速度方向跟随剑尖运动；左右朝向只镜像一次。场景使用现有 `relink_detail_particles` 深度匹配，保留像素后处理中的细小亮芯并接受场景遮挡。取消、完成、换武器及销毁都会清理特效；角色贴图和材质无需增加光效。

## 物品所有权与行囊

`InventoryComponent3D` 维护所有权、容量和格子位置，默认 12 格，以武器 ID 去重。`TryAddWeapon`、`Contains`、`Find` 提供物品接口；`GetCellIndex`、`GetWeaponAtCell`、`MoveWeaponToCell` 管理位置。背包窗口、右下角按钮与 B 键入口已移除，组件本身不创建界面或修改输入锁。

`EquipWeapon` / `UnequipSlot` / `RequestNextWeapon` 负责通用装备操作。装备和卸下不改变所有权，最后一把武器不能卸下。仅有一个已装备槽位时，`RequestNextWeapon` 返回 `false`。

`StandaloneModuleDriver` 在 F6 直接运行关卡时推进模块；正常入口已有 RootModule 时不重复更新。

## 存档恢复

角色分区格式第 5 版保存 `inventory.weapon_ids`、可选的 `inventory.positions`、`equipment.equipped_weapon_id` 、`equipment.slots` 和可选的 `equipment.stance`。装备使用 `loadout_version = 1`，`null` 表示空槽。先恢复所有权及格子位置，再恢复装备和外观，最后恢复能力冷却；不保存待切换请求或进行中的动作。

读取旧存档时，只恢复当前场景 `StartingWeapons` 和装备目录中可用的武器。旧档中的太刀会被忽略；若过滤后没有武器或有效装备，回退到当前初始大剑。不存在的格子位置按顺序分配，重复或越界位置放到空格。格式第 1–4 版继续兼容；旧 `attack` 冷却迁移为 `greatsword_attack`，已有坐标迁移保持不变，不修改传入的原始存档对象。

## 验证

运行 `weapon_stances.tscn` 检查三段左键选招、收招回待机、移动锁释放、重复点击、换势排队及角色实例隔离；`-- --attack-capture` 单独录制三招预览。完整存档格式见[存档系统](saves.md)。
