# DDGI 接入与维护

当前 ForestTownLevel 已启用自研 DDGI：PC / Windows / Vulkan / Forward+，使用常规 Godot 4.8-beta 加本项目补丁，需要 GPU 支持 Ray Query。实现为 C# EC 场景捕获与调度、GLSL 探针计算、Forward+ 原生材质接收；不依赖 NVIDIA Godot 分支或 RTXGI SDK。算法参考 [DDGI 原始论文](https://jcgt.org/published/0008/02/01/)。

## 运行与调整

从项目根目录运行：

```powershell
dotnet build yu.csproj --no-restore
.\scripts\tools\engine\launch_godot.ps1
# 或直接运行游戏，从主菜单点击开始。
.\scripts\tools\engine\launch_godot.ps1 -RunGame
```

默认运行目录为 `D:/DCC/Godot_4.8/bin/ddgi_scene/`，必须连同其 GodotSharp 一起使用。已经打开的旧进程需要保存退出，再通过启动脚本重开。源码构建、补丁顺序和 NuGet 配套见 [引擎说明](../../scripts/tools/engine/README.md)。

接线位于 `assets/scenes/levels/forest_town_level.tscn` 的 `EnvironmentRig → Components → DdgiComponent3D`：`GeometryRoot = ..` 收集整个关卡，`WorldEnvironmentPath = WorldEnvironment` 绑定同宿主环境。昼夜、天气、地表组件先更新，DDGI 随后捕获。它只在运行时启用；编辑器静态 3D 视图暂不显示 DDGI，按 F5 进入游戏或 F6 运行关卡检查。

独立配置为 `assets/resources/rendering/ddgi/spacelevel.tres`：

| 参数 | 当前值与用途 |
| --- | --- |
| Origin | `(-24, -11.5, -24)`，世界空间最小角；小镇地面实际约在 Y=-11，不能套用 Y=0 样板 |
| ProbeCounts / Spacing | `(27, 8, 27)` / 2 米，共 5832 探针，范围至 `(28, 2.5, 28)` |
| ProbesPerFrame / RaysPerProbe | 256 / 128，每次更新 32768 条探针射线，含每探针 32 条固定分类射线；另有命中点方向光阴影查询 |
| MaxRayDistance | 由当前 Profile 配置追踪距离，单位为米 |
| UseEnvironmentSky | 开启，读取当前昼夜天空及已有云辐照；关掉后使用 SkyRadiance 常量 |
| Energy / Hysteresis | 间接光整体倍率 / 每次探针更新的历史权重（默认 0.9）；缓存不含相机曝光 |
| NormalBias / ViewBias | 接收位置偏移；过大可能越墙，不能替代几何与探针布局检查 |

关卡日月的 `LightIndirectEnergy = 0.75`。DDGI 独立对照应关闭 SSIL / SDFGI，保留 SSAO 和 SSR；这些开关仍由 Environment 手动配置，验证脚本只隔离测试实例，不改写美术保存的开关。体积内 DDGI 替换环境漫反射，镜面反射继续走原管线。F8 →「渲染与 FPS」显示 DDGI 状态、贡献实例、BLAS、探针、射线预算、天空来源、发光三角形与发光面遮挡查询预算。运行时可在远程 Inspector 修改组件 Profile.Enabled / Energy 等参数作对照。

## 在其他场景接入

1. 使用现有 `GameObject3D` 环境宿主，在其 Components 中添加 `DdgiComponent3D`。
2. 新建本场景的 `DdgiProfile`，设置世界空间范围；引用已有 WorldEnvironment，GeometryRoot 指向完整贡献场景根。
3. 确保相机没有覆盖掉该 WorldEnvironment 的 Compositor。组件会复制并保留原合成器，在其前面插入 DDGI，退出时还原。
4. 贡献光源的间接能量必须大于零。先分别检查天空遮挡、直射回弹与自发光，再调整整体能量。

无需替换 StandardMaterial3D 或给普通 Spatial Shader 加接收代码。不支持的贡献对象可设节点 metadata `ddgi_contribute=false` 排除；它仍能接收。自定义贡献 Shader 需要适配命中协议或使用不渲染到主相机的 GI 代理，并验证该代理在收集根中。

## 数据流与代码归属

```text
EC 主线程捕获几何 / 材质 / 日月 → 有类型快照 → 渲染线程 BLAS / TLAS
    → PreOpaque：天空时域检测 → 辐射追踪 → 分类 / 重定位
    → 几何失效时重建固定可见性 → 发光面遮挡与面积积分 → 探针积分 → 提交历史版本
    → RenderSceneBuffersRD.set_ddgi 当帧发布
    → Forward+ 不透明 / 透明间接漫反射 → HDR LUT → 透明 → Film / Bloom
```

