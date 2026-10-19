# 角色美术资源

当前 Player 使用黄棕色狐耳少女，初始持双手大剑。保留少女面容、完整旅行装和低像素风格。大剑剑首、握柄、护手中心和剑身沿同一中轴，待机竖直持握。

当前运行资源为 [full_256](../../assets/resources/sprites/amber_fox_style_v2/full_256/)，已接入 `player.tscn`、两套武器动画配置和行囊角色肖像。**大剑 idle 原样复用用户认可的 256×256 原版24帧**，站立约 224px、脚底 (128,240)，PNG 字节与原版一致，不再缩小或重新生成。曾重新生成的 179px idle 因五官、比例和色块退化而弃用。大剑跑步和新版攻击以原版 idle 为基准，采用同样的原生像素密度；其他动作保持原有整套视频资源。各动画通过显示比例对齐同一世界参考高度 0.936 和碰撞下缘 -0.468。保留物体固有色与五官，无连续黑色/深棕外描边。

[恢复后的原版 idle 预览](../../assets/textures/sprites/amber_fox_style_v2/full_256/gs/previews/idle-preview-restored-v2.png) 与 [复用校验](../../assets/resources/sprites/amber_fox_style_v2/full_256/gs/approved-idle-reuse.json) 为当前大剑待机基准。已认可的原画和动画优先复用；“统一流程”不意味着重画已有成品。新动作先与这版 idle 对照五官、比例和大色块，再与对应视频核对；像素忠实于新视频不能代替角色风格检查。

两套均覆盖 `idle`、`run`、`attack`、`jumpup`、`inair`、`isfalling`、`land`、`jump`、`dash`、`hurt`、`death`。共 237 张独立源帧、247 个动画帧引用；大剑 126 张，太刀 111 张。大剑待机 24 帧 / 8 fps、跑步 20 帧 / 12 fps；太刀跑步 24 帧 / 12 fps。大剑攻击 30 帧 / 25 fps = 1.20s，太刀攻击 9 帧 / 25 fps = 0.36s。冲刺原始动画 0.275s，以现有 1.25 倍速对应能力的 0.22s；受击 0.4s、倒地约 0.667s。起跳和落地各 0.2s，空中和下落不循环并保持最后一帧。`jump` 保留为兼容的完整跳跃动作。切换逻辑与配置见 [武器系统](../../scripts/gamelogic/weapons/README.md)。

原整套源视频为 512×512 / 24 fps / 73 帧；新版大剑攻击为三个 39 帧连接片段，仅最近邻缩为 256×256，不逐帧裁紧、拉伸、减色或覆盖静态脸。新增动作采用绿幕键控，外侧 3px 范围内用同帧附近材质像素修复绿边；原版大剑 idle 沿用已认可的灰底视频与透明结果，不重跑抠像。脸部与皮肤受保护，细太刀使用经逐帧对照修正的阈值，避免近灰色剑身被删除；运动装备不套静态遮罩。新版大剑攻击采用双手向下重斩并回到 idle，弃用力度不足且剑身缩短的前刺；倒地视频重生成为连续跪下再侧卧。太刀攻击跳过了剑尖出框的源帧，实际帧号均在 `animation-map.json` 中。

[大剑预览](../../assets/textures/sprites/amber_fox_style_v2/full_256/gs/all-actions-attack-front-v3.png)、[太刀预览](../../assets/textures/sprites/amber_fox_style_v2/full_256/kt/all-actions-preview.png)、[透明帧和图集](../../assets/textures/sprites/amber_fox_style_v2/full_256/)、[原始视频](../../assets/videos/characters/amber_fox_style_v2/full_256/) 与 [工作流、种子、提示词及校验报告](../../assets/resources/sprites/amber_fox_style_v2/full_256/) 均已保存。`sources/reviews/` 保留逐帧全身对照、随头部移动的五官对照和深浅底图。恢复的 idle 以单独保留的已认可 PNG 为完整 RGBA 复用参照，其余动作没有独立全剪影参照，继续如实标记 `needs_visual_review`；另有逐帧目视记录。受保护脸部和内缩身体像素与视频一致，无全局减色。

