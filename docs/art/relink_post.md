# Relink HDR 后处理

当前 `spacelevel.tscn → EnvironmentRig → WorldEnvironment → Compositor` 保存场景内的 Compositor。它包含透明绘制前的 HDR LUT 调色与透明绘制后的电影曲线、五级泛光。编辑器天空预览和运行时 EC 环境共用昼夜配置；2D UI 在 3D 后处理之后绘制。

这里只有一个 Compositor，其中两个 Effect 分别命名为 `Relink · HDR LUT (Before Transparency)` 和 `Relink · Film + Bloom (After Transparency)`，共用同一个 Profile。分成两阶段是为了让透明材质仍接收线性 HDR，最后再统一压缩高光，不会把电影曲线执行两次。

Profile 描述整条 Relink 后处理链：`Active` 和 `Exposure` 必须同步，LUT 查询与 Film/Bloom 对同一份曝光后的 HDR 进行处理；LUT 阶段只读取 LUT、距离等参数，Film/Bloom 阶段读取曲线输入与泛光参数。拆成两份 Profile 会让开关或曝光容易失配。Compositor、两阶段与 Profile 都使用 Local To Scene：同一场景实例内共用 Profile，不同场景实例各自拥有独立的 Profile。

## 捕获来源

来源为 `D:/Project/GitHub/MyMissionSystem/Captures/Relink/standard_d3d11_frame10803.rdc`，RenderDoc 1.46 完整 D3D11 捕获。没有使用主体几何缺失的 `relink_frame18802.rdc`。源文件 SHA-256、资源 ID、常量、LUT 原始字节哈希和模糊采样参数保存在 [capture_manifest.json](../../assets/resources/rendering/relink_post/capture_manifest.json)。RDC 与原始反汇编保留在本机，运行游戏不需要 RenderDoc 或外部工程。

| 内容 | 捕获证据 | 接入方式 |
| --- | --- | --- |
| 近/远 LUT | 24874 / 24881 生成；24905 绑定资源 323 / 329，视图 RGBA16F，32³ | `g_NearLUT.res` / `g_FarLUT.res` 为无损 ImageTexture3D，没有 PNG 截断、sRGB 解码或有损压缩 |
| LUT 输入 | 24905 使用 PQ 编码，HDR 输入除以 100 | 保留原编码与原始归一化纹理坐标；LUT 输出仍为线性 HDR，最大值超过 100 |
| 深度权重 | `g_ColorCorrectRange = (0,15,15,60)` | 0→15 米近 LUT 渐退，15→60 米远 LUT 渐入，15 米为原色；天空是否调色由 GradeSky 控制 |
| 电影曲线 | 31660，Shader hash `175f4f75-2316407a-919c00b6-b5e4f323` | 保留 0.15 / 0.05 / 0.004 / 0.50 / 0.060 / -0.066667 与 1.379064，泛光在曲线之后相加 |
| 模糊 | 31204 的九次双线性采样及原偏移、权重 | 使用观测采样形状，归一化增益后用于五级金字塔；强度交给本项目昼夜 Profile |

原 LUT 烘焙了该帧 `adaptedExposure = 1.4142035`。本项目先用当前相机已曝光的颜色乘 `Exposure`，查询 LUT 前除以捕获曝光，查询后乘回，避免把该帧的曝光再次乘到物理相机上。这个转换保留固定捕获的调色特征，没有重建原作每帧更新 LUT 的曝光历史。

