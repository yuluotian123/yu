# 通用场景表面与卡通渲染

Spacelevel 已接入 [森林小镇](forest_town.md)，通过白盒基础场景保留角色、相机、昼夜、天气和 DDGI。建筑与植物分别建模，使用手绘图集和顶点配色；原有画面参数保持不变。白盒测试场景继续单独保留。

环境系统支持 Forward+ 日月直射光、阴影、自研 DDGI、天空镜面反射与 SSR，保留 SSAO。SSIL / SDFGI 由 Environment 独立配置，单独验收 DDGI 时关闭；验证脚本只修改测试实例。建筑、水面和植被通过原生材质路径接收光照；受控城镇 / 植被材质同时贡献 DDGI。探针范围与限制见 [DDGI 接入指南](ddgi_implementation.md)。

通用不透明表面可使用 `town_toon.gdshader`。基础分支为 `diffuse_burley` + `specular_schlick_ggx`，保留 Forward+ 的天空反射、SSR、环境光、阴影、曝光与 AO；`light()` 负责直接光的 Toon/Burley 漫反射及 GGX 高光。白盒地面使用场景内 StandardMaterial3D。通用表面及植被 Shader 保留原路径与 UID，继续供天气和 DDGI 使用。

不使用原生 `diffuse_toon` 开关，因为该分支会按金属度抑制非金属的天空与 SSR 反射。统一 Shader 只对直接漫反射做风格化，不改变这部分反射路径，见 [Forward+ 环境反射源码](https://github.com/godotengine/godot/blob/4.6.1-stable/servers/rendering/renderer_rd/shaders/forward_clustered/scene_forward_clustered.glsl)。

`texture_detail` 越小，底色贴图越简洁；`color_saturation` 控制底色饱和度。`toon_strength` 在 Burley（0）与 Toon（1）直接漫反射间混合；建筑默认 1，铺装和路缘石设为 0，以保留现有效果。`toon_softness` 缩放基础粗糙度决定的 Toon 过渡宽度，默认 1。天气降低高光粗糙度时不收紧此过渡，积水区域逐渐转向 Burley，避免湿润时明暗边界突变。

`surface_roughness`、`surface_metallic`、`specular_strength` 分别对应粗糙度、金属度和镜面参数。非金属镜面默认 0.5，对应正面约 4% 反射。金属使用底色决定反射颜色。直接镜面项实现 Godot 4.6.1 的 GGX 分布、可见性、Schlick Fresnel、光源镜面倍率及多重散射补偿。`ggx_energy_lut` 统一绑定 `assets/textures/rendering/ggx_energy.res`：使用该版本官方积分 Shader 离线生成的 128×128 单通道半浮点 DFG 能量表，运行时仅采样，不执行烘焙或增加绘制通道。公式见 [Forward+ 光照源码](https://github.com/godotengine/godot/blob/4.6.1-stable/servers/rendering/renderer_rd/shaders/scene_forward_lights_inc.glsl) 与 [DFG 积分源码](https://github.com/godotengine/godot/blob/4.6.1-stable/servers/rendering/renderer_rd/shaders/forward_clustered/integrate_dfg.glsl)。当前高光面向各向同性点光近似；原生有限尺寸光源的高光扩展不由自定义 `light()` 接口提供，阴影软度仍由引擎控制。

可选 `use_normal_texture` / `normal_texture` 使用原生切线空间法线重建，`normal_scale` 控制强度。`use_orm_texture` / `orm_texture` 以线性数据读取 R=AO、G=粗糙度、B=金属度，后两通道乘对应材质标量；使用贴图原值时将标量设为 1。AO 默认只影响环境光，`ao_light_affect` 可调整直接光遮蔽。底色贴图使用 sRGB，法线和 ORM 使用线性数据。`uv_scale` / `uv_offset` 共用于这些贴图。当前资源未绑定的 PBR 贴图保持关闭。

| 表面 | surface_roughness | specular_strength | surface_metallic |
| --- | --- | --- | --- |
| 光滑非金属的反射 | 0.03–0.08 | 0.5 | 0 |
| 参考图中的镜面银色金属球 | 0.03–0.10 | 0.5 | 1 |
| 磨砂非金属 | 0.6–0.9 | 0.5 | 0 |

参考图中的划痕需要法线/粗糙度贴图，未提供这些贴图时，三个标量只能控制整体反射。本 Shader 仍是不透明材质：仅调整 specular 与 roughness 无法产生玻璃的透射与折射，`base_color.a` 当前不参与透明度。玻璃的正面反射本来较弱。

Spacelevel 使用现有天空辐照与 SSR，没有新增反射探针。天空材质的 AT_CUBEMAP_PASS 提供环境反射；昼夜组件将原三层云在独立云世界中采样为六个 256×256 的透明线性 HDR 视图，并在辐照通道与大气合成。云贴图、噪声、颜色、旋转和天气灰化来自同一套原材质，暂停时不继续刷新。可见背景保留原网格，正交天空构图也不变。镜面球和湿润路面现在可以反射云；采样仅包含云，按环境刷新频率更新变化，不重绘小镇场景。

SSR 只能补充屏幕内可见几何的反射，屏幕外使用天空反射；增加 Max Steps 无法恢复屏幕外的建筑。

Spacelevel 的天气表面组件为这些材质创建运行时副本，雨雪期间叠加压暗、低粗糙度和水膜高光；石路、桥面及路缘石额外启用局部积水与雨滴波纹。水膜形成时粗糙度以归一化指数响应下降，默认 WetRoughness = 0.14、PuddleRoughness = 0.045、WetSpecular = 0.6；积水区域加强水底石材的压暗，保持俯视时可读的水岸轮廓。默认 specular 为卡通画面增强；物理水的约 2% 正面反射可用 WetSpecular = 0.354。干燥时恢复原粗糙度与底色，退出后恢复原材质绑定，配置见 [天气系统](../../scripts/gamelogic/weather/README.md)。整体仍为卡通/PBR 混合渲染，而非写实 PBR 材质集。

树叶沿用原 alpha 轮廓、风动及树冠中心，以 `normal_roundness=0.68` 平滑叶片法线；顶点色权重降到 0.4，渐变强度 0.6，避免两次染色后暗面发黑。基础材质使用 Alpha Scissor 与 MSAA Alpha To Coverage，保留深度和投影，并平滑细小叶片轮廓；边缘光按屏幕导数平滑裁切。`directional_shadow_lift=0.12` 是日月照明下的风格化树冠透光量，设为 0 可关闭。点光和背向透光保留完整 ATTENUATION，不给范围外叶片补光。

EnvironmentRig → Components → Profile → Cartoon Lighting 可调整：

| 参数 | 默认 | 效果 |
| --- | --- | --- |
| NoonTemperature | 5600 K | 微暖白光，晨昏仍过渡到 3000 K |
| NoonSunTint | (1, 0.99, 0.96) | 减少与材质亮部叠加后的黄色偏色 |
| SunLux | 120000 | 正午直射光亮度 |
| DayExposureEv | 14.35 | 白天相机曝光，越小越亮；不改变光源能量 |
| ShadowAngularDistance | 0 | 关闭 PCSS 大半影，独立于天空太阳盘大小 |
| ShadowFilterBlur | 1.0 | 柔化原生阴影边缘 |
| ShadowBias / ShadowNormalBias | 0.05 / 0.5 | 控制深度/法线偏移；过大会脱离接触面或扭曲细结构 |
| ShadowDistance | 55 m | 阴影覆盖范围，越大越分散贴图精度 |
| ShadowSplit1/2/3 | 0.2 / 0.4 / 0.7 | 级联边界比例，第一段覆盖角色实际拍摄距离；开启分段混合 |
| SSAO | Environment.SsaoEnabled | 直接在 Environment 配置，昼夜组件不接管开关 |
| ColorSaturation | 1.08 | 仅开启 DrivePostEffectsFromProfile 时由 Profile 控制饱和度 |

方向阴影使用 8192 分辨率、32 位深度和 Ultra 过滤，配合 4× MSAA 和树叶覆盖抗锯齿。三维画面按原始视口分辨率渲染。该配置优先保证阴影与轮廓的稳定性，会增加 GPU 和显存开销。编辑器预览和运行时通过同一个 ApplyLighting 方法应用日月、曝光和阴影设置。

日月投影方向逐帧更新，Sun/Moon 关闭物理插值；天空辐照、雾和曝光按 Profile 的 VisualUpdatesPerSecond 刷新。环境配置见 [昼夜系统说明](../../scripts/gamelogic/timeofday/README.md)。

参考：[Godot 灯光与阴影](https://docs.godotengine.org/en/4.6/tutorials/3d/lights_and_shadows.html)、[空间 Shader](https://docs.godotengine.org/en/4.6/tutorials/shaders/shader_reference/spatial_shader.html)。

## 直接编辑 Environment

WorldEnvironment → Environment 中可直接调整雾、SSAO、色调映射和 Adjustment。Profile → Scene Environment Controls 的 DriveFogFromProfile / DrivePostEffectsFromProfile 默认关闭，避免系统持续覆盖 Inspector。

当前 Spacelevel 使用物理光照强度驱动昼夜，日月倍率为 1，间接能量为 0.75。相机曝光由昼夜 EV 曲线控制，不随镜头中的亮暗变化，曝光不改变光源。灯罩材质使用 300 cd/m² 自发光，除自身发光与泛光外，也通过 DDGI 向周围贡献低频间接光。`DayExposureEv` / `NightExposureEv` 控制日夜曝光，关闭 `DriveExposureFromProfile` 后可手动编辑 Camera Attributes。天空强度由太阳高度驱动；当前夜间 EV 为 2，`NightSkyIntensity = 8`。关闭 `DriveSkyBrightnessFromProfile` 后可手动编辑 Background Intensity。运行时修改 Profile 后调用环境组件 `RefreshProfile()`。
