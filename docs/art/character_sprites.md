# 角色美术资源

当前玩家使用黄棕色狐耳少女，初始持双手大剑，可切换太刀。保留已确定的五官、金色侧马尾、完整旅行装、大色块画法和单条尾巴，不使用全局减色或静态脸覆盖。

## 当前运行资源

| 内容 | 资源 |
| --- | --- |
| 大剑动作 | [greatsword_v4 SpriteFrames](../../assets/resources/sprites/amber_fox_style_v2/greatsword_v4/sprite_frames.tres) |
| 太刀动作 | [katana_v3 SpriteFrames](../../assets/resources/sprites/amber_fox_style_v2/katana_v3/sprite_frames.tres) |
| 武器配置 | [weapons](../../assets/resources/weapons/) |
| 行囊肖像 | `assets/textures/sprites/amber_fox_style_v2/full_256/gs/portrait.png`、`kt/portrait.png` |

两套动作都由各自的 `all-actions-atlas.png` 提供，覆盖 idle、run、attack、jumpup、inair、isfalling、land、jump、dash、hurt、death。图集无损导入、无 mipmap、二值 alpha，最近邻显示。已被替代的动作版本、月汐法师、重复逐帧 PNG、预览与源视频解码中间帧已移除；`full_256` 只保留仍用于行囊的两张肖像。

大剑为 256×256，174px 站立参考，脚底 (128,219)，PixelSize 0.004137931034，VisualScale 1.3。待机 24 帧 / 8 fps，跑步 18 帧 / 12 fps，攻击 30 帧 / 25 fps = 1.20 秒。

太刀为 256×256，179px 站立参考，脚底 (128,224)，PixelSize 0.00402234637，VisualScale 1.3。攻击 18 帧 / 30 fps = 0.60 秒，冷却 0.66 秒。两套动作均对齐约 0.936 的世界高度和碰撞下缘。

## 源素材与重建

当前两套动作保留各自 `sources/` 中的关键姿势原稿、[大剑源视频](../../assets/videos/characters/amber_fox_style_v2/greatsword_v4/)、[太刀源视频](../../assets/videos/characters/amber_fox_style_v2/katana_v3/)，以及资源目录中的提示词、工作流、种子、选帧与动画映射。原视频整体最近邻缩为 256×256，不逐帧裁紧、缩放、拉伸或减色；绿幕修复限制在轮廓外侧 3px 的混色区域。

使用 `scripts/tools/art/export_greatsword_v4.py` 或 `export_katana_v3.py` 可从保留的源视频与 `selection.json` 重建帧、图集、SpriteFrames 和映射。逐帧文件与审核图属于可重建输出，当前不随项目保留；重新导出后应检查脸部、剑身、透明边缘、左右朝向和脚底对齐。

## 独立刀光与运行检查

两套武器通过 Ability Timeline 的 Blade Trail 轨播放独立弧形刀光。大剑为 0.34–0.42 秒，太刀为 0.13–0.30 秒；刀光跟随左右朝向，结束或取消时隐藏，不投影。

构建后从主场景进入林间镇，检查两套武器的所有动作、切换、攻击时长、刀光和脚底对齐。NPC 规格见 [灰绿居民](npc_resident.md) 与 [跑腿员](npc_courier.md)。
