# 昼夜与月相

当前 Spacelevel 使用 Godot Forward+ 的日月直射光、阴影和天空环境光；不使用自研 GI、SDFGI 或 SSIL。材质保留原生受光、SSR、天气表面和后处理。

`TimeOfDayModule` 管理一个全局 `WorldClock`，`DayNightEnvironmentComponent3D` 按 EC 生命周期驱动场景环境。当前 `spacelevel.tscn` 保存原生环境和日月节点，Environment / CameraAttributes 引用 `relink_environment.tres` / `day_night_camera_attributes.tres`，内嵌昼夜 Profile。另提供 `res://assets/scenes/environment/day_night_environment.tscn` 作为其他关卡模板；同一个 World3D 使用一套环境。当前关卡在 Godot 原生渲染上接入[Relink HDR LUT、电影曲线与泛光合成器](../../../docs/art/relink_post.md)，启用 SSR，关闭 SSAO、SDFGI 和 SSIL。夜间主要使用天空环境光与月光。独立模板沿用原生后处理，仍默认关闭 SSR、SSIL 和 SDFGI。

默认 20 分钟一个游戏日、初始满月、29.53059 天一个月相周期。模板及当前关卡默认 12:00，可通过 Profile 的 StartHour 覆盖。时间受游戏速度影响，游戏暂停、时钟暂停或没有环境宿主时停止推进。返回菜单和切换关卡保留日历。旧存档没有时间数据时使用默认值。

编辑器预览逐帧调用 Profile；TonemapMode、FogMode 和 AutoExposureEnabled 只在值改变时写入。原生 setter 对相同值也会发送检查器属性列表更新，重复写入会造成 Environment 选项闪烁。固定时间连续预览 10 帧的验证中，修复后环境和相机的属性列表更新通知均为 0。

NVIDIA 引擎开启 `Environment.PathtracingEnabled` 时，日月的间接光强度为 1，切回光栅恢复为 0。运行时即使时钟暂停，也会响应光追开关变化。云辐照的临时 Texture2DRD 只通过 RenderingServer 绑定；ShaderMaterial 的可序列化参数保持禁用、空纹理，避免保存 Environment 后重新加载出紫色天空。初始化会清除旧资源中遗留的临时纹理绑定。

## 配置

在 `spacelevel` 中选中 `EnvironmentRig → Components → DayNightEnvironmentComponent3D → Profile`，直接编辑内嵌配置。独立模板使用 `res://assets/resources/environment/default_time_of_day.tres`，不影响当前场景的内嵌配置。

- Scene Environment Controls：默认直接编辑 WorldEnvironment 的雾、SSAO 和 Adjustment；DriveFogFromProfile / DrivePostEffectsFromProfile 开启后才使用 Profile 对应值。天空亮度和曝光默认随昼夜变化，关闭 DriveSkyBrightnessFromProfile / DriveExposureFromProfile 后可在原生 Environment / Camera Attributes 手动调整。
- Calendar Defaults：新会话的日长、初始日期/时刻、朔望月长度、初始月相和暂停状态。已有存档优先。
- Sun and Moon：正午高度、方位角、直射照度、日月视直径。太阳默认 1.8 度、月亮默认 3 度；两者独立可调，设为约 0.52 度可接近真实视直径。编辑器预览同步更新。
- Sky：日夜天空、地平线与晚霞颜色、天空强度、星光。
- Day and Night Post Effects：开启 DrivePostEffectsFromProfile 和 BlendNightPostEffects 后，ColorSaturation / NightColorSaturation、DayContrast / NightContrast、DayGlowIntensity / NightGlowIntensity、DayGlowThreshold / NightGlowThreshold 按日照权重平滑插值。NightBloom 只在夜间渐入，GlowEnabled 可关闭泛光。其余泛光层级、色调映射仍直接在 Environment 编辑。当前关卡由天空提供环境光，夜间补光通过 NightSkyIntensity（模板 0.16，Spacelevel 3.2）调整；纯天空环境光下 Ambient Light Energy 不影响天空补光。
- Exposure and Quality：日夜 EV、曝光补偿和环境更新频率，默认 10 Hz；该频率仅限制天空辐照、雾和曝光，日月投影方向逐帧更新，暂停时保持不变。Sun/Moon 关闭物理插值，避免渲染帧更新方向与物理帧插值冲突。
- Cartoon Lighting：ShadowSplit1/2/3 控制四级联边界，默认 0.2/0.4/0.7；ShadowBias/ShadowNormalBias 默认 0.05/0.5，ShadowFilterBlur 默认 1.0。开启级联混合，日月、编辑器预览与运行时共用 Profile 的配置。方向阴影使用 8192 分辨率、32 位深度和 Ultra 过滤；画面按视口原始分辨率渲染，启用 4× MSAA 与 TAA，抑制日月移动及镜头移动时的边缘闪烁。该配置增加 GPU 和显存开销，TAA 会轻微柔化画面。
- Atmosphere and Aerial Perspective：HorizonHaze / HorizonSoftness 控制地平线薄雾，MieStrength / MieAnisotropy 控制太阳方向的散射近似。DriveFogFromProfile 开启时，FogStart / FogEnd / FogCurve / FogOpacity 控制距离雾，FogHeight / FogHeightDensity 控制高度雾；当前关卡已开启该开关。CloudHorizonFade 只控制云的地平线渐隐。运行时与编辑器预览共用 `TimeOfDayProfile.ApplyAtmosphere`。

