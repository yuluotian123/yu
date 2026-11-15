# 双手大剑攻击参考与制作

更新：2026-11-15。当前采用用户确认的 **v27**，三架势各一次完整普攻、各 64 帧。预览和操作说明见[主角人物档案](../characters/protagonist.md)。本页只维护现用动作；旧版时长、已撤下的关键图提示词和失败方案不再作为制作要求。

## 当前动作

| 架势 | 动作与收势 | 蓄势 | 挥出 | 触点 | 收势 | 总时长 |
| --- | --- | --- | --- | --- | --- | --- |
| 上段 | 蹬腿送髋、胸肩下压带刀下劈；低位承重，收肘后慢提剑回高架 | 300 ms | 75 ms | 25 ms | 1200 ms | 1.600 秒 |
| 中段 | 从后侧蓄剑，腰肩带刃向前横切；继续随挥、承重收柄，再回肩架 | 301.875 ms | 194.063 ms | 43.125 ms | 840.938 ms | 1.380 秒 |
| 下段 | 先收柄到腰侧，后腿蹬地向前平刺，再沿原线收回低架 | 376.14 ms | 79.19 ms | 59.39 ms | 752.28 ms | 1.267 秒 |

上段第 12–15 帧累计前移为 0、14、34、48 原生像素，对应 0.300–0.375 秒、总距离约 0.1986 米。实际位移通过 CharacterBody3D 碰撞执行；触点完成前移，之后站在落点收势。中、下段没有额外前移。

上段 23–62 帧使用原角色完整的站定收势，身体、头肩、双手和腿来自同一姿势，不分段拼接，不逐帧居中。上段 40 帧和中段 31 帧的钢刃以现用待机母版修整，按护手连接并保留人物遮挡。首末帧精确接回原待机。

## 动作参考

以下来源用于动作分析，第三方视频或截图不进入游戏素材。表中时间是此前查阅范围；本项目的帧数和时长为自行设计，并非参考招式实测值。

