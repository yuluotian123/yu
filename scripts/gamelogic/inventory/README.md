# 武器行囊

`InventoryComponent3D` 保存物品所有权、容量和格子位置。背包窗口、右下角按钮与 B 键入口已移除；组件不创建界面或修改玩家输入锁。

初始拥有大剑、太刀，两把均已装备，默认持有大剑。行囊默认 12 格，以武器 ID 去重。`TryAddWeapon`、`Contains`、`Find` 提供物品接口；`GetCellIndex`、`GetWeaponAtCell`、`MoveWeaponToCell` 管理格子位置。

装备由 `WeaponEquipmentComponent3D` 管理两个固定槽位：`EquipWeapon` / `UnequipSlot` / `RequestNextWeapon`。装备或卸下不增减物品所有权；最后一把武器禁止卸下。Q 只遍历已装备的槽位，攻击或冲刺期间切换会延迟到收招结束，能力实例与冷却保持不变。

`StandaloneModuleDriver` 在 F6 直接运行关卡时推进已有模块；正常入口有 RootModule 时不重复更新。

角色存档 schema 5 保存 `inventory.weapon_ids` 和可选的 `inventory.positions`（武器 ID 到行囊格编号），装备侧使用 `loadout_version = 1` 和允许 null 的 `slots` 保留空槽。先恢复所有权与格子位置，再恢复装备，最后恢复能力冷却。旧档没有 positions 时按已有物品顺序排列；重复、越界的位置自动放到空格。schema 1–4 继续兼容；无效的全空装备恢复一把已有武器，保持至少持有一把的规则。

运行 ForestTownLevel，确认没有背包入口、B 不打开界面，Q 切换武器和普攻正常。存档格式与恢复顺序见[存档系统](../saves/README.md)。
