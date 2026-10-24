# 昼夜与月相

昼夜组件持续更新世界时钟、日月方向、天空配色与云动画。原生 Environment、相机和灯光参数默认由场景资源控制；SSAO、SSIL、SDFGI、SSR、Glow、Adjustment、雾和自动曝光开关均可独立编辑。

`TimeOfDayModule` 管理全局 `WorldClock`，`DayNightEnvironmentComponent3D` 按 EC 生命周期更新环境。ForestTownLevel 引用 `relink_environment.tres` / `day_night_camera_attributes.tres` 和独立 Relink Compositor；模板位于 `res://assets/scenes/environment/day_night_environment.tscn`。这些资源的原生效果设置不会因组件刷新或 Relink 开关而自动改写。

默认 20 分钟一个游戏日、初始满月、29.53059 天一个月相周期。模板及当前关卡默认 12:00，可通过 Profile 的 StartHour 覆盖。时间受游戏速度影响，游戏暂停、时钟暂停或没有环境宿主时停止推进。返回菜单和切换关卡保留日历。旧存档没有时间数据时使用默认值。

编辑器预览逐帧调用 Profile；TonemapMode、FogMode 和 AutoExposureEnabled 只在值改变时写入。原生 setter 对相同值也会发送检查器属性列表更新，重复写入会造成 Environment 选项闪烁。固定时间连续预览 10 帧的验证中，修复后环境和相机的属性列表更新通知均为 0。

`main` 使用官方 Godot 4.8。日月的 LightEnergy、LightIndirectEnergy 和精灵的 GI Mode 保留场景配置，SSIL / SDFGI 不再强制关闭。RTX 版本保存在 `codex/rtx-version`。云纹理仍只通过 RenderingServer 绑定，避免临时 Texture2DRD 写入可序列化资源。

`scripts/gamelogic/rendering/cloud_radiance_copy.gd` 负责渲染线程的云纹理复制，`relink_gpu_cleanup.gd` 负责 Relink 后处理 GPU 资源清理。两者由 C# 渲染类直接加载；GDScript 回调不随 C# 程序集热重载失效，因此这些辅助脚本需要与 C# 调用方一起保留。

## 配置

在 `ForestTownLevel` 中选中 `EnvironmentRig → Components → DayNightEnvironmentComponent3D → Profile`，直接编辑内嵌配置。独立模板使用 `res://assets/resources/environment/default_time_of_day.tres`，不影响当前场景的内嵌配置。

- Scene Environment Controls：所有 Drive...FromProfile 开关默认关闭。DriveFogFromProfile 只驱动雾的数值；DrivePostEffectsFromProfile 驱动原生饱和度、对比度与泛光数值；DriveRelinkBloomFromProfile 单独驱动 Relink 泛光；DriveSkyBrightnessFromProfile 驱动背景强度；DriveExposureFromProfile 驱动物理曝光参数；DriveLightSettingsFromProfile 驱动日月照度、颜色和阴影；DriveSkyProjectionFromProfile 驱动天空 FOV/旋转。原生效果开关、Fog Mode、Tonemap、AutoExposure 和灯光能量倍率始终独立。开关默认关闭；当前 ForestTownLevel 已显式启用这些驱动，F8 手动调节 Relink 泛光后仅退出 Relink 泛光驱动。
- Calendar Defaults：新会话的日长、初始日期/时刻、朔望月长度、初始月相和暂停状态。已有存档优先。
- Sun and Moon：日月方向随时钟更新；照度、颜色、可见性和阴影参数仅在 DriveLightSettingsFromProfile 开启时应用。天空天体盘大小仍由 SunDiameterDegrees / MoonDiameterDegrees 控制。
- Sky：日夜天空、地平线与晚霞颜色、天空强度、星光。
- Day and Night Post Effects：DrivePostEffectsFromProfile 开启后可选按昼夜插值饱和度、对比度和泛光强度；原生 Glow / Adjustment 的开关仍由 Environment 决定，Relink 与原生后处理可以同时启用。
- Camera Exposure (Optional Driver) / Update Quality：日夜 EV 和曝光补偿仅在 DriveExposureFromProfile 开启后应用，AutoExposure 不会被关闭。VisualUpdatesPerSecond 控制环境数值和云辐照的刷新频率，日月方向与云动画继续随世界时钟更新。
- Cartoon Lighting：阴影角度、过滤、偏移、距离与级联参数仅在 DriveLightSettingsFromProfile 开启后应用；默认直接编辑日月节点。组件不再强制将 LightEnergy 设为 1、LightIndirectEnergy 设为 0 或关闭精灵 GI。精灵是否受光直接编辑各自的 Shaded 属性。
- Atmosphere and Aerial Perspective：天空散射参数仍驱动自定义天空材质。DriveFogFromProfile 开启时可驱动雾的密度、距离、曲线和高度数值，但 FogEnabled、FogMode、FogAerialPerspective、FogSkyAffect、FogSunScatter 始终保留 Environment 设置。已删除不再生效的 Profile.FogEnabled 字段。