| 来源 | 查阅位置与用途 | 本项目取舍 |
| --- | --- | --- |
| [Monster Hunter 官方大剑演示](https://www.youtube.com/watch?v=t2h3Uxj7nNg) | 重点查看 10–15 秒蓄力、下劈及低位承重；后续对照 9.7–11.3、15.3–18.5 秒连续画面 | 借鉴腰腿带动和落刀后的承重，缩减长蓄力和多段追击 |
| [LIER：How to Cut with a Longsword 05 - Horizontal Cuts](https://www.youtube.com/watch?v=jCFmBmdRU-U) | 0–7.6 秒试斩、170–177.6 秒示范；连续帧观察双手、转刃与切过后的控制 | 中段朝面向方向切出；刃口、护手与手腕处于同一切割平面，避免用宽剑面横拍 |
| [Sean Franklin：Physics, Push-Pull with the longsword, bad cuts… and you!](https://swordstem.com/2018/05/23/physics-push-pull-with-the-longsword-bad-cuts-and-you/) | Follow Through、Body Structure、Edge Control | 以身体带动稳定双手结构，保留随挥与刃向控制 |
| [Wapenkunst：Figueyredo 简单规则 1–16](https://www.youtube.com/watch?v=3ARRdoA35Po)；[Figueyredo 译本](https://oakeshott.org/wp-content/uploads/2016/10/Figueiredo_Montante_Translation_Myers_and_Hick_v2.pdf) | Rule 11 横切；Rule 4/5 收臂和平刺；Rule 2 上方斩击与回架。译本印刷页 14 的 Rule XI | 只取一次面向前方的横切或平刺，控制步幅和转身 |
| [PlayStation Japan：《浪人崛起》大剑演示](https://www.youtube.com/watch?v=mjPeqAJJH2Q)；[官方系统页](https://teamninja-studio.com/ronin/us/system/) | 全片 58 秒，每 2 秒关键帧 | 对照双手间距、腰肩承重和长剑减速，保持原角色视角 |
| [Onos Gaming：Nioh 2 Odachi Weapon Showcase](https://www.youtube.com/watch?v=zCyY5fjI7dU)；[《仁王 2》官方操作手册](https://www.gamecity.ne.jp/manual/t2wNiSht/steam/bri/2020.html) | 前 96 秒教学区域与 Quick Attack / Change Stance | 比较三段轮廓与回架；沿用三段直选，不扩展残心或属性机制 |
| [Genshin Impact 官方：Eula: Surging Frost](https://www.youtube.com/watch?v=deByypNHedI)；[官方发布页](https://genshin.hoyoverse.com/en/news/detail/103931) | 普通攻击及舞步式剑法介绍 | 只取身体线条、步法和发尾韵律，保持当前角色与双手握剑 |

## 制作约束与来源

- 原生画布 512×384、脚底锚点 (256,347)，由 320×384 待机水平加边；推理输入 1024×768，人物尺度不变。
- 双手握同一连续剑柄，保持人物比例、衣服、腰包侧别及大剑结构；中段刀刃朝前切出，不倒放出刀作为收势。
- 不在贴图上绘制刀光、模糊或粒子；当前三架势无武器攻击特效。换势周身粒子由独立场景管理。
- 完整新动作采用本地 ComfyUI MiniMax H3，Aseprite 负责选帧、定时、透明化及局部修整；不将视频生成称为逐帧手绘。
- [制作配置](../../assets/resources/sprites/amber_fox_style_v2/greatsword_stance_attack/production.json)中的 `source_parts` 和 `source_pose_map` 是当前来源依据。v10、v12、v17、v21、v22 的文件名可能仍是现用源，不能仅按版本号删除。
- `generation_input_files` 保存现用工作流与原始输入的对应关系；[关键姿势](../../assets/resources/sprites/amber_fox_style_v2/greatsword_stance_attack/keyposes.json)只保留当前制作所需记录及提示词。中段整段试片仅用于关键图来源，未直接作为播放片段。
- [逐像素修正](../../assets/resources/sprites/amber_fox_style_v2/greatsword_stance_attack/pixel-corrections.json)记录 alpha 与钢刃修改；原始视频、RGB 源帧、可编辑 Aseprite 和独立导出一起保留。PNG/APNG 是像素依据，GIF 用于观看。

## 重新封装与验证

读取当前 `production.json`，使用含 PyAV、NumPy、Pillow 的 Python 运行 `scripts/tools/art/package_stance_attacks.py --assemble` 无损拼接，再运行 `--prepare` 提取 RGB；随后在 Aseprite 执行 `scripts/tools/art/build_stance_attacks.lua`，最后运行 Python 包装脚本生成 APNG、审计映射及技能时间轴。

每招使用 64 个等长 Godot 播放条目，上段 40 fps，中段约 46.376812 fps，下段约 50.513023 fps。Aseprite/APNG 按累计边界取整到毫秒；`records` 分别保存整数 `duration_ms` 与运行时 `runtime_duration_ms`。`holds` 记录节奏点，`phases` 使用从 0 开始、右端不含的帧范围。

高架攻击的 `root_motion.source_forward_pixels` 描述源图提取偏移，`root_motion.forward_pixels` 描述物理前移，两者独立。当前源图不需要逐帧水平抵消。技能 Timeline 与冷却覆盖完整动作；换势等待收势完成，连续点击不续招。

v27 已核对 192 帧源图与修正台账、独立 Aseprite 导出、图集、APNG、Godot 导入，以及左右各两次实际播放。完整玩法、碰墙和取消检查沿用运行逻辑未变的 v26 结果。导出一致不能替代角色结构、力量感或连贯性的目视检查；没有独立全轮廓人工标注或盲审。详见[当前复核记录](../../assets/resources/sprites/amber_fox_style_v2/greatsword_stance_attack/visual-review.json)。