运行图集无损导入、无 mipmap、二值 alpha，最近邻显示。大剑 idle、run 和新版 attack 的 `PixelSize=0.003214285714`、脚底 (128,240)、Y=0；其余状态默认 `PixelSize=0.00402234637`、脚底 (128,224)、Y=0.033988827，缩放均为 1.3。`WeaponAnimationProfile.AnimationPixelSizes` / `AnimationFootAnchors` 可按动画指定原生密度，装备组件在动画变化和切换武器时同步显示参数，避免为了统一画布损失已认可的原生像素。实际导入逐帧核对 RGB、全部 alpha、尺寸与播放设置；数值见 [引擎校验](../../assets/resources/sprites/amber_fox_style_v2/full_256/engine-validation.json)。真实 Player 的运行检查覆盖全部状态、反复 idle/run/idle 的参考密度和脚底、两套武器切换及攻击时长；构建后可运行 `godot --headless --path . res://assets/scenes/tests/character_visual_smoke.tscn`。

## 大剑攻击 v3：延长并完整劈到身前

[当前攻击预览](../../assets/textures/sprites/amber_fox_style_v2/attack_front_v3/previews/idle-front-cleave-return-v3.png) 为 30 帧、25 fps、1.20 秒、不循环。先蓄力，再快速向右前方下劈，完整伸剑到身前并短停后收回 idle。前方落剑时剑尖约 x241，原版 idle 约 x155，已超过身体和前腿位置；没有在竖剑待机位置提前结束斩击。完整剑长仍有模型和栅格误差，目视逐帧核对并保存了关键姿势的近似端点测量，不宣称物理长度完全固定。

新增前方落剑关键稿使用内置 imagegen，以原版 idle 锁定身份、以紧凑收势稿约束身体；由本地 `video_minimax_h3_i2v` 生成斩击和回收连接。沿用已核验的蓄力视频，并明确把其抬剑源帧 A22 放在蓄力后作为快速下劈的过渡，这是按关键姿势重新编排，原视频及逐帧映射完整保留。过滤了转向时剑身弯曲、缩短的生成候选，没有逐帧缩放或盖脸。见 [生成参数、提示词与工作流](../../assets/resources/sprites/amber_fox_style_v2/attack_front_v3/generation.json)。

首尾完整 PNG 复用原版 idle，24 帧 idle 和现有 run 的文件字节保持原样；28 个视频内部帧与对应 RGB 参考逐帧对照脸、身体、手、剑、腿脚和尾巴，审核数值错误为 0。深浅底和棋盘检查无新增连续黑边。没有独立全身遮罩，自动审核继续标为 `needs_visual_review`，目视记录和端点哈希单独保存。动画、能力时间轴与动画 clip 均为 1.20 秒，冷却改为 1.35 秒，保留原先结束后的约 0.15 秒间隔；155 项真实 Player 检查包括 0.8 秒时仍在攻击、结束后再执行排队换武器。

## 以原版 idle 为基准的活泼跑步 v4