当前关卡参考 Relink 的清透色彩与柔和高光：白天使用 5900K 微暖白光、淡蓝天空，距离雾从 35 米开始，远端 240 米、强度 0.38；夜晚使用蓝紫天空与冷月光，距离雾从 14 米开始，远端 150 米、强度 0.7。开启 BlendNightFog 后，NightFogStart / NightFogEnd / NightFogOpacity / NightFogHeightDensity 与原白天参数平滑插值。当前关卡的白天和夜间 FogHeightDensity 均为 0，保留距离雾，避免以固定世界高度在角色和建筑上叠加水平雾层。日月投影保持开启，SSAO 默认关闭。白天泛光强度 0.04、阈值 1.8，夜晚强度 0.26、阈值 0.9，并叠加 0.025 的低强度柔光。这些参数由 ApplyRelinkPostProcessing 同步到合成器；开启时停止原生 Glow / Adjustment，使用捕获的电影曲线与 3.0 输入补偿，关闭时恢复 AgX 与 0.7 曝光。ColorSaturation / Contrast 只作用于原生后处理对照模式；Relink 模式由提取的 LUT 调色。

昼夜由太阳/月亮的 lux、太阳高度、月相、天气和天空亮度决定，日月 LightEnergy 保持 1。相机曝光由 Profile 的日夜 EV 曲线控制，与光源能量分开。运行时和编辑器使用同一曲线；转动镜头不会改变曝光，暂停时钟后曝光保持固定。

关闭 `DriveExposureFromProfile` 后保留手动 Camera Attributes。原生 AutoExposure 默认关闭。灯罩使用 300 cd/m² 自发光材质，只产生可见发光与泛光，不照亮邻近物体。

太阳落下后，天空亮度依据太阳高度在地平线亮度与夜间亮度之间进行对数插值，完全独立于相机参数。日夜相机 EV 曲线保留对数过渡；它只是可选的摄影方案，不是改变光照强度的方法。

MoonTint 控制月光的美术染色；MoonHaloStrength 控制背景中的蓝紫月晕，随月相和月亮高度衰减，不参与环境辐照。编辑器与运行时环境组件共用 `ApplySky`，夜空配色、月相和星光也随编辑器的 StartHour / StartLunarPhase 更新。以上昼夜插值开关默认关闭，其他模板沿用原配置。

已移除 `addons/scene_sky_preview` 插件。编辑器预览由 `[Tool] GameObject3D` 分发 `Component3D.OnEditorUpdate`，`DayNightEnvironmentComponent3D` 使用配置的 StartHour / StartLunarPhase 同步天空、日月、云和后处理；不初始化运行时模块、不生成角色行为、不写存档或推进世界时钟。`PreviewInEditor` 可关闭自动预览。只有当前编辑场景参与预览，组件临时绑定编辑器相机的环境和 Compositor，并让曝光继承 WorldEnvironment；切换场景、退出或关闭自动预览时恢复原有相机覆盖，不改变编辑器视角和 FOV。

环境实例的 `Components → DayNightEnvironmentComponent3D` 提供节点路径、Profile 和 `LightSceneSprites`。后者让本关卡原有 Sprite3D 参与天空光、日月光和投影，退出时恢复原属性。角色仍保留原生受光和原有 Alpha 抗锯齿，未改为自发光或 Unshaded。新会话从第一个进入的环境 Profile 初始化日历；之后切换场景只替换视觉风格，既有日历或已加载存档优先。环境、天空、ShaderMaterial、曝光均勾选 Local To Scene，实例间的资源隔离在场景加载时完成。

编辑器保存的环境为正午预览；动态循环在运行时由 EC 组件驱动。在 Remote 场景树中可以检查日月、WorldEnvironment 和宿主的 `time_of_day` 元数据。运行时改 Profile 颜色、曝光补偿或天体大小后，调用组件 `RefreshProfile()` 即可刷新，不会重置日历。

## 调用

### 运行时 GM