Godot 自带 Filmic 的系数与该捕获不同，因此使用计算着色器处理曲线。原生 Environment 保持 Linear / Exposure=1，关闭原生 Glow / Adjustment；Godot 完成最后一次线性到 sRGB 转换，避免重复调色。[Godot 4.6.1 原生后处理源码](https://github.com/godotengine/godot/blob/4.6.1-stable/servers/rendering/renderer_rd/shaders/effects/tonemap.glsl)和[Compositor 文档](https://docs.godotengine.org/en/4.6/tutorials/rendering/compositor.html)提供接入依据。

## 调整与对照

展开 Compositor 的任一效果，再展开共享的 `Profile`（资源位于 `profile.tres`）：

- `Active`：整套后处理开关。关闭后恢复当前关卡原有 AgX、0.7 曝光与昼夜 Adjustment / Glow。运行时修改后调用环境组件 `RefreshProfile()`，或直接调用 `SetRelinkPostEnabled(bool)`。
- `LutStrength`：0 为仅电影曲线与泛光，1 为完整捕获 LUT 强度。
- `DepthRanges`：近/远渐变的四个米数。默认保留捕获值，可按关卡尺度调整。
- `Exposure`：本项目的曲线输入补偿；当前关卡为 3.0，用于适配原有物理灯光和相机。它是本项目校准值，不是原作常量。相机的日夜 EV 继续在 TimeOfDayProfile 编辑。
- `CapturedExposure`：LUT 元数据，保留 1.4142035。
- `GradeSky`：资源类默认关闭，对应捕获中不对天空使用场景 LUT 的分支；当前关卡的 Profile 开启该参数，天空也参与 LUT。如需天空跳过场景调色可关闭，天空始终经过电影曲线。
- `BloomEnabled / BloomIntensity / BloomThreshold / BloomFloor`：手动泛光参数。当前关卡开启 `DrivePostEffectsFromProfile`，这些值会被昼夜配置驱动；要手调先关闭该开关。
- `Godot Fallback`：关闭 Film 时的原生 Tonemap、曝光、Glow 与 Adjustment。昼夜驱动开启时，原生 Glow / Adjustment 仍取昼夜值；关闭驱动则取这里的手动值。单独关闭 Film Effect 的 Enabled 也会恢复原生曲线，不会因 LUT Effect 仍开启而保持 Linear。

F8 →「渲染与 FPS」可读取当前生效的 Tonemap / Exposure / Glow / Adjustment 与两阶段状态，并检查是否有重复后处理或相机覆盖。物理相机曝光在 HDR 场景生成时生效，Profile Exposure 是电影曲线输入的美术补偿；两者位置不同。Relink Film 开启时原生 Tonemap 只保留 Linear / Exposure=1 和一次 sRGB 输出。

```gdscript
var environment = $EnvironmentRig.get_meta("time_of_day")
environment.SetRelinkPostEnabled(false) # 原有 Godot 风格
environment.SetRelinkPostEnabled(true)  # Relink LUT 与电影曲线
```

C# 计算效果是 CompositorEffect 资源，环境行为仍属于 `DayNightEnvironmentComponent3D`。资源 Local To Scene，切换关卡时释放 GPU 程序和视口中间纹理，分辨率改变时由 RenderSceneBuffersRD 重建缓冲。支持当前 Windows Forward+ 渲染器；显式读取非 MSAA 颜色/深度，保留场景的 4× MSAA。

启用 MSAA 时，HDR LUT 调色后增加一次全屏绘制，将解析颜色写回 MSAA 颜色附件。Godot 在透明绘制后会再次解析 MSAA；只修改非 MSAA 层会被后一次解析覆盖。写回保留透明前调色顺序，深度附件保持原样。编辑器 Scene 视口与运行时都使用同一实现，可直接修改 `LutStrength`；无需天空编辑器插件。GM 同时显示阶段是否执行及 MSAA 写回状态。

EC 回收时暂时关闭阶段再释放 GPU 资源，重新生成时恢复回收前各阶段的 Enabled 状态，不覆盖用户单独关闭某阶段的设置。

GPU 清理通过 `relink_gpu_cleanup.gd` 在渲染线程执行。其原生回调不会因 C# 编辑器热重载失效，缓冲的最后一个引用也在该线程释放，避免退出或重新编译后由 C# 终结器清理 GPU 资源。

当前 Godot 4.6.1 Mono 在重载 C# 合成器的瞬间仍会输出 `managed_callable->delegate_handle` 报错，随后两阶段恢复执行。直接修改 Profile 参数无需重新编译；实际运行、场景回收与退出验证没有该报错。

## 复现与验证

1. 用 RenderDoc 1.46 的 `qrenderdoc.exe --python D:/Project/GitHub/yu/scripts/tools/rendering/extract_relink_post.py` 提取。`YU_PROJECT` 和 `RELINK_RDC` 可覆盖路径；事件编号对应上述完整捕获。临时输出在 `.godot/relink-analysis`。
2. 使用本项目的 Godot 4.6.1 Mono **Forward+** 执行 `--path D:/Project/GitHub/yu --script res://scripts/tools/rendering/import_relink_luts.gd`。需要实际 RenderingDevice；不能用 dummy headless 或 Compatibility 保存浮点 LUT。脚本校验原始半浮点字节后才保存。

当前验证在 D3D12 / RTX 5080 下完成：两套 LUT 字节一致，近/远调色和泛光均有实际像素变化；正午、午夜、黄昏及开关对照已检查，退出时没有新增 GPU RID 泄漏。

## 复现边界

### NVIDIA 4.8-dev 路径追踪

城镇材质通过 NVIDIA 的 `RT` 预处理分支适配。光栅保留 `fwidth/dFdx/dFdy`；光追使用相机投影估算表面像素足迹，并通过相邻噪声采样过滤积雪、水洼和雨滴。该足迹是相机近似，不是完整的次级射线微分。花草与雪层在光追中使用 Alpha Scissor / Hash，避免将片元 `discard` 编译进 closest-hit。

引擎补丁修正了 RT varying 被错误转换为 `frag_to_light` 成员的问题，构建脚本会应用 `scripts/tools/engine/patches/`。树叶基材保留剪裁和受光，屏幕深度描边叠加层在 RT 下不输出；卡通 `light()` 保留用于光栅，路径追踪使用其原生 PBR 光照。日月在路径追踪下启用间接光，切回光栅时恢复原有设置。

`validate_pathtracing.gd` 在 RTX 5080 / Vulkan 上覆盖城镇 31 个材质、标量/数组 varying、附加雪层、雨雪、Relink + 4× MSAA、DLSS Ray Reconstruction 与切回光栅。截图与诊断位于 `.godot/pathtracing-validation/`。不要将“仅原生编译通过”当作材质验证完成。

移植覆盖捕获 LUT、深度权重、电影曲线及模糊采样。泛光亮部提取、金字塔混合权重和场景曝光是本项目适配：没有照搬原作 EffectMask / Stencil 分类、Bloom 历史、角色专用调色、双阶段时域滤波、运动模糊或深度线条。超出 LUT 域的 HDR 查询采用纹理边缘钳制，没有重建原作的高亮解析回退。材质、灯光、天空与原作场景不同，因此这是基于捕获的色调接入，不代表完整渲染管线的一比一复原。