已查询 [双手持剑奔跑姿势](https://posemy.art/sword-fight-poses/female-running-sword-pose/3405/) 和 [大剑跑步循环](https://charios.com/animations/great-sword-run)，仅参考重心、步态和握持。v3 虽采用相同显示密度，耳尖却比 idle 低 28–41px，躯干和腿的姿态过度压低，用户反馈奔跑时明显变矮。v4 以原版 idle 为身份和画风基准重生成 [跑步静帧](../../assets/textures/sprites/amber_fox_style_v2/run_height_v4/run-keyframe-256.png)，抬高髋部、舒展躯干与腿部，采用较直立的活泼跑姿；再调用本地 `video_minimax_h3_i2v`。保留腾空、抬膝及马尾/披肩/单条尾巴的跟随摆动，两手连握同一剑柄。没有使用游戏内拉伸或缩放补偿。

[新版跑步预览](../../assets/textures/sprites/amber_fox_style_v2/run_height_v4/previews/run-preview-v4.png)、[idle/run 同像素尺寸对照](../../assets/textures/sprites/amber_fox_style_v2/run_height_v4/previews/idle-run-comparison-v4.png)、[透明 PNG 与图集](../../assets/textures/sprites/amber_fox_style_v2/run_height_v4/)、[原视频](../../assets/videos/characters/amber_fox_style_v2/run_height_v4/minimax-source.mp4) 和 [完整生成参数](../../assets/resources/sprites/amber_fox_style_v2/run_height_v4/generation.json) 已保存。20 帧、12 fps、约 1.667 秒循环；源帧 24、26 至 62，四个完整步态周期。全部 73 个源帧耳尖 y=20–33，idle 为 y=16，下移缩至 4–17px；[高度记录](../../assets/resources/sprites/amber_fox_style_v2/run_height_v4/height-validation.json) 保留前后对照。测试新增逐帧耳尖高度及平均值检查，避免相同 PixelSize 掩盖姿态压低；该指标不等同独立骨骼测量，比例仍需目视核对。保留全画布，仅统一最近邻缩小，不逐帧裁紧、平移、拉伸、减色或覆盖五官。

全部 20 帧与视频逐区域对照，并检查深浅底、棋盘底及循环接缝。脸部和身体内部 RGB 完全保持源视频；仅在外侧 3px 内局部修复绿幕混色。完整剪影缺少独立真值遮罩，自动审核保持 `needs_visual_review`，由逐帧目视核对补充；[记录和覆盖限制](../../assets/resources/sprites/amber_fox_style_v2/run_height_v4/visual-review.json) 如实保留。原版 idle 和其他动作的 PNG 字节不变，旧跑步及其可复查素材保留为历史版本。

直接并排比较 idle 与支撑、腾空、迈步帧后，v4 的角色一致性仍标记为 **needs_revision**：脸型与肩部更宽、眼形更深且金色虹膜不够明确，尾巴中段更圆、白尾尖比例随帧变化，钢刃配色和小色块组织也有差异。高度、导入像素和运行检查通过不代表造型一致。见 [一致性对照](../../assets/textures/sprites/amber_fox_style_v2/run_height_v4/sources/idle-run-consistency-v4.png)、[头脸对照](../../assets/textures/sprites/amber_fox_style_v2/run_height_v4/sources/idle-run-head-consistency-v4.png) 和 [评审记录](../../assets/resources/sprites/amber_fox_style_v2/run_height_v4/character-consistency-review.json)。下一次生成应以原版 idle 锁定身份与比例，当前 run 只作动作参考，先修静帧再生成视频。

## 保留的 96px 旧版资源

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

## 大色块画风 idle（MiniMax H3）

以用户确认的 [idle 原稿](../../assets/textures/sprites/amber_fox_style_v2/sources/idle-static.png) 为设定，使用 ComfyUI `video_minimax_h3_i2v` 生成 [3 秒循环视频](../../assets/videos/characters/amber_fox_style_v2/idle.mp4)。首尾约束使用同一张图，固定镜头、脚底和直立大剑，保留金黄、奶白、棕色的大色块画法，动作以呼吸、眨眼、发梢和尾巴摆动为主。

另提供 [透明 idle 图集](../../assets/textures/sprites/amber_fox_style_v2/idle-atlas.png) 和 [SpriteFrames](../../assets/resources/sprites/amber_fox_style_v2/idle_sprite_frames.tres)：24 帧、8 fps、3 秒循环，人物约 96px 高，224×224 帧画布，脚底锚点 (112,192)。保持最近邻采样、二值 alpha 与无 mipmap。该资源仅包含 idle，尚未替换 Player 当前整套动作。

[可重跑工作流](../../assets/resources/sprites/amber_fox_style_v2/idle-minimax-workflow.json)、[参数与提示词](../../assets/resources/sprites/amber_fox_style_v2/idle-video-generation.json)、[逐帧对齐](../../assets/resources/sprites/amber_fox_style_v2/idle-video-alignment.json) 与 [原始生成视频](../../assets/videos/characters/amber_fox_style_v2/idle-minimax-source.mp4) 均已保留。Godot 已验证 SpriteFrames 可加载、24 帧纹理尺寸与循环设置正确。

## 256×256 大色块 idle（MiniMax H3）

按新提供的 idle 立绘与人设图，使用本机 ComfyUI `video_minimax_h3_i2v` 新生成 [待机视频](../../assets/videos/characters/amber_fox_style_v2/idle_256/idle.mp4) 与 [无描边 APNG 循环预览](../../assets/textures/sprites/amber_fox_style_v2/idle_256/idle-no-outline-preview.png)。另保留 [GIF 预览](../../assets/videos/characters/amber_fox_style_v2/idle_256/idle-preview.gif)。保留狐耳、单尾、黄棕奶白配色和双手竖直持大剑的姿势；动作包含呼吸、眨眼、发梢与较活泼的尾巴摆动。生成时首尾输入为同一关键帧，按用户选择采用第一版较大的尾摆。

[透明图集](../../assets/textures/sprites/amber_fox_style_v2/idle_256/idle-atlas.png)、[24 张独立 PNG](../../assets/textures/sprites/amber_fox_style_v2/idle_256/frames/) 与 [SpriteFrames](../../assets/resources/sprites/amber_fox_style_v2/idle_256/sprite_frames.tres) 均以 **256×256** 为帧尺寸，24 帧、8 fps、3 秒循环。原始模型视频为 512×512 / 24 fps；只采用最近邻缩小、灰底抠像与最外沿杂点清理，保持二值 alpha。面部、服装与身体内部保留原视频 RGB 像素，取消全图减色、平滑和固定帧覆盖，避免损失五官与小色块。近灰色剑身使用原关键帧的剑身轮廓遮罩，避免误抠成透明孔或留下压缩灰边。

[抠像工作流](../../assets/resources/sprites/amber_fox_style_v2/idle_256/matting-workflow.json) 与 [逐帧分区校验](../../assets/resources/sprites/amber_fox_style_v2/idle_256/matting-validation.json) 已保存。全部 24 帧均按脸、头发、上身、手、剑、腿与尾巴 7 个区域对照原视频；脸部及身体内部颜色差值为 0，并在深浅背景检查轮廓。无损 APNG 用于检查原像素，GIF 仅供兼容预览；MP4 使用无损 H.264 4:4:4 编码，避免二次色度抽样。

外轮廓采用邻近材质的固有色，去掉围绕金黄头发、奶白衣料、尾巴与手部的深色描边；耳尖、裤袜、靴子本身的棕色及五官正常保留。[轮廓修整工作流](../../assets/resources/sprites/amber_fox_style_v2/idle_256/outline-removal-workflow.json) 与 [校验记录](../../assets/resources/sprites/amber_fox_style_v2/idle_256/outline-validation.json) 覆盖全部 24 帧，确认脸部、身体内部和透明轮廓像素均未改动，修改集中在外侧约两像素的轮廓带。修整前输入保存在 `sources/idle-with-dark-contour.png`，原始 MiniMax 视频继续保留。

人物约 224px 高、脚底锚点 (128,240)。按当前游戏站立高度使用时，AnimatedSprite3D 设最近邻采样、`pixel_size=0.003214285714`、缩放 1.3、Y 位移 0，即与原角色的碰撞体下缘 -0.468 对齐。图集采用无损导入、关闭 mipmap。

[生成提示词与参数](../../assets/resources/sprites/amber_fox_style_v2/idle_256/generation.json)、[可重跑工作流](../../assets/resources/sprites/amber_fox_style_v2/idle_256/minimax-workflow.json)、[逐帧对齐及显示参数](../../assets/resources/sprites/amber_fox_style_v2/idle_256/alignment.json)、[原始生成视频](../../assets/videos/characters/amber_fox_style_v2/idle_256/minimax-source.mp4) 和 [输入原稿](../../assets/textures/sprites/amber_fox_style_v2/idle_256/sources/) 已保存。Godot 4.8 已验证动画加载、全部纹理尺寸、帧数、帧率与循环设置。

## 大色块画风跑步（MiniMax H3）

在同一 idle 设定上新增 [跑步原稿](../../assets/textures/sprites/amber_fox_style_v2/sources/run-static.png)：双手在腰腹前握同一剑柄，剑身斜向前上方，肘腕随握持方向自然弯曲。沿用原有金黄、奶白、棕色与无连续深描边的大色块画法。

[2 秒跑步循环视频](../../assets/videos/characters/amber_fox_style_v2/run.mp4) 由该关键姿势通过 `video_minimax_h3_i2v` 生成，固定画面中的水平位置、双腿交替，首尾约束为同一关键帧。生成时以可读步频完成三轮步态，再按 1.5 倍整理为游戏跑步节奏。

[透明跑步图集](../../assets/textures/sprites/amber_fox_style_v2/run-atlas.png) 与 [run SpriteFrames](../../assets/resources/sprites/amber_fox_style_v2/run_sprite_frames.tres) 共 24 帧、12 fps、2 秒循环，224×224 画布、脚底锚点 (112,192)，最近邻与二值 alpha。按 idle 的脸部像素宽度匹配角色尺寸；屈膝跑姿自身约 68×74px，不把弯曲姿势拉高至站立高度。已验证 Godot 加载、帧数、纹理尺寸和循环设置，尚未替换 Player 现有整套动作。

[跑步关键帧对齐](../../assets/resources/sprites/amber_fox_style_v2/run-keyframe-alignment.json)、[动画逐帧对齐](../../assets/resources/sprites/amber_fox_style_v2/run-video-alignment.json)、[提示词与参数](../../assets/resources/sprites/amber_fox_style_v2/run-video-generation.json)、[可重跑工作流](../../assets/resources/sprites/amber_fox_style_v2/run-minimax-workflow.json) 已保存。

## 256×256 大色块跑步（MiniMax H3）

使用新生成并经比例、握剑与尾巴修正的 [跑步关键帧](../../assets/textures/sprites/amber_fox_style_v2/move_256/run-keyframe-v4.png)，通过本机 ComfyUI `video_minimax_h3_i2v` 生成 [原始视频](../../assets/videos/characters/amber_fox_style_v2/move_256/minimax-source-v4.mp4)。保留当前三分之四朝向、黄棕奶白配色与大色块画法，双手携剑原地跑动，腿部交替支撑并有短暂腾空，头发、披肩和单尾随步伐运动。

[无损循环预览](../../assets/textures/sprites/amber_fox_style_v2/move_256/run-preview-v4.png)、[透明 APNG](../../assets/textures/sprites/amber_fox_style_v2/move_256/run-transparent-v4.png)、[24 张透明 PNG](../../assets/textures/sprites/amber_fox_style_v2/move_256/frames/)、[图集](../../assets/textures/sprites/amber_fox_style_v2/move_256/run-atlas-v4.png) 与 [SpriteFrames](../../assets/resources/sprites/amber_fox_style_v2/move_256/sprite_frames.tres) 已保存。帧尺寸 **256×256**，24 帧、12 fps、2 秒循环；模型视频为 512×512、24 fps，取源帧 0、3、6 至 69，以 1.5 倍速度播放。全部帧统一上移 8px，将生成视频的地面接触线 248 对齐到脚底锚点 **(128,240)**，保留自身起伏，不逐帧拉伸或固定脚底。显示沿用 idle 的最近邻、无损、无 mipmap 和相同像素尺寸。

采用绿色背景抠像与二值 alpha，剑随动作移动，不套用 idle 的静态剑身遮罩。局部修复轮廓绿边并移除 5 个孤立杂点，没有整图减色、平滑或覆盖静态脸。全部 24 帧按脸、头发、上身、双手、剑、腿脚和尾巴对照视频，并检查深浅底色；脸部及身体内部 RGB 保持原视频像素。完整剪影没有独立逐像素遮罩，自动审核明确保留这一覆盖限制，另有逐帧目视记录。

[提示词与参数](../../assets/resources/sprites/amber_fox_style_v2/move_256/generation.json)、[视频工作流](../../assets/resources/sprites/amber_fox_style_v2/move_256/minimax-workflow-v4.json)、[抠像工作流](../../assets/resources/sprites/amber_fox_style_v2/move_256/matting-workflow-v4.json)、[对齐与帧映射](../../assets/resources/sprites/amber_fox_style_v2/move_256/alignment.json)、[像素审核](../../assets/resources/sprites/amber_fox_style_v2/move_256/pixel-audit.json)、[目视记录](../../assets/resources/sprites/amber_fox_style_v2/move_256/visual-review.json) 及 [Godot 校验](../../assets/resources/sprites/amber_fox_style_v2/move_256/engine-validation.json) 均已保存。干净导入测试使用与项目相同的资源路径，24 帧加载正确，277625 个不透明像素 RGB 与全部 alpha 均和源 PNG 一致。此 SpriteFrames 作为独立跑步资源交付。

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

## 大剑攻击 v2（历史 0.6 秒版本）

[v2 历史预览](../../assets/textures/sprites/amber_fox_style_v2/attack_power_v2/previews/idle-attack-return-v2.png) 为 15 帧、25 fps、0.60 秒、不循环。首尾完整 PNG 直接复用已认可的 idle，24 帧原版 idle 和现有 run 的文件字节保持原样。以原版 idle 加入内置 imagegen 制作的蓄力、收势关键稿，通过本地 `video_minimax_h3_i2v` 分段生成连接；三个原视频、关键稿、实际提示词、种子和工作流保留在 [生成记录](../../assets/resources/sprites/amber_fox_style_v2/attack_power_v2/generation.json)。动作参考 [双手重斩姿势](https://posemy.art/sword-fighting-poses/power-slash-attack-pose/4807/) 的蓄力、落剑和重心转移。

选帧保留蓄力、快速下斩、落剑短停和收回，剔除剑尖出框、剑身弯曲、侧成细线和偏长的候选帧。完整剑刃人工端点量测约 105–113px，idle 约 111px，端点判断约有 2px 误差；这是近似几何核对，不宣称每帧物理长度完全一致。详见 [剑长对照](../../assets/textures/sprites/amber_fox_style_v2/attack_power_v2/sources/review-weapon-landmarks.png) 与 [测量记录](../../assets/resources/sprites/amber_fox_style_v2/attack_power_v2/weapon-length-review.json)。双手、脸、躯干、腿脚和尾巴均逐帧与原视频对照，并检查深浅底和棋盘底，无新增连续黑描边；表情、尾巴剪影和小色块仍有模型变化。

13 个内部帧透明化后保留的脸部和身体内部 RGB 与对应视频完全一致，两个完整 idle 端点独立按 PNG 哈希核验。RGB 无损参考视频是三个原视频解码后最近邻缩小的 FFV1 拼接，不冒充单次模型原片；[像素审核](../../assets/resources/sprites/amber_fox_style_v2/attack_power_v2/pixel-audit.json) 数值错误为 0，但没有独立全身遮罩，自动状态仍为 `needs_visual_review`，完整逐帧目视记录另存。实际项目导入和真实 Player 检查均已通过。