进入 `spacelevel` 后按 **F8** 或右上角 **GM** 打开面板，选择「时间与天空」。拖动时间滑条会立即更新太阳、月亮、天空和曝光；拖动期间暂时停表，松开后恢复原来的播放状态。也可输入日期、小时和分钟（回车提交），一键跳到黎明、正午、黄昏或午夜，独立暂停/调整时钟倍率，选择八种月相或拖动月相滑条。

面板打开时暂时关闭项目输入层，避免点击控件触发角色攻击或移动镜头；收起或退出场景后恢复之前的输入状态。世界模拟不会因此暂停。修改的是当前日历，后续正常存档会包含修改；面板本身不主动写盘。即使游戏暂停也可操作面板。

`spacelevel/RuntimeGm` 实例化 `res://assets/scenes/ui/runtime_gm.tscn`，所有 Control、样式和 F8 Shortcut 都已序列化。`RuntimeGmComponent3D` 负责面板开关和输入隔离；`TimeOfDayGmComponent3D` 以 Embedded 模式绑定时间页，`EnvironmentRigPath` 指向旁边的环境宿主。面板默认展开由外壳的 `StartOpen` 控制，其他页面见 [Runtime GM](../gm/README.md)。

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

调用 `SetDate` / `SetTimeOfDay` / `AdvanceHours` 后，环境立即按目标时刻更新日月、天空与曝光。编辑器修改 StartDay / StartHour 采用相同的配置曲线。

Spacelevel 的白天 EV 为 14.35，夜间 EV 为 2，夜间天空美术强度为 8；曝光与太阳高度关联。夜间天空底值与当前曝光、天空配色和 Relink 电影曲线配合校准，使日落后天空光衰减到终点时，雨天、新月的道路和建筑仍然可读。天空强度沿原有太阳高度曲线连续变化，不按整点切换。日月、天空和灯罩强度独立由光源控制。关闭 `DriveExposureFromProfile` 时，昼夜变化不会改写手动相机曝光。

## 原作者纸片云

云也接入天空环境反射。`DayNightEnvironmentComponent3D` 管理 `SkyCloudRadiance`，在独立世界中复制原三层网格和材质，以六个 256×256 透明 HDR 视口采样完整方向；不包含关卡几何、灯光或后处理。`cloud_radiance_copy.gd` 在绘制后把结果复制到独立 GPU 纹理，天空只接收没有节点路径的 Texture2DRD，避免复制场景时解析临时 Face0..5 节点。采样颜色为线性预乘 alpha，Sky Shader 仅在 AT_CUBEMAP_PASS 与大气辐照合成，因此可见背景仍直接绘制原云网格。天空亮度和原有投影旋转继续应用，天空辐照始终保留。云转动、SDF、噪声、颜色及天气改变时按 VisualUpdatesPerSecond 刷新；暂停且配置不变时保留采样。关闭 CloudsEnabled 同时关闭云倒影，编辑器退出与组件回收先解绑纹理，再在渲染线程释放 GPU 副本，延迟释放采样视口。内部采样节点不序列化到场景。