| 入口 | 职责 |
| --- | --- |
| `scripts/gamelogic/rendering/ddgi/DdgiComponent3D.cs` | EC 接线、合成器隔离、捕获及退出 / 托管重载清理 |
| `DdgiProfile.cs` / `DdgiSceneCapture.cs` | 配置、离屏几何、资源 Changed 版本、命中协议、CPU 贴图快照 |
| `DdgiEffect.cs` | RD 资源、Surface 共享 BLAS、刚体 TLAS、分批探针更新与状态 |
| `assets/shaders/rendering/ddgi/ddgi_trace.glsl` / `ddgi_material.glslinc` | 真实命中、Alpha 裁切、方向光遮挡、一次漫反射回弹、自发光与天空 |
| `ddgi_classify.glsl` / `ddgi_integrate.glsl` | 探针有效性与重定位、八面体辐照度 / 距离矩、边界与历史 |
| `ddgi_temporal.glsl` / `ddgi_sky.glslinc` | 世界单位天空变化检测、光照历史版本、逐探针版本提交与共享天空求值 |
| `ddgi_emissive.glslinc` | 发光三角形的球面投影面积、接收半球裁剪与解析漫反射形状因子 |
| `ddgi_debug_sample.glsl` / `ddgi_sampling.gdshaderinc` | 独立 GPU 读回验证；正式材质使用引擎中的查询实现 |
| `assets/shaders/weather/surface_patterns.gdshaderinc` | 栅格与 GI 共享的积水 / 积雪噪声 |
| `scripts/gamelogic/rendering/ddgi/ddgi_gpu_release.gd` | 极小的原生 RID 回收桥，回调可跨 C# 域卸载存活，必须保留 |

运行逻辑使用 C#，验证驱动使用 GDScript。`relink_gpu_cleanup.gd`、`cloud_radiance_copy.gd` 同样承担原生资源生命周期职责，不能因扩展名而删除。

## 原生接收与源码边界

引擎基线为官方提交 `4d10419cacd6d3009976e8f9fd3abd970fd58018`。三份顺序补丁覆盖：Ray Query 的零 SBT / TLAS 上传修复与 Vulkan SPIR-V 兼容；RenderSceneBuffersRD 当帧发布及 Forward+ 接收；当前视图原生 HDR 天空桥。文件与复现步骤统一维护在引擎说明中。

主要改动在 `servers/rendering/rendering_device.cpp`、`drivers/vulkan/rendering_device_driver_vulkan.cpp`、`renderer_rd/storage_rd/render_scene_buffers_rd.*`、`renderer_rd/forward_clustered/render_forward_clustered.*`、`renderer_rd/shaders/forward_clustered/scene_ddgi_inc.glsl` / `scene_forward_clustered.glsl` 及天空实现；反射图集退出时遗漏 color cube 的释放也已修复。

探针保存未曝光线性 HDR 的 E/PI。接收在 AO、底色与金属度处理之前乘当前视图曝光；相机曝光不进入持久缓存。天空接口提供逆旋转、UV 边界、世界单位换算与版本，纹理由引擎拥有，生产者不得释放。每个效果拥有独立缓存，每帧更新一次，再向各视图发布。视图 binding 39–42 接收三张借用纹理及 48 字节参数 UBO；发布仅在当帧有效，关闭效果或摘除合成器后自动旁路。

Lightmap 优先；显式 IRRADIANCE 按 alpha 混合；unshaded 不接收。体积外恢复原环境漫反射。当前每个视图仅一个体积，不支持多体积混合。场景对象、加速结构和探针生产仍在项目侧，尚未整体迁入 RendererRD::GI。

## 已支持与限制

| 项目 | 当前范围 |
| --- | --- |
| 几何 | 三角形 ArrayMesh / 常规 PrimitiveMesh；静态和刚体变换；按 Surface 共享 BLAS，Changed 后重建 |
| 命中材质 | StandardMaterial3D；town_toon / town_ground 底色混色、UV、顶点色、ORM 金属度、自发光；plant_painterly 调色；leaf_article 渐变与 Alpha 裁切 |
| 天气 | 受控材质的湿润变暗、积水与积雪低频漫反射；涟漪、细法线与镜面仍在栅格路径 |
| 贴图 | CPU 可读 Texture2D，缩放至 128²；颜色 sRGB、ORM 独立线性数组，各最多 31 张实际纹理加白色层；动态图像原位更新需发出 Resource.Changed |
| 光照 | 方向光一次漫反射回弹、稳定的不透明发光面积分、天空可见性；不含点光 / 聚光灯贡献、多次回弹或镜面 GI |
| 未支持贡献 | 任意 Shader、蒙皮 / BlendShape 变形、MultiMesh、Sprite3D 遮挡、透射、水、法线 / 自发光贴图；StandardMaterial 的顶点色与金属度贴图尚未适配 |
| 叶片 | Alpha 裁切、双面薄卡片；GI 使用静止网格，不追踪风动顶点 |

不支持 Ray Query 时报告 unsupported 并保留原环境路径；尚无等价软件 GI，当前关卡的暗部效果不能保证与支持光追的 GPU 一致。