Profile 保留原有关卡的雾、曝光、灯光与泛光参考值；只有显式开启对应驱动后才应用数值。Relink 与原生后处理完全独立，切换 Relink 不再关闭原生 Glow / Adjustment 或改写 Tonemap / Exposure。

日月方向和天空动画遵循世界时钟；灯光能量倍率、间接光倍率及相机曝光互不绑定，默认直接编辑场景中的灯光与 CameraAttributes。

DriveExposureFromProfile 默认关闭，保留手动 CameraAttributes；原生 AutoExposure 开关始终独立。灯罩材质和自发光仍由对应资源控制。

显式开启 DriveSkyBrightnessFromProfile 后，天空亮度才随太阳高度平滑插值；显式开启 DriveExposureFromProfile 后才使用日夜 EV 曲线。默认两者都由原生资源手动控制。

MoonTint 控制月光的美术染色；MoonHaloStrength 控制背景中的蓝紫月晕，随月相和月亮高度衰减，不参与环境辐照。编辑器与运行时环境组件共用 `ApplySky`，夜空配色、月相和星光也随编辑器的 StartHour / StartLunarPhase 更新。以上昼夜插值开关默认关闭，其他模板沿用原配置。

已移除 `addons/scene_sky_preview` 插件。编辑器预览由 `[Tool] GameObject3D` 分发 `Component3D.OnEditorUpdate`，`DayNightEnvironmentComponent3D` 使用配置的 StartHour / StartLunarPhase 同步天空、日月、云和后处理；不初始化运行时模块、不生成角色行为、不写存档或推进世界时钟。`PreviewInEditor` 可关闭自动预览。只有当前编辑场景参与预览，组件临时绑定编辑器相机的环境和 Compositor，保留编辑器相机自身的 CameraAttributes；切换场景、退出或关闭自动预览时恢复原有相机覆盖，不改变编辑器视角和 FOV。

环境实例的 `Components → DayNightEnvironmentComponent3D` 提供节点路径、Profile 和编辑器预览开关。天气组件复用它解析的环境与日月节点，不再保存第二套路径。精灵受光在各自节点的 Shaded 属性中配置。角色仍保留原生受光和原有 Alpha 抗锯齿，未改为自发光或 Unshaded。新会话从第一个进入的环境 Profile 初始化日历；之后切换场景只替换视觉风格，既有日历或已加载存档优先。环境、天空、ShaderMaterial、曝光均勾选 Local To Scene；昼夜 Profile 在运行时另行复制，手动调节不会改写场景定义。

编辑器保存的环境为正午预览；动态循环在运行时由 EC 组件驱动。在 Remote 场景树中可以检查日月、WorldEnvironment 和宿主的 `time_of_day` 元数据。运行时改 Profile 颜色、曝光补偿或天体大小后，调用组件 `RefreshProfile()` 即可刷新，不会重置日历。

## 调用

### 运行时 GM

进入 `ForestTownLevel` 后按 **F8** 或右上角 **GM** 打开面板，选择「时间与天空」。拖动时间滑条会立即更新太阳、月亮和天空；曝光仅在显式开启驱动时变化；拖动期间暂时停表，松开后恢复原来的播放状态。也可输入日期、小时和分钟（回车提交），一键跳到黎明、正午、黄昏或午夜，独立暂停/调整时钟倍率，选择八种月相或拖动月相滑条。

