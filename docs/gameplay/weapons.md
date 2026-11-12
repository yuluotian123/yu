# 武器与行囊系统

主角当前仅使用大剑，初始物品和装备均只有大剑，第二个装备槽为空。左键触发大剑普攻，玩家不再绑定武器切换操作。人物设定和动作预览统一见[主角人物档案](../characters/protagonist.md)。

上、中、下段如何结合 Weapon 与 Skill/Ability，见[大剑架势接入方案](combat-stances.md)。三段各有待机、移动和单次普攻，默认中段；Shift + 滚轮上／左键／滚轮下直选上／中／下段。普通左键使用当前架势的完整普攻，攻击中连点或按住不会续招；手柄换势入口暂时移除。换势无额外冷却，开始时播放 0.32 秒周身细小金色粒子。

| 架势 | 能力 ID | 技能图 |
| --- | --- | --- |
| 上段 | `greatsword_attack_high` | [上段重劈](../../assets/resources/weapons/greatsword_attack_high_timeline.tres) |
| 中段 | `greatsword_attack` | [中段横扫](../../assets/resources/weapons/greatsword_attack_timeline.tres) |
| 下段 | `greatsword_attack_low` | [下段前刺](../../assets/resources/weapons/greatsword_attack_low_timeline.tres) |

左键启动一次完整普攻；攻击中连点或按住不续招，完整收势后再次点击才启动新攻击。每个架势的技能图只有输入、Timeline 和结束三个节点；通用连招窗口及条件节点继续保留。上段向前重劈、中段横扫、下段低位前刺。动作、蓄满和落点停留的实际时间统一记录在[制作配置](../../assets/resources/sprites/amber_fox_style_v2/greatsword_stance_attack/production.json)和技能 Timeline 中。当前伤害与命中判定尚未接入。

v12 重做中段，减少腰胯反向扭转，横切后先在身前提剑、再屈肘回架；上、下段沿用 v11。继续使用提速后的节奏：蓄势 377–426 毫秒、挥出 80–96 毫秒、落点短停 48–54 毫秒、连续收势 406–484 毫秒。蓄满停留由 130–140 毫秒缩短至 65–70 毫秒，减少停住等待。上段压身重劈后承重提剑，中段通过腰肩带动刃口横切后贴身回架，下段低位平刺后沿原路回收；101 张姿势帧采用独立定时。冷却与动作锁同步覆盖全程。

## 资源与组件

- [大剑定义](../../assets/resources/weapons/greatsword.tres)引用攻击能力和动画配置。`WeaponDefinition` 为共享只读配置；`WeaponAnimationProfile` 保存 SpriteFrames、画布、脚底锚点和像素比例。
- `StanceMoveAnimations` 将各段映射为 `run_high/mid/low`；配置非空时必须覆盖全部架势且引用循环动画，留空则继续使用基础 `run`。`ActionFrames` 保存独立动作集，与基础、待机和过渡资源按装备实例合成。切换时同时更新 `idle` 和 `run` 的别名，再刷新一次播放。
- `WeaponEquipmentComponent3D : Component3D` 在动画组件初始化后、持久化注册前初始化。实例独立维护两个固定槽位、当前槽位、架势、换势动作和待切换请求；通用装备 API 保留，当前玩家只配置大剑。
- CharacterGraph 用一个注册节点引用武器配置，展开三套普攻；移动、跳跃和架势切换保留原来的输入与组件调用节点，架势切换不注册为能力。每个攻击技能图的 `AbilityInputNodeData` 监听 `player_attack`，排除 `player_stance_mid`，并用 `AbilityCurrentWeaponAttackCondition` 选择当前武器、当前已完成架势的技能。玩家不绑定武器切换和冲刺。
- `StancePrimaryAttacks` 为各段配置独立 AbilityResource，初始化及装备时一次授予；`CurrentPrimaryAttack` 按当前已完成的架势选择技能。未配置该映射的武器回退到 `PrimaryAttack`，映射非空时须覆盖全部架势。中段保留旧 ID `greatsword_attack` 兼容存档。
- 当前玩家通过一个 `CharacterRegisterAbilityNodeData` 的 `SourcePath` 引用大剑定义，统一注册其普攻；装备初始化的授予操作幂等，继续支持非玩家入口。`AttackFrames` 提供三个攻击动画并按装备实例合成；切换架势不重建能力实例或刷新冷却，攻击期间的有效换势等待收招。
- 每套生产 Timeline 编排一段完整动画，结束后进入唯一 Return，不打开续招窗口。通用输入窗口和消费条件继续保留；以后配置多出口连招时，每个结束分支须使用独立 Return。
- 当前动画和显示规格以[主角人物档案](../characters/protagonist.md)及[大剑动画配置](../../assets/resources/weapons/greatsword_animation.tres)为准。

三段移动沿用短步幅与同一套移动物理；新攻击按准备、出手和回架独立定时。动作依据和来源见[双手大剑攻击参考](../art/greatsword-attack-references.md)。

### 表现

武器攻击特效已移除。三套普攻 Timeline 仅保留完整动画；刀光 Action、轨迹资源、专用粒子场景和运行时播放器均已删除，制作工具也不再生成攻击特效。

换势仍由武器组件实际开始过渡时播放 [StanceEffectGraph](../../assets/resources/weapons/greatsword_stance_effect.tres)，其中 `AbilityParticleSceneAction` Timeline 片段控制周身粒子。这张图没有输入节点和 Ability 注册，不参与普攻的运行状态或冷却。

## 物品所有权与行囊

`InventoryComponent3D` 维护所有权、容量和格子位置，默认 12 格，以武器 ID 去重。`TryAddWeapon`、`Contains`、`Find` 提供物品接口；`GetCellIndex`、`GetWeaponAtCell`、`MoveWeaponToCell` 管理位置。背包窗口、右下角按钮与 B 键入口已移除，组件本身不创建界面或修改输入锁。

`EquipWeapon` / `UnequipSlot` / `RequestNextWeapon` 负责通用装备操作。装备和卸下不改变所有权，最后一把武器不能卸下。仅有一个已装备槽位时，`RequestNextWeapon` 返回 `false`。

`StandaloneModuleDriver` 在 F6 直接运行关卡时推进模块；正常入口已有 RootModule 时不重复更新。

## 存档恢复

角色分区格式第 5 版保存 `inventory.weapon_ids`、可选的 `inventory.positions`、`equipment.equipped_weapon_id` 、`equipment.slots` 和可选的 `equipment.stance`。装备使用 `loadout_version = 1`，`null` 表示空槽。先恢复所有权及格子位置，再恢复装备和外观，最后恢复能力冷却；不保存待切换请求或进行中的动作。

读取旧存档时，只恢复当前场景 `StartingWeapons` 和装备目录中可用的武器。旧档中的太刀会被忽略；若过滤后没有武器或有效装备，回退到当前初始大剑。不存在的格子位置按顺序分配，重复或越界位置放到空格。格式第 1–4 版继续兼容；旧 `attack` 冷却迁移为 `greatsword_attack`，已有坐标迁移保持不变，不修改传入的原始存档对象。

## 验证

运行 `weapon_stances.tscn` 检查三架势真实左键单击、连点/按住不续招、完整收势及下一次点击；另用内存图验证保留的连招框架，包括 1/2/3 段分支、每窗口一次缓存与取消清理。也覆盖技能图端口、注册范围、移动锁释放、换势排队及角色实例隔离。`-- --attack-capture` 单独录制三架势普攻预览。完整存档格式见[存档系统](saves.md)。
