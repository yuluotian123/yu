# 角色美术资源

当前 Player 使用黄棕色狐耳少女，初始持双手大剑。保留少女面容、完整旅行装和低像素风格。大剑剑首、握柄、护手中心和剑身沿同一中轴，待机竖直持握。

现支持按 Q 在大剑与太刀之间切换，两套动作均保持约 96px 人物高度、224px 透明帧画布及统一脚底锚点。太刀使用细长弧刃，拥有独立的 40 张姿势和 0.36 秒攻击；大剑攻击为 0.6 秒。切换逻辑与配置见 [武器系统](../../scripts/gamelogic/weapons/README.md)。

两套武器的跑动、攻击和跳跃源图以现有 idle 的细描边、较纤细的头身比例为修整基准；idle、受伤和倒地帧保持原有像素。跑动八帧均保留双手握同一根剑柄的结构，去掉前腰容易被看成第三只手的额外轮廓和腰包。太刀跑动沿用修正后的大剑双臂姿势，避免独立出图再次增加手臂。修整使用内置 imagegen，完整提示词分别记录在 [大剑修整记录](../../assets/resources/sprites/amber_fox_greatsword/idle_style_revision.json) 和 [太刀修整记录](../../assets/resources/sprites/amber_fox_katana/idle_style_revision.json)。

太刀原稿 / 图集：[amber_fox_katana](../../assets/textures/sprites/amber_fox_katana/)；[动画、锚点及内置 imagegen 提示词](../../assets/resources/sprites/amber_fox_katana/)。从源图重建：

```powershell
godot --headless --path . --script res://scripts/tools/build_tide_mage_sprites.gd -- --character=amber_fox_katana --height=96 --frame-size=224 --warm-head --attack-duration=0.36
```

- [角色设计参考](../../assets/textures/concepts/protagonist/amber_fox_greatsword/character-design.png)；[原生待机 PNG](../../assets/textures/sprites/amber_fox_greatsword/idle-native.png) 实际为 57×96。
- [动作源图及图集](../../assets/textures/sprites/amber_fox_greatsword/)；[SpriteFrames、锚点及生成提示词](../../assets/resources/sprites/amber_fox_greatsword/)。
- 40 张姿势：待机 4、跑步 8、大剑攻击 8、跳跃及落地 12、受伤 4、倒地 4；沿用原有 11 个动画状态。
- 人物站立约 96 像素高。224×224 帧画布用于大剑挥动的透明边距，不提高人物像素密度；运行图集为 1792×1120。
- 无损导入、无 mipmap、最近邻采样、二值 alpha。`pixel_size=0.0075`、缩放 1.3，脚底锚点 (112,192)，Y 位移 0.312，使脚底高度 `0.312-(192-112)*0.0075*1.3=-0.468` 与原碰撞下缘一致。
- 初始外观和攻击动作均持大剑；沿用现有攻击、冲刺能力及控制逻辑，攻击 8 帧总长仍为 0.6 秒。

从四张源图重建：

```powershell
godot --headless --path . --script res://scripts/tools/build_tide_mage_sprites.gd -- --character=amber_fox_greatsword --height=96 --frame-size=224 --warm-head
```

重建脚本无参数时保留原月汐对齐方式；`--warm-head` 按狐耳少女暖色头部排除上举钢剑的高度，统一人物像素密度。临时原生放大图及图集预览在 `.godot/art-validation/amber_fox_greatsword/`。

## 大色块人设与画面参考（概念稿）

新增 [golden_fox_color_blocks_v1 人设表](../../assets/textures/concepts/protagonist/golden_fox_color_blocks_v1/character-design.png) 和 [河畔小镇画面参考](../../assets/textures/concepts/protagonist/golden_fox_color_blocks_v1/scene-reference.png)。保留当前主角的金黄、焦糖棕、奶油白配色、狐耳单尾与初始大剑，表情采用自然的闭口微笑。

画法采用大块连续像素色面，轮廓直接以物体固有色接触背景；用相邻色块和局部大阴影区分形体，避免连续黑色或深棕描边。头发、衣褶、五官和装饰简化，每种材质以二至三档明暗为主。正背面统一金黄短披肩、米白长袖、棕色背心与不透明棕色裤袜。

这套图片用于后续美术设计，尚未替换运行时动作或 Player 配置；场景图为氛围概念参考。用户提供的 [画风参考](../../assets/textures/concepts/protagonist/golden_fox_color_blocks_v1/style-reference.png) 与内置 imagegen 的 [完整生成提示词](../../assets/resources/art/golden_fox_color_blocks_v1/generation_prompts.json) 已保存。

## 保留的月汐资源

前版 Player 使用「月汐·巡潮者」低像素龙裔法师。角色设计参考位于 [tide_mage](../../assets/textures/concepts/protagonist/tide_mage/character-design.png)。

- [贴图目录](../../assets/textures/sprites/tide_mage/) 保留内置 imagegen 生成的透明动作原稿 `sources/` 和运行图集 `atlas.png`。
- [资源目录](../../assets/resources/sprites/tide_mage/) 包含 `sprite_frames.tres`、逐帧锚点 `alignment.json` 和完整生成提示词 `generation_prompts.json`。
- 共 40 张不同姿势：待机 4、跑步 8、攻击 8、跳跃及落地 12、受伤 4、倒地 4；沿用现有 11 个动画名及 EC 动画状态图。
- 图集为 1536×960，原生帧 192×192，站立角色约 112 像素高，脚底锚点 (96,160)。纹理无损导入、关闭 mipmap，AnimatedSprite3D 使用最近邻采样、硬边 alpha 裁切和 `pixel_size=0.0075`。
- 子节点缩放 1.3、Y 位移 0.156；脚底对应碰撞体下缘 -0.468，碰撞尺寸继续使用 (0.468,0.936,0.468)。攻击 8 帧总长 0.6 秒，与原攻击时间轴同步。跳跃空中阶段对齐头部，避免收腿额外抬高角色。

从保留原稿重建低像素图集：

```powershell
godot --headless --path . --script res://scripts/tools/build_tide_mage_sprites.gd
```

脚本使用最近邻缩小和二值透明度建立原生像素网格；逐帧裁切排除相邻姿势，所有帧保留透明边距。预览输出到忽略的 `.godot/art-validation/tide-mage/`。