当前云直接使用[原仓库](https://github.com/xinyangaa/Unity_URP_Genshin_Impact_Programmed_Skybox/tree/ef1bd4c6fa486977a323c3badaba5740feae1d55)提供的 cloud.fbx、原始 UV2、RGBA 云图和噪声文件。`EnvironmentRig/OriginalClouds` 下的 BaseClouds、HighClouds、SecondaryClouds 都是场景内的真实 MeshInstance3D，各自材质和网格均已序列化，不在运行时创建。

`EnvironmentRig → Components → DayNightEnvironmentComponent3D → Profile → Original Cloud Timeline` 可控制启用状态、CloudEvolutionPeriodHours（云形周期，默认 1.6 游戏小时）、CloudRotationPeriodHours（整体旋转周期，默认 1.6 游戏小时）、CloudNoisePeriodHours（噪声周期，默认 8 游戏小时）及三层云各自的 Timeline。CloudRotationTimeline 保留原作者旋转曲线，其中只启用 GroupYaw 驱动共同父节点；上层和第二层通过继承同步移动，不再叠加额外自转。周期数值越大，旋转越慢。展开其 Curves 可直接编辑原作者的 22 条材质曲线，包括 SDF、四种明暗颜色、亮边颜色和日月切换；未额外柔化、改写首尾关键帧。材质平铺应用两次，与源 Shader 相同；噪声相位也从累计游戏时间取样。

旋转、云形、噪声和颜色统一由 WorldClock.TotalHours 计算。默认 20 分钟一天时，1.6 游戏小时仍相当于原 80 秒节奏；改变日长或 GM 倍率会同步改变云速，同一游戏日期／时刻的云位置不会因日长变化而跳变。F8 时间 GM 的暂停、快进和回退同步控制云。编辑器与运行时共用 Profile.SampleCloudTime：编辑 StartDay / StartHour 即可预览对应时刻，编辑器不再用独立秒表推动云。运行时逐帧更新共同转动和各层 SDF／噪声，全部遵循世界时钟。现有日月轨道、月相、相机跟随和正交／透视切换保持接入。

原仓库缺少示例场景引用的高空及第二层云网格（GUID `4e842a0dab424954fbbe40e4d81b5a3d`），其噪声材质 GUID 也无法对应到上传文件。现已离线补建高层和第二层网格，接上原作者第二套图集、各自 22 条材质曲线及旋转曲线；底层云也随整体风向转动。新增网格为补建布局，底层保留原始 FBX。补建云片的竖直方向与环半径从底层网格提取，去掉朝球心倾斜和随机滚转，使高低层的透视缩短方式一致；三层共用同一相机投影和转动角度。三层噪声均使用同仓库 Noise_091；这不是文章 GIF 的逐项原样复刻。原始文件校验、资源出处和必要引擎适配见 `docs/art/sky_clouds.md`。

## 场景相机与投影

`spacelevel/CameraRig/Camera3D` 是真实序列化的场景相机。直接在 Inspector 调整 Transform、Projection、FOV、Size、Near/Far，并用 Camera3D 的 Preview 检查构图。运行时不创建全局相机，也不覆盖投影、FOV 或初始构图。

`CameraRig → Components → SceneCameraComponent3D` 是唯一的相机行为组件：`TargetPath = ../Player`，`CameraPath = Camera3D`。它在目标准备完成后自动绑定，处理跟随、前瞻、平滑和震动。原来的 LevelCamera / GlobalCamera 两层组件已经合并移除。其 `Profile` 只保留跟随参数；构图以 Camera3D 为准。CameraModule 只路由 Follow、Focus、Shake 等调用，不创建或销毁相机节点。

天空会自动适配相机投影：正交使用 Profile 的 `OrthographicSkyFov = 65` 和 `OrthographicSkyPitchDegrees = -33` 进行横版背景构图；透视/视锥模式关闭这两个覆盖，使用相机真实投影、朝向和 FOV。透视的视野大小取决于 FOV 和距离，正交的 Size 对透视不起作用。该切换在时钟暂停时也生效。

环境组件的 Tool 预览让本地相机切换和天空/日月尺寸参数的修改直接在编辑器中生效，无需额外天空插件。编辑器自由视角仍用于浏览，最终构图请看场景 Camera3D Preview。修改 Compositor 中共享 Profile 的 `LutStrength` 可直接看到调色变化；MSAA 下的结果会写回多采样颜色缓冲，不被透明后的再次解析覆盖。

## 模型边界

这是可配置的游戏天球模型，不是现实天文历：日出 06:00、日落 18:00、太阳正午达到设定高度；月球轨道按月相相对太阳偏移，因此上弦月傍晚高悬、满月午夜高悬。月盘通过球面朝向和日照方向计算连续明暗，月光按可见照明比例衰减。尚不模拟季节、经纬度或日食月食；[天气系统](../weather/README.md)通过同宿主组件叠加晴、阴、雨、雪与自动调度。

日夜共用物理光照单位和相机曝光。夜间天空强度是为可玩性设置的美术补光，可在 Profile 中调低；月光本身默认 0.3 lux。星光和天体盘不写入环境辐照贴图，以免与方向灯重复照明。Shader 不使用 TIME；云的噪声与材质曲线由游戏时钟驱动，暂停时保持不变，画面仍正常逐帧绘制。

参考：[Godot 物理光照与相机单位](https://docs.godotengine.org/en/4.6/tutorials/3d/physical_light_and_camera_units.html)、[天空 Shader](https://docs.godotengine.org/en/4.6/tutorials/shaders/shader_reference/sky_shader.html)。

## 验证

```powershell
dotnet build yu.csproj
& 'D:/DCC/Godot_v4.6.1-stable_mono_win64/Godot_v4.6.1-stable_mono_win64.exe' --path . res://assets/scenes/spacelevel.tscn
```

检查跨日、暂停、速度、月相环绕、非法输入原子恢复、日月位置、存档往返、实例资源隔离、GM、烘焙云资源、相机自动绑定和跟随、保留编辑器构图、正交/透视天空切换以及退出释放。

验收应覆盖白天、黄昏和午夜的跳时曝光，暂停时转动镜头，以及连续移动后的亮度与阴影稳定性；确认运行时 SDFGI / SSIL 关闭，GM 只保留渲染诊断与后处理控制。

临时验证脚本、截图和性能数据放在忽略目录 `.godot/`，不作为游戏源码或发布资源保留。
