# 小镇卡通渲染

建筑、石路、围栏和道具使用 `town_toon.gdshader` 的绘画式明暗：原三段色块与连续体积过渡混合，`painterly_blend=0.65`，设为 0 可对照三段效果。材质保存在 `materials/`，通过 riverside_town.tscn 的 Surface Material Override 序列化引用。玻璃、河流、花草及树叶保留专用材质。灯罩发光直接由材质参数控制。

`texture_detail` 越小纹理越简洁；`band_softness` 越小明暗边界越硬，当前默认 0.14；`light_wrap` 控制侧面的受光宽度。木石保留哑光，金属单独配置 surface_roughness、surface_metallic 和 specular_strength。材质接受原生日月光、天空环境光、阴影及大气雾。

树叶沿用原 alpha 轮廓、风动及树冠中心，以 `normal_roundness=0.68` 平滑叶片法线；顶点色权重降到 0.4，渐变强度 0.6，避免两次染色后暗面发黑。`directional_shadow_lift=0.12` 是日月照明下的风格化树冠透光量，设为 0 可关闭。点光和背向透光保留完整 ATTENUATION，不给范围外叶片补光。

EnvironmentRig → Components → Profile → Cartoon Lighting 可调整：

| 参数 | 默认 | 效果 |
| --- | --- | --- |
| NoonTemperature | 5600 K | 微暖白光，晨昏仍过渡到 3000 K |
| NoonSunTint | (1, 0.99, 0.96) | 减少与材质亮部叠加后的黄色偏色 |
| SunLux | 120000 | 正午直射光亮度 |
| DayExposureEv | 15.38 | Tonemap Exposure 统一为 1 后的物理曝光，越小越亮 |
| ShadowAngularDistance | 0 | 关闭 PCSS 大半影，独立于天空太阳盘大小 |
| ShadowFilterBlur | 1.0（当前关卡 0.65） | 柔化原生阴影边缘 |
| ShadowBias / ShadowNormalBias | 0.05 / 0.5 | 控制深度/法线偏移；过大会脱离接触面或扭曲细结构 |
| ShadowDistance | 55 m | 阴影覆盖范围，越大越分散贴图精度 |
| ShadowSplit1/2/3 | 0.2 / 0.4 / 0.7 | 级联边界比例，第一段覆盖角色实际拍摄距离；开启分段混合 |
| ContactOcclusionEnabled | false | 仅开启 DrivePostEffectsFromProfile 时由 Profile 控制 SSAO |
| ColorSaturation | 1.08 | 仅开启 DrivePostEffectsFromProfile 时由 Profile 控制饱和度 |

方向阴影使用 Godot 项目默认质量，保留 2× MSAA，三维画面按原始视口分辨率渲染。编辑器预览和运行时通过同一个 ApplyLighting 方法应用日月、曝光和阴影设置。

日月投影方向逐帧更新，Sun/Moon 关闭物理插值；天空辐照、雾和曝光按 Profile 的 VisualUpdatesPerSecond 刷新。环境配置见 [昼夜系统说明](../../../scripts/gamelogic/timeofday/README.md)。

参考：[Godot 灯光与阴影](https://docs.godotengine.org/en/4.6/tutorials/3d/lights_and_shadows.html)、[空间 Shader](https://docs.godotengine.org/en/4.6/tutorials/shaders/shader_reference/spatial_shader.html)。

## 直接编辑 Environment

WorldEnvironment → Environment 中可直接调整雾、SSAO、色调映射和 Adjustment。Profile → Scene Environment Controls 的 DriveFogFromProfile / DrivePostEffectsFromProfile 默认关闭，避免系统持续覆盖 Inspector。

天空亮度和曝光默认随昼夜变化；需要固定手调时，关闭 DriveSkyBrightnessFromProfile 或 DriveExposureFromProfile，再分别编辑 Environment → Background Intensity 或 WorldEnvironment → Camera Attributes。天空配色、日月与云动画继续由昼夜 Profile 管理。运行时修改 Profile 后，调用环境组件 RefreshProfile 刷新。