面板打开时暂时关闭项目输入层，避免点击控件触发角色攻击或移动镜头；收起或退出场景后恢复之前的输入状态。世界模拟不会因此暂停。修改的是当前日历，后续正常存档会包含修改；面板本身不主动写盘。即使游戏暂停也可操作面板。

`ForestTownLevel/RuntimeGm` 实例化 `res://assets/scenes/ui/runtime_gm.tscn`，所有 Control、样式和 F8 Shortcut 都已序列化。`RuntimeGmComponent3D` 负责面板开关和输入隔离；`TimeOfDayGmComponent3D` 以 Embedded 模式绑定时间页，`EnvironmentRigPath` 指向旁边的环境宿主。面板默认展开由外壳的 `StartOpen` 控制，其他页面见 [Runtime GM](../gm/README.md)。

### 代码

```csharp
var time = Framework.ModuleSystem.GetModule<GameLogic.ITimeOfDayModule>();
time.Clock.SetDate(8, 18.5);       // 第 8 天 18:30，日期从 0 开始
time.Clock.Paused = true;
time.Clock.Speed = 10;
time.Clock.AdvanceHours(8);       // 睡眠、等待或时间跳转
time.Clock.SetLunarPhase(0.25);   // 0 新月、0.25 上弦、0.5 满月、0.75 下弦
var now = time.Clock.State;
time.Clock.DayChanged += day => { /* 每次跳转只通知最终日期 */ };
time.Clock.Changed += state => { /* 每个游戏分钟或主动调整时通知 */ };
time.Clock.MoonPhaseChanged += phase => { /* 八种月相 */ };
```

记得在调用者销毁时退订事件。环境 EC 组件公开 `SetTimeOfDay`、`SetDate`、`AdvanceHours`、`SetLunarPhase`、`SetPaused`、`SetSpeed`、`GetTimeState`，可接入现有 Graph 组件动作。

GDScript 调试示例（运行中的关卡）：

```gdscript
var controller = $EnvironmentRig.get_meta("time_of_day")
controller.SetPaused(true)
controller.SetTimeOfDay(0.0)
controller.SetLunarPhase(0.5)
```

全局时间自动写入既有存档的 `sections.world.time_of_day`，保存总小时、月相偏移、日长、月长、速度与暂停状态。恢复先验证完整数据再提交。快进使用绝对累计时间，跨多天不会丢失月相。

调用 SetDate / SetTimeOfDay / AdvanceHours 会立即更新日月方向、天空和云动画；其他原生设置仅在显式开启对应驱动后更新。

ForestTownLevel 的 Profile 使用白天 EV 14.35、夜间 EV 2 和夜间天空强度 8。是否更新相机与背景亮度，由各自独立驱动开关决定。

## 原作者纸片云

云也接入天空环境反射。`DayNightEnvironmentComponent3D` 管理 `SkyCloudRadiance`，在独立世界中复制原三层网格和材质，以六个 256×256 透明 HDR 视口采样完整方向；不包含关卡几何、灯光或后处理。`cloud_radiance_copy.gd` 在绘制后把结果复制到独立 GPU 纹理，天空只接收没有节点路径的 Texture2DRD，避免复制场景时解析临时 Face0..5 节点。采样颜色为线性预乘 alpha，Sky Shader 仅在 AT_CUBEMAP_PASS 与大气辐照合成，因此可见背景仍直接绘制原云网格。天空亮度和原有投影旋转继续应用，天空辐照始终保留。云转动、SDF、噪声、颜色及天气改变时按 VisualUpdatesPerSecond 刷新；暂停且配置不变时保留采样。关闭 CloudsEnabled 同时关闭云倒影，编辑器退出与组件回收先解绑纹理，再在渲染线程释放 GPU 副本，延迟释放采样视口。内部采样节点不序列化到场景。

