<a id="original-author-cloud-implementation"></a>
# 原作者云层方案的实现

来源：[新杨XIYAG 的文章](https://zhuanlan.zhihu.com/p/660806535)、[GitHub 仓库](https://github.com/xinyangaa/Unity_URP_Genshin_Impact_Programmed_Skybox/tree/ef1bd4c6fa486977a323c3badaba5740feae1d55)。固定提交：`ef1bd4c6fa486977a323c3badaba5740feae1d55`。

<a id="active-resources"></a>
## 当前使用的资源

`ForestTownLevel/EnvironmentRig/OriginalClouds` 包含来自 `assets/scenes/environment/reference_clouds.tscn` 的 BaseClouds、HighClouds 和 SecondaryClouds 三个 MeshInstance3D 节点。可见云层几何直接配置在场景中；昼夜组件还会在仅包含云层的内部世界中采样同样的三层云，用于天空辐射，详见[昼夜集成](time-of-day.md)。

`assets/models/environment/clouds/` 中使用的 FBX 和 `assets/textures/environment/clouds/` 中的 PNG 均逐字节保留原文件，并已对照 [source_manifest.json](../../assets/resources/environment/clouds/source_manifest.json) 中的 Git 对象哈希验证。`original_cloud_mesh.res` 是 Godot 对作者 `cloud.fbx` 的导入结果，包含 80 个顶点、20 个四边形及原始 UV2。`78678.png` 是源基础云材质引用的原始 RGBA 图像，保留 sRGB 导入标记、mipmap 和钳制寻址。没有合成 SDF、模糊、缩放或重新拼排图集。原始噪声 PNG 也使用完整的 2048² 分辨率。

[`cloud.gdshader`](../../assets/shaders/environment/clouds/cloud.gdshader) 保留原 `Cloud.shader` 的片元公式：UV2、应用两次的噪声平铺、`_Time.x * 0.05`、0–0.03 的正向标量 UV 扰动、乘以 A 通道的单侧 0.08 SDF 过渡、平方后的光照方向权重，以及通过 R 通道进行的四色混合和 G 通道边缘光。材质噪声平铺为 (0.3, 0.3)。原 Unity 着色器及材质导出文本在转换后已清理，上游版本仍可从固定提交查看。转换后的时间曲线位于 `assets/resources/environment/clouds/`。

`base_timeline.tres` 包含动画片段 `1387710026462685712` 中全部 22 条材质曲线；源场景将它们绑定到基础云。关键帧和未加权 Hermite 切线均被保留；时间域从 0–4 归一化到 0–1，切线相应乘以 4。其中 SDF 起点为 0.041，终点为 0.050，不再使用此前的端点修补。高层云和次级云各自保留 22 条源材质曲线，分别绑定到场景局部的独立材质。`000001EEA8FB26F8.png` 是它们未经修改的原始图集。

<a id="godot-integration-differences"></a>
## 接入 Godot 时的差异

- 将 HLSL 翻译为 Godot 空间着色器语法。云层使用不受光的 ALBEDO、透明混合且不写入深度；源实现的 `ZWrite On / ZTest NotEqual` 无法直接用于游戏采用反向 Z 的天空背景。着色器把云放到远端深度，让场景几何保持在其前方。
- 顶点投影以当前游戏相机为中心，使用真实透视投影，或已有的正交天空 FOV 和俯仰角。保留 GPU 投影 Y 符号、原云层 UV 及几何。作者参考相机高度 (-14) 作为视图偏移保留。
- 云节点使用 Unity 场景保存的位置 (0, 0, 0)，未应用示例专用的动画位置 (-119.6 Y)，因为它会让整层云落到本关卡可见范围之下。这是场景接入选择，未修改网格或着色公式。
- 三层云使用相同的透视／正交投影及当前太阳／月亮方向。各自材质启用 Local To Scene，彼此独立。
- 噪声时间由已有的确定性世界时钟驱动。SDF 动画周期默认为 1.6 游戏小时，在默认 20 分钟一天的设置下等于作者的 80 秒周期。原颜色和 SunMoon 曲线将日出、正午、日落及午夜映射到现有的 20 分钟游戏日。暂停、GM 调时及存档恢复保持可用。

<a id="completed-layers-and-rotations"></a>
## 已补全的云层与旋转

`rotation_timeline.tres` 保存源云组、高层云和次级云的欧拉 Y 旋转曲线。云组及次级云以线性切线从 1 度变化到 361 度，高层云保留原本为零的端点切线。归一化 Curve 的切线乘以 4。`CloudRotationPeriodHours` 和 `CloudEvolutionPeriodHours` 默认均为 1.6 游戏小时，`CloudNoisePeriodHours` 默认为 8 游戏小时。游戏运行和编辑器都通过 `TimeOfDayProfile.SampleCloudTime(totalGameHours)` 采样相位。在默认 20 分钟一天的设置下，它们分别保持此前的 80 秒旋转／SDF 周期及 400 秒噪声周期。修改一天的时长只改变世界时钟速率，不改变固定日期时刻的云位置。Godot 偏航角取负以适配 Unity 的坐标手性。所有云层使用绝对世界时间，因此支持暂停、跳转、倒退、速度变化及存档恢复。

所有云层现在只继承公共 `GroupYaw` 风向轴。源资源仍保存 `HighYaw` 和 `SecondaryYaw`，但不应用它们：叠加组轨道和子轨道会使次级云以基础云两倍的速度旋转，也会让高层云相对基础云加减速。场景中配置的局部网格朝向在整个周期内固定。未应用缺失 FBX 的 179.5 度导入朝向或示例专用偏移。

已上传 FBX 的 GUID 为 `6cc1303ea41d42846945248ef433ee35`，与原基础云一致。示例中高层云和次级云的网格 GUID `4e842a0dab424954fbbe40e4d81b5a3d` 在两次资源上传提交（`ac163529`、`4144b98e`）及当时最新目录树中均不存在。2026-10-06 列出的 16 个公开分叉均保留上游推送时间与仓库大小，未找到更新的资源上传。

为补全所需云层，离线烘焙了 `reconstructed_high_mesh.res` 和 `reconstructed_secondary_mesh.res`。它们是**重建网格，并非找回的作者原模型**：分别以错层环带布置 28 个高层和 14 个次级竖直四边形，为原始 512×256 图集单元保留 2:1 平面比例。烘焙器直接从基础网格提取环半径和向上方向，包括约 0.5 度的倾斜；较高环带只改变高度，不改变面向相机的方式。移除了径向朝向及随机滚转，因此上下云层采用相同的透视缩短约定和着色投影。源 RGBA 贴图、噪声和逐层动画曲线保持原样。三种材质均启用 `bound_atlas_cell`，把扰动后的 UV 限制在各自的 2×4 图集单元内，并在边缘最后 0.012 UV 范围渐隐，避免邻格碎片露出；原基础 FBX 配合提供的噪声也曾出现该问题。这是明确的采样边界修正，没有修改任何源纹理或 FBX 字节。可以在各材质上关闭它，检查原始的无边界限制公式。

重建网格已保存为资源。一次性烘焙器已清理，场景节点、网格和材质直接从项目加载。

图集边界遮罩还会在一至两个像素的保护带内渐隐未扰动 UV。MSAA 可能在云片覆盖范围之外评估边缘片元，使插值 UV 在噪声偏移前就进入相邻图集单元。源 UV 保护带可消除这些浅色点状接缝，无需修改网格或图集。另一种更亮的点线伪影来自天空着色器对太阳圆盘的抗锯齿：远离太阳处的切线投影奇点造成无界导数。现在太阳和月亮都先将半径平方限制到 2，再计算边缘导数。

编辑器按 `StartDay * 24 + StartHour` 采样云位置，不使用独立的累计时间计数器。拖动开始日期／时间时，云的旋转、形状、噪声和颜色相位与游戏中跳转到相同时刻一致。运行时使用世界时钟／GM，支持暂停、零速、变速和倒退。旧序列化数据中以秒计的周期键按原 20 分钟一天的基准转换；过时的编辑器动画开关键仍可读取，但会被丢弃。

原材质的噪声 GUID `efdab6692df1af443b3b1b6d4996d2ed` 仍未找到对应资源。所有云层使用仓库中完整分辨率的 `Noise_091.png`，避免绑定为空。由于采用了这一替代噪声及重建布局，不能将当前结果称为对原文章 GIF 的完全视觉复现。