平稳光照下，每 23 次更新才能轮转 ForestTownLevel 全部探针；快速全局光照变化时会临时突破每帧预算，见下文。CPU 仍逐帧遍历与打包，尚未完成大场景性能优化。状态中的 gpu_update_ms 只统计探针任务，不能代表完整 GI 或整帧成本。交互式 C# 域重载、AMD / Intel、Vulkan 验证层和复杂薄墙仍需专项验收。

## 时域稳定性

旧实现把天空 bake revision、微小日月方向与天气参数变化都当成全量失效，每次重新开始至少一轮无历史更新。实际天空连续烘焙时历史无法积累，旋转射线的噪声、距离矩和探针有效性变化就会直接出现在画面中。

当前按探针保存两个 uint 历史版本，分别管理辐照度和几何可见性。GPU 用 32 个固定方向比较未曝光世界天空，相对变化超过 30% 才判定天空突变；CPU 对材质与光源的显著跳变更新光照版本，连续变化保留历史。天空 bake revision 和曝光换算因子本身不再触发清空。几何 / Alpha / 追踪距离变化更新可见性版本。可见性使用独立的 256 条固定球面射线，只在几何版本变化或探针重定位后重建；几何不变时保留距离矩，避免旋转辐射射线推动低权重区域在有光 / 全黑之间跳变。无效探针不提交有效可见性版本，重新有效后必须先补齐距离数据。积分完成后以独立 dispatch 提交版本，避免多个工作组读写同一版本标记的竞态。

GetStatus 的 generation 仍表示输入修订，不能据此判断历史被清空；lighting_epoch / visibility_epoch 是 CPU 失效版本，DebugReadback 返回的 debug_history_epoch 包含 GPU 检测到的天空突变。

每探针前 32 条射线固定分布于完整球面，用于分类与重定位；余下射线旋转并积累漫反射。RaysPerProbe=32 时全用固定射线。已稳定的有效探针只在几何版本变化后重新分类，防止采样方向变化导致反复失效。该机制不冻结日月或云动画，关灯、换材质、天空突变仍会在对应探针下次更新时刷新；刷新时延仍受一轮更新预算限制。低于突变阈值的变化通过历史渐进响应。

夜间的小灯罩不能靠旋转球面射线偶然命中来供光。不透明发光三角形单独收集世界顶点与材质索引，每个分成四个子三角形，以固定中心查询遮挡；未遮挡的发光面按球面投影边积分，并裁剪至接收法线的半球，得到 E/PI。随机命中路径不再重复计入这些发光面。无阻挡时面积项为解析形状因子，部分遮挡与积雪边界由四个子三角形的可见性 / 表面状态近似，细遮挡精度仍受此分割限制。Alpha 裁切发光材质暂保留原射线贡献并报告警告，不承诺同等稳定性。

当前灯罩为 48 个发光三角形，每次更新最多增加 `256 × 48 × 4 = 49152` 条发光面遮挡查询；新建可见性数据时另有每探针 256 条固定查询。这些均在原 32768 条辐射 / 分类射线预算之外。发光样本缓冲约 3.75 MiB、可见性暂存约 2 MiB，按当前更新预算计算；不能把原射线数当成完整 GI 成本。

加速入夜或手动跳时还存在另一种过曝来源：相机先切换至夜间曝光，而 0.9 历史权重与分批更新仍保留白天辐照度。组件额外提供当前世界天空强度作为调度提示；每次渲染更新超过 0.05 EV 的天空能量变化，或方向光发生突变时，项目侧在同一帧内分批刷新整轮探针，临时将辐照度历史权重置 0，再发布给原生接收。增量天空稍后完成烘焙时再补一轮。该过程不修改曝光曲线、日夜端点、灯光能量或几何可见性缓存；纯相机曝光变化不触发此策略。

整轮更新复用原有临时缓冲和当帧绑定，低预算配置至少使用 256 个暂存槽，避免大量微小 dispatch。当前关卡的整轮辐射 / 分类预算为 746496 条，发光面遮挡查询上限 1119744 条，GPU 工作量会明显增加；快速调时以画面及时响应为优先，尚未完成性能专项优化。光照平稳后自动恢复 Profile.ProbesPerFrame 与原历史权重。F8 和 GetStatus 的 lighting_catch_up / updated_probes 可确认当前模式；射线状态显示实际更新量。

## 关卡联调

直接运行白盒关卡，通过 F8 检查 DDGI 状态，在正午、午夜、连续入夜、跳时及雨雪条件下观察间接光照、材质变化和退出回收。自动验证脚本与历史实验输出已移除；极暗处仍可能出现低采样数噪声。

下一步优先处理 RendererRD 场景所有权与脏标记、局部几何失效、预算内更新优先级、性能与复杂薄墙；随后分别验收材质图集、探针调试视图、蒙皮 / 风动、多次回弹与世界流送。软件射线或预计算重光照属于后续兼容路线，当前未实现。
