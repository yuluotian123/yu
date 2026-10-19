# 灰绿居民少女

测试报告、预览图和逐帧对照资料已清理。本文保留角色规格与历史制作说明，运行帧、源素材和导出工具继续保留。

用户确定首名 NPC 为普通居民：人类少女，神情有点冷漠，灰色与绿色色调。交付角色设定、透明 idle 与行走动画。使用 generate-pixel-block-characters 和 pixel-character-animation-workflow。

## 造型与版本

[第一版设定](../../assets/textures/sprites/npc/gray_green_resident/source/character_design_v1.png) 是保留的人设：灰色齐肩短发、绿色发夹与眼睛、冷淡半垂眼、灰白高领上衣、苔绿色披肩和长外套、深灰膝裙、灰色长袜、灰绿短靴及灰色挎包。

第一版运行图因细碎发亮块、衣褶和深色轮廓受到用户批评。v2 重新设计了比例、脸和服装，被用户明确否定，不用于运行。v3 直接编辑第一版，用户明确要求沿用此造型并统一主角画风。最终 v6 保留同一造型，用主角原画参考整理大色块，生成灰绿角色关键稿；有光晕的 v4/v5 不用于运行。不得以“统一画风”为由再次修改已确定的人设，也不使用全图减色掩盖细节问题。

## 资源

- [原生关键稿](../../assets/textures/sprites/npc/gray_green_resident/source/keyframe_native_v6.png)：256×256，角色高 224px，脚底锚点 (128,240)。原始 RGB 稿保留在 source/keyframe_rgb_v6.png，粉色背景仅用于后续生成和透明遮罩。
- [待机 PNG](../../assets/textures/sprites/npc/gray_green_resident/idle/) 和 [行走 PNG](../../assets/textures/sprites/npc/gray_green_resident/walk/)：真实图生视频动作，共用画布、比例和锚点。idle 保留轻微呼吸与发梢/披肩运动；行走保留交替支撑和重心变化，不逐帧裁紧居中。
- 待机无损动画预览、行走无损动画预览、[SpriteFrames](../../assets/resources/npc/gray_green_resident/sprite_frames.tres) 与 [源视频](../../assets/videos/npc/gray_green_resident/)。预览为多帧 APNG，使用 PNG 扩展名。
- [生成记录](../../assets/resources/npc/gray_green_resident/generation.json) 包含修订范围、被否定版本、提示词、模型、种子和工作流；帧映射 记录实际选帧和透明化方法。

NPC 使用原生 CharacterAnimationComponent3D 与两状态 locomotion 图。Sprite 的 PixelSize 为 0.003214285714，缩放为 1.3，与主角参考世界高度 0.936 一致。NPC 碰撞体高 0.72，因此 Sprite 上移 0.108，使 idle 脚底与碰撞下缘 -0.36 重合。移动组件只翻转美术，物理体保持正缩放。

idle 为 24 帧、8 fps、3 秒循环；行走为 16 帧、12 fps、约 1.333 秒循环。引擎逐帧核验这 40 帧的尺寸、可见 RGB、全部 alpha 与导出 PNG 一致，并检查 idle 帧号实际推进、两段动画均存在不同图像、行走后恢复待机及暂停 AI 组件后清除请求。当前待机是轻微呼吸和发梢/披肩运动，眼睑变化较浅，不宣称已生成完全闭眼的眨眼动作。

## 校验边界

从 PyAV 解码 RGB 后统一最近邻映射到 256×256。保留未抠图参考帧，只用可区分灰绿材料和肤色的粉色背景色度生成 alpha，不全图减色、不覆盖静态脸、不逐帧缩放。PNG 为二值 alpha；运行纹理无损导入、无 mipmap、最近邻采样，禁止自动转有损 3D 压缩。

逐帧对照脸、头发、上身、双手、挎包和腿脚，并在深浅底检查边缘。待机自动报告 和 行走自动报告 的数值错误均为 0，但仅检查保留下来的内部 RGB；没有独立的全身前景遮罩，因此保留 needs_visual_review 状态，不能据此宣称没有任何误抠。风格、造型与轮廓由目视核对，不由“像素与视频一致”替代。目视记录 与 source/review_v6/ 保留完整帧图、局部原视频/透明结果对照。

原生导出脚本为 scripts/tools/art/prepare_gray_green_resident.py（v6 输入使用 --chroma，输出 source/keyframe_native_v6.png）和 export_gray_green_animations.py。旧版行走导出器、v2 至 v5 中间稿和旧视频已清理；保留第一版身份参考、当前 v6 源视频、工作流及逐帧证据。自动审核使用角色动画 skill 的 audit_sprite_frames.py。Godot 中的导入、动作切换与实际场景检查命令见 [NPC 图配置指南](../../scripts/gamelogic/ai/NPC_GRAPH_GUIDE.md#初版内容与验证)。

当前角色直接位于 `assets/scenes/spacelevel.tscn` 根节点 `Resident`，统一配置入口是 `AiAgentComponent3D.Graph`。