当前云直接使用[原仓库](https://github.com/xinyangaa/Unity_URP_Genshin_Impact_Programmed_Skybox/tree/ef1bd4c6fa486977a323c3badaba5740feae1d55)提供的 cloud.fbx、原始 UV2、RGBA 云图和噪声文件。`EnvironmentRig/OriginalClouds` 下的 BaseClouds、HighClouds、SecondaryClouds 都是场景内的真实 MeshInstance3D，各自材质和网格均已序列化，不在运行时创建。

`EnvironmentRig → Components → DayNightEnvironmentComponent3D → Profile → Original Cloud Timeline` 可控制启用状态、CloudEvolutionPeriodHours（云形周期，默认 1.6 游戏小时）、CloudRotationPeriodHours（整体旋转周期，默认 1.6 游戏小时）、CloudNoisePeriodHours（噪声周期，默认 8 游戏小时）及三层云各自的 Timeline。CloudRotationTimeline 保留原作者旋转曲线，其中只启用 GroupYaw 驱动共同父节点；上层和第二层通过继承同步移动，不再叠加额外自转。周期数值越大，旋转越慢。展开其 Curves 可直接编辑原作者的 22 条材质曲线，包括 SDF、四种明暗颜色、亮边颜色和日月切换；未额外柔化、改写首尾关键帧。材质平铺应用两次，与源 Shader 相同；噪声相位也从累计游戏时间取样。

旋转、云形、噪声和颜色统一由 WorldClock.TotalHours 计算。默认 20 分钟一天时，1.6 游戏小时仍相当于原 80 秒节奏；改变日长或 GM 倍率会同步改变云速，同一游戏日期／时刻的云位置不会因日长变化而跳变。F8 时间 GM 的暂停、快进和回退同步控制云。编辑器与运行时共用 Profile.SampleCloudTime：编辑 StartDay / StartHour 即可预览对应时刻，编辑器不再用独立秒表推动云。运行时逐帧更新共同转动和各层 SDF／噪声，全部遵循世界时钟。现有日月轨道、月相、相机跟随和正交／透视切换保持接入。

原仓库缺少示例场景引用的高空及第二层云网格（GUID `4e842a0dab424954fbbe40e4d81b5a3d`），其噪声材质 GUID 也无法对应到上传文件。现已离线补建高层和第二层网格，接上原作者第二套图集、各自 22 条材质曲线及旋转曲线；底层云也随整体风向转动。新增网格为补建布局，底层保留原始 FBX。补建云片的竖直方向与环半径从底层网格提取，去掉朝球心倾斜和随机滚转，使高低层的透视缩短方式一致；三层共用同一相机投影和转动角度。三层噪声均使用同仓库 Noise_091；这不是文章 GIF 的逐项原样复刻。原始文件校验、资源出处和必要引擎适配见 `docs/art/sky_clouds.md`。

## 场景相机与投影

`ForestTownLevel/CameraRig/Camera3D` 是真实序列化的场景相机。直接在 Inspector 调整 Transform、Projection、FOV、Size、Near/Far，并用 Camera3D 的 Preview 检查构图。运行时不创建全局相机，也不覆盖投影、FOV 或初始构图。

`CameraRig → Components → SceneCameraComponent3D` 是唯一的相机行为组件：`TargetPath = ../Player`，`CameraPath = Camera3D`。它在目标准备完成后自动绑定，处理跟随、前瞻、平滑和震动。原来的 LevelCamera / GlobalCamera 两层组件已经合并移除。其 `Profile` 只保留跟随参数；构图以 Camera3D 为准。CameraModule 只路由 Follow、Focus、Shake 等调用，不创建或销毁相机节点。

天空投影默认保留 Environment 的 SkyCustomFov / SkyRotation。显式开启 DriveSkyProjectionFromProfile 后，正交相机才使用 Profile 的 OrthographicSkyFov / OrthographicSkyPitchDegrees，透视相机恢复真实投影；云材质读取实际生效的天空投影参数。

环境组件的 Tool 预览让本地相机切换和天空/日月尺寸参数的修改直接在编辑器中生效，无需额外天空插件。编辑器自由视角仍用于浏览，最终构图请看场景 Camera3D Preview。修改 Compositor 中共享 Profile 的 `LutStrength` 可直接看到调色变化；MSAA 下的结果会写回多采样颜色缓冲，不被透明后的再次解析覆盖。

## 模型边界

这是可配置的游戏天球模型，不是现实天文历：日出 06:00、日落 18:00、太阳正午达到设定高度；月球轨道按月相相对太阳偏移，因此上弦月傍晚高悬、满月午夜高悬。月盘通过球面朝向和日照方向计算连续明暗，月光按可见照明比例衰减。尚不模拟季节、经纬度或日食月食；[天气系统](../weather/README.md)通过同宿主组件叠加晴、阴、雨、雪与自动调度。

日月、天空背景、相机曝光与 Relink 后处理独立配置。世界时钟继续驱动日月方向、月相、星光和云动画，暂停时保持对应时间状态。

参考：[Godot 物理光照与相机单位](https://docs.godotengine.org/en/4.6/tutorials/3d/physical_light_and_camera_units.html)、[天空 Shader](https://docs.godotengine.org/en/4.6/tutorials/shaders/shader_reference/sky_shader.html)。

## 验证

```powershell
dotnet build yu.csproj
.\scripts\tools\engine\launch_godot.ps1 -RunGame -GodotArguments @('res://assets/scenes/levels/forest_town_level.tscn')
```

检查跨日、暂停、速度、月相环绕、非法输入原子恢复、日月位置、存档往返、实例资源隔离、GM、烘焙云资源、相机自动绑定和跟随、保留编辑器构图、正交/透视天空切换以及退出释放。

验收覆盖跨昼夜与 Relink 切换后手动参数不变、各原生效果同时开/关、日月方向继续更新、雾模式与自动曝光保持手动设置，以及退出释放。可直接运行白盒关卡并通过 GM 调整参数，检查这些行为。

临时验证脚本、截图和性能数据放在忽略目录 `.godot/`，不作为游戏源码或发布资源保留。

## EnvironmentRig 参数整理

当前 EnvironmentRig 有四个 EC 组件：昼夜管理世界时钟与天球，天气管理天气与降水，地表管理含水 / 积雪，DDGI 捕获场景并更新探针。前三者先更新，DDGI 最后捕获；运行时接线见 [DDGI 指南](../../../docs/art/ddgi_implementation.md)。日月间接能量为 0.75，SSIL 等开关由 Environment 独立配置，昼夜组件不额外接管。加速或手动调时导致全局光照快速变化时，DDGI 临时整轮刷新探针，避免旧的白天辐照度被夜间曝光放大；相机曝光曲线与日夜端点保持原配置。

| 整理项 | 处理与原因 |
| --- | --- |
| Profile.FogEnabled | 删除；雾开关只有 Environment.FogEnabled 一处有效配置。 |
| Relink.NativeTonemap / NativeExposure / NativeGlow / NativeAdjustment | 删除四个未使用的参考字段；原生参数直接编辑 Environment。 |
| LightSceneSprites | 删除全场景精灵受光覆盖；每个 SpriteBase3D 自己配置 Shaded，玩家场景已开启。 |
| Weather.EnvironmentPath / SunPath / MoonPath | 删除重复路径，复用同宿主昼夜组件已经解析的节点。 |
| 原生后处理与 Relink 泛光驱动 | 拆为 DrivePostEffectsFromProfile 和 DriveRelinkBloomFromProfile，互不接管对方。 |
| 泛光昼夜端点 | 保留一组端点，原生 Glow 与 Relink 可分别选择共用；取样和范围限制合并到同一函数。 |
| WeatherSurface.Profile | 不新增重复配置；湿润、积水、涟漪和积雪统一使用 WeatherComponent3D.Profile。 |
| 三层云路径、材质时间线及旋转/形变/噪声周期 | 保留；分别控制不同网格、曲线和动画相位，不能用一个值替代。 |
| StartDay / StartHour / StartLunarPhase 等初始日历值 | 保留；初始化新世界和编辑器预览使用，已加载的世界时钟/存档优先。 |
| BlendNightFog / BlendNightPostEffects | 保留；决定是否插值夜间端点，与是否接管对应效果是不同选择。 |

所有保留的导出参数补充中文 XML 注释，说明用途、单位及生效条件。曝光与刷新质量分组，后处理饱和度归到后处理组。驱动开关默认关闭；当前关卡已显式启用的配置继续保留。

Relink 运行时调节见 [Runtime GM](../gm/README.md)。直接通过远程 Inspector 或代码修改 Profile 参数同样逐帧生效；启用昼夜泛光时泛光参数会由曲线更新，手动编辑资源前可关闭该独立驱动。
