# 天气系统

环境与日月节点只在 `DayNightEnvironmentComponent3D` 配置路径；天气组件复用已解析的节点。地表组件不另设 Profile，统一复用天气 Profile。各导出字段有中文注释，标明单位与可选驱动的生效条件。

`WeatherComponent3D` 是 `EnvironmentRig` 上的 EC 组件，已接入 ForestTownLevel 和独立昼夜环境模板。晴天、阴天、雨天、雪天共用现有世界时钟，叠加阴云与跟随角色的 GPU 降水。直射光衰减和雨雪雾参数均为独立的可选驱动，默认不覆盖场景设置。

## 使用

运行 ForestTownLevel，按 **F8 → 天气**。默认开启自动天气；手动选择会关闭自动调度，再打开开关即可恢复。雨雪强度可实时调整。暂停时手动选择立即生效。「湿润程度」「地面积水」「地面积雪」可直接预览表面效果；「涟漪强度」调节水面扰动。推进游戏时间后继续自然变化。

`EnvironmentRig → Components → WeatherComponent3D → Profile` 提供配置；默认资源为 `res://assets/resources/weather/default_weather.tres`：

- `PeriodHours = 4`：每四个游戏小时重新抽取，可连续抽到相同天气。
- `TransitionHours = 0.08`：过渡约 4.8 游戏分钟，在默认 20 分钟一天时约 4 秒。
- 四种权重默认 `45 / 25 / 25 / 5`，把雪的权重设为 0 可关闭自动降雪；全为 0 时使用晴天。
- `Seed`：相同种子、配置和绝对游戏时间产生相同天气；暂停、快进、回退、读档和切换关卡不会消耗额外随机数。
- `Atmosphere`：各天气阴云量、直射光/天空衰减、降水雾增量与视距比例。
- `Precipitation`：风向、雨雪强度与发射高度。粒子数量、范围和寿命在 `assets/scenes/environment/precipitation.tscn` 中调整。

运行时组件复制 Profile，雨雪的处理材质、绘制网格和材质均 Local To Scene；GM 调整不会改写默认配置或其他环境实例。

```csharp
var weather = environmentRig.GetComponent<GameLogic.WeatherComponent3D>();
weather.SetWeather((int)GameLogic.WeatherKind.Rain);       // 平滑切换并手动覆盖
weather.SetWeather((int)GameLogic.WeatherKind.Clear, true); // 立即切换
weather.SetAutomatic(true);                               // 回到当前日历天气
var state = weather.GetWeatherState();
```

GDScript / Graph 可通过宿主 `weather` 元数据调用 `SetWeather`、`SetAutomatic`、`GetWeatherState`。状态包含目标类型、过渡进度、阴云/雨/雪混合量和下次抽取倒计时。

## 时钟与环境

自动天气根据绝对游戏小时直接取样，在每段开始时从上一段天气平滑过渡。主动快进直接取目标时刻，回退结果可重现。手动过渡也使用游戏小时；暂停停止过渡与粒子模拟。粒子模拟倍率上限为 8，避免高倍快进导致 GPU 开销骤增。

天气组件的 DriveFogFromWeather / DriveLightsFromWeather 默认关闭，保留 Environment 雾参数和日月照度。只有显式开启对应开关后，才通过 EnvironmentApplying / EnvironmentApplied 恢复并叠加雾或灯光数值；雾的开关和模式始终由 Environment 决定。阴云、雨雪粒子和天气表面效果独立更新。回收时只恢复主动接管过的参数。

自动天气由已存档的世界时间重建。手动覆盖、运行时强度调整以及表面含水量、积雪量仅在当前环境生命周期有效，尚未加入存档。当前降水与积雪是视觉效果，尚无室内遮挡、雨滴粒子碰撞、可踩踏雪层、雷电或音效。

## 湿润与积水

ForestTownLevel 的 EnvironmentRig 还挂载 `WeatherSurfaceComponent3D`，`SurfaceRootPath = ../RPGTown`。它复制作用范围内的材质并绑定原网格，退出/回收时恢复原绑定；贴图、树叶风动与边缘光仍使用原资源。河流、透明玻璃、天空、人物纸片不替换材质。

雨雪逐渐加深建筑、路面、道具和植物的底色、降低粗糙度并增加水膜反射。雪天默认按雨天 55% 的程度湿润，并独立积累雪量；雪覆盖的区域转为柔和的粗糙雪面，压制下面的水膜反射。雨天另在启用 `weather_puddles_enabled` 的材质上积水。当前标记铺装石材 `Town_Structures_0.tres` 和路缘石 `Town_Structures_2.tres`，覆盖道路、桥面及路缘石，并根据朝上法线限制积水，墙面和树叶不会变成水塘。建筑与这两个地面材质统一使用 `town_toon.gdshader`；铺装、路缘石通过 `toon_strength = 0` 保留 Burley 漫反射，建筑默认 1。直接光的 Toon 明暗与 PBR 反射分开处理，非金属的天空和 SSR 反射保持有效。

积水掩码使用世界坐标噪声，覆盖范围随含水量增长，保留砖石贴图、局部压平法线，低粗糙度水膜接受现有天空反射和 SSR。两层噪声先展开集中在中间的分布，让较小积水量也能命中洼地，覆盖率为零时完全关闭积水。积水区域额外压暗水底石材，使俯视时的水岸轮廓可辨。噪声使用整数散列，确保相邻格子共享完全相同的边界值，避免 sin/fract 浮点误差造成矩形切边；五阶插值与屏幕导数平滑水岸，维持连续边缘。雨滴环形波纹使用游戏时间，暂停时保持不动。涟漪随机改变落点、扩散并衰减，同时采样相邻格子的波环，避免格子截断；按屏幕像素足迹滤除远处亚像素波形，避免远处的低粗糙度高光放大成噪点。雨滴改为 0.30 米短滴和柔化轮廓，减少原先长亮线的感觉；`RainOpacity = 0.42` 控制透明度，可在 F8 天气页「雨滴可见度」实时调整，雨雪强度仍独立控制降水量。

