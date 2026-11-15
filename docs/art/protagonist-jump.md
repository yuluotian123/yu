# 主角三段跳跃

更新：2026-11-15，v2。用户选择“轻快自然：小幅屈膝蓄力、腾空收腿、落地缓冲”，要求三段分别制作、各自保持原架势持剑，并进一步要求以行走为基础。

三段均从现用行走的窄步态起跳，保留前后脚关系，落地继续迈步。上段保持头顶高位架剑，中段保持肩侧斜举，下段保持胸前向下斜持。双手和剑随肩部一起运动；披肩、马尾与尾巴跟随一次起落。完整预览集中在[人物档案](../characters/protagonist.md)，不单独展示跳跃分段。

## 当前资源与节奏

- 资源：[制作配置](../../assets/resources/sprites/amber_fox_style_v2/greatsword_jump/production.json)、[帧清单](../../assets/resources/sprites/amber_fox_style_v2/greatsword_jump/manifest.json)、[SpriteFrames](../../assets/resources/sprites/amber_fox_style_v2/greatsword_jump/sprite_frames.tres)。
- 上、中段各 30 帧，下段 28 帧，共 88 帧；画布 512×384，脚底锚点 (256,347)，与现用行走和攻击保持同一人物尺度。
- 预备 0.080 秒，上升 0.150 秒，顶点附近 0.1833 秒，下降 0.1667 秒，落地缓冲 0.200 秒。完整美术时序为 0.780 秒；实际空中阶段由竖直速度与触地状态切换，允许短跳和高处下落。
- 起跳速度保持 4.0，重力保持 16.5。真实玩家检查中完整跳高约 0.519 米，准备期间松键的短跳约 0.114 米，移动中可继续平移。
- 起跳前增加 80 毫秒地面屈膝；默认移动配置的蓄力时间仍为零，只有玩家启用。蓄力或空中换势请求排队，取消/控制锁清除未发出的跳跃。
- 持续移动时，`land` 结束直接进入对应 `run` 循环，跳过从站姿开始的 `run_start`；停止移动时仍按原逻辑返回待机。该跳过条件为动画前导段的可选配置，普通地面起步仍播放原起步动画。

## 制作来源

以三段现用行走循环的 `move_000.png` 为输入，用本地 MiniMax H3 int8 图生视频制作；28 步，不使用加速 LoRA。输入在 Aseprite 中扩展头顶空间后最近邻放大，避免上段高举的剑越界。三段分别生成，种子、任务编号、输入和完整工作流在制作配置中记录。每段原视频为 768×1024、24 fps、56 帧；未对人物做全图减色、静态脸覆盖或肢体拼接。

视频解码为最近邻 384×512 RGB，先选择姿势，再在 Aseprite 中抠绿、移入 512×384 交付画布、设置时长并导出。上段第一次生成只有行走，未采用；重做后选取完整的一次起跳与落地，剑尖全程保留。下段只取第一次起落，跳过原视频中多余的第二次小跳。首尾精确采用各自行走循环首帧，落地末段保持迈步方向。

空中以腰带扣跟踪整体竖直位移，减去视频中的起落，让 Godot 的碰撞体接管跳高；地面屈膝、腿脚伸收和附属物运动保留。不逐帧把脚底重新居中，也不在图中叠加第二次跳跃位移。

只在两像素轮廓带修正绿幕混色，记录于[逐像素修整表](../../assets/resources/sprites/amber_fox_style_v2/greatsword_jump/pixel-corrections.json)。角色内部 RGB 与 alpha 保持原始取样规则。`.aseprite`、独立 RGBA 导出、透明 PNG、图集、完整 APNG 和原视频均保留在对应资源目录。

## 重建与检查

1. 运行 `scripts/tools/art/package_stance_jumps.py --prepare` 提取配置指定的原始 RGB。
2. 用 Aseprite 执行 `scripts/tools/art/build_stance_jumps.lua`，生成三段源文件、序列帧、图集与 SpriteFrames。
3. 运行 `scripts/tools/art/package_stance_jumps.py` 核对源 RGB、修整表、竖直位移、独立 Aseprite 导出、图集及 APNG。
4. 按 `greatsword_jump/audit/*.json` 执行逐区域审核器，核对脸、头发、上身、手、剑、腿脚、尾巴与腰包。独立 Aseprite alpha 只证明导出一致；原视频没有独立前景遮罩，不能把自动检查当成抠图或人体美术质量证明。
5. Godot 导入后用 `--headless --path . --script scripts/tools/art/validate_stance_jumps.gd` 核对实际导入的全部帧与时长，再运行 `assets/scenes/tests/protagonist_jump.tscn` 检查真实输入与物理。另跑现有 `weapon_stances.tscn` 检查移动、换势和攻击回归。
6. 用 `--resolution 1366x768 --fixed-fps 60 --disable-vsync --audio-driver Dummy -- --capture` 运行跳跃场景，录制三架势完整左右朝向跳跃；Aseprite 执行 `scripts/tools/art/export_stance_jump_runtime.lua` 合成 3 秒实机 GIF，再用 `export_protagonist_docs.lua` 同步人物档案。

完整实际预览使用真实玩家组件、HFSM、物理跳跃和动画别名，录制左右两个方向的连续行走、起跳、落地与继续行走；镜头跟随角色平移，地面刻度显示实际移动。渲染为无光照检查视图。88 帧已通过 Godot 导入后 alpha/可见 RGB 对照、15 段命名与时长核验，以及短跳、移动跳、三架势回架、换势排队和取消检查。另对三段分别检查“先行走，再起跳，落地后直接回循环”，确认中间未插入待机或起步片段。

构建为 0 错误、8 个既有警告。完整武器/架势回归通过。导入日志仍出现既有 WorldAuthoring 配置类型转换错误（旧攻击 v27 的导入日志也有相同错误），不影响本次跳跃资源加载与测试场景；本次未修改该编辑器插件。检查范围与原视频前景参照限制见[复核记录](../../assets/resources/sprites/amber_fox_style_v2/greatsword_jump/visual-review.json)。