Profile 的 `Wet Surfaces (Game Hours)` 可以调整：

- `WettingHours = 0.15`：湿润响应时间，在默认日长下约 7.5 秒。
- `PuddleBuildHours = 0.6`：积水形成比表面湿润慢，约 30 秒。
- `DryingHours = 1.5` / `PuddleDryingHours = 3`：停雨后表面约 75 秒、积水约 150 秒的衰减时间。
- 这些值是指数变化的时间常数，经过一个时间常数变化约 63%；强度越小，最终湿润和积水越少。上述秒数按时钟默认 1200 秒一天换算；当前 ForestTownLevel 的 `DayLengthSeconds = 4800`，对应时间为四倍，积水时间常数为 120 秒。暂停时通过 GM「地面积水」直接预览。
- `WetDarkening`、`PuddleCoverage`、`PuddleScale`：湿润压暗量、局部积水覆盖率和形状大小。
- `WetRoughness = 0.14` / `PuddleRoughness = 0.045`：建筑、路面及标准 PBR 表面的水膜/积水目标粗糙度。水膜使用归一化指数响应；含水量 0.8 时，原粗糙度为 1 的表面降至约 0.16，而非旧算法的 0.42。植物保留较柔和的原有湿润高光。
- `WetSpecular = 0.6`：上述水膜的非金属反射强度，沿用 Godot specular 语义；0.354 约对应物理水的正面 2% 反射，默认增强到约 5.8% 以适配卡通路面。积水的直接高光由统一 Shader 计算 Fresnel，天空/SSR 的 Fresnel 由原生 PBR 计算；干燥时恢复原材质的 specular。
- `PuddleCoverage` 默认 0.62，可增加局部积水面积；不改变水膜的反射来源。
- `SnowWetness`：雪天湿润比例；`WetSurfacesEnabled`：开关。

`Rain Ripples` 的 `RainRipplesEnabled`、`RippleStrength = 0.14`、`RippleScale = 1.7` 和 `RippleSpeed = 1.35` 分别控制涟漪开关、法线扰动、世界尺度和扩散速度。雨量同时控制落点密度和扰动幅度，晴天没有涟漪。

`Snow Cover (Game Hours)` 的 `SnowCoverEnabled`、`SnowBuildHours = 0.45`、`SnowMeltHours = 3` 控制积雪开关及堆积/融化的时间常数；默认约 22.5 秒堆积、150 秒融化。停雪后积雪保留并逐渐融化，雨天加速融化并留下湿润。`SnowColor` 和 `SnowRoughness = 0.86` 控制雪面。世界坐标噪声让雪从零星区域逐渐连成覆盖；朝上表面更厚、坡面更薄，垂直墙面及底面不积雪。现有小镇、树叶和花草 Shader 共用雪层采样，标准不透明 PBR 材质追加独立雪层，同时保留原贴图和 NextPass；无雪时关闭附加绘制。

含水量使用正向游戏时间积分，暂停不累积、不蒸发。时间回退保留当前含水量，之后向前推进继续演化；没有重建过去每次降水的地表历史。材质积水是视觉水膜，不改变碰撞、流体高度或角色移动。

```csharp
var surfaces = environmentRig.GetComponent<GameLogic.WeatherSurfaceComponent3D>();
surfaces.SetSurfaceState(0.8f, 0.9f); // 即使时钟暂停也能预览
surfaces.SetSnowState(0.85f);        // 独立预览积雪量
```

### 当前场景是否使用 PBR

场景使用混合渲染。基础 Ground、部分导入表面使用 `StandardMaterial3D`；小镇建筑、道具、铺装及路缘石统一使用 `town_toon.gdshader`，树叶、花草使用专用卡通 Shader。统一 Shader 的 `light()` 处理 Toon/Burley 直接漫反射及 GGX 高光，天空反射和 SSR 使用原生 PBR 分支；支持可选法线与 ORM 贴图。树叶与花草仍使用自定义卡通受光。当前铺装资源以底色贴图为主，未绑定完整的法线与 ORM 贴图组。

统一 Shader 通过 `toon_strength` 与 `toon_softness` 控制直接光的 Toon 明暗，雨雪与积水在同一材质中处理。直接高光保留 GGX 与多重散射能量补偿，材质共用离线 DFG 能量表，不新增绘制通道。人物仍是 AnimatedSprite3D 纸片，河流与天空保留专用 Shader。SSR 只能反射屏幕内的信息，无法保证屏幕外物体的完整倒影。原三层网格云已由昼夜组件采样并合入天空辐照，倒影与原云共用时间轴和天气颜色，详见 [材质和反射配置](../../../docs/art/town_rendering.md) 与 [Godot 环境与屏幕空间反射](https://docs.godotengine.org/en/4.6/tutorials/3d/environment_and_post_processing.html#screen-space-reflections-ssr)。

## 验证

检查晴、阴、雨、雪切换，暂停与时间推进，湿润和积雪材质、云反射、回收及退出释放。临时脚本、截图与日志放在忽略目录 `.godot/`。
