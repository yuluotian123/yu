# 昼夜与月相

`TimeOfDayModule` 管理一个全局 `WorldClock`，`DayNightEnvironmentComponent3D` 按 EC 生命周期驱动场景环境。当前 `spacelevel.tscn` 直接保存 `EnvironmentRig/WorldEnvironment`、`Sun`、`Moon` 及所有材质、曝光和配置资源。控制器不在运行时创建节点或材质，只更新已有属性。另提供 `res://assets/scenes/environment/day_night_environment.tscn` 作为其他关卡可直接摆入的模板；同一个 World3D 使用一套环境。

默认 20 分钟一个游戏日、初始满月、29.53059 天一个月相周期。模板默认 12:00，当前关卡保留原有的 StartHour = 10.06。时间受游戏速度影响，游戏暂停、时钟暂停或没有环境宿主时停止推进。返回菜单和切换关卡保留日历。旧存档没有时间数据时使用默认值。

## 配置

在 `spacelevel` 中选中 `EnvironmentRig → Components → DayNightEnvironmentComponent3D → Profile`，直接编辑内嵌配置。独立模板使用 `res://assets/environment/default_time_of_day.tres`，不影响当前场景的内嵌配置。

- Calendar Defaults：新会话的日长、初始日期/时刻、朔望月长度、初始月相和暂停状态。已有存档优先。
- Sun and Moon：正午高度、方位角、直射照度、日月视直径。太阳默认 1.8 度、月亮默认 3 度；两者独立可调，设为约 0.52 度可接近真实视直径。编辑器预览同步更新。
- Sky：日夜天空、地平线与晚霞颜色、天空强度、星光。
- Exposure and Quality：日夜 EV、曝光补偿和环境更新频率，默认 10 Hz；暂停且时间不变时停止刷新天空。

环境实例的 `Components → DayNightEnvironmentComponent3D` 提供节点路径、Profile 和 `LightSceneSprites`。后者让本关卡原有 Sprite3D 参与光照，退出时恢复原属性。新会话从第一个进入的环境 Profile 初始化日历；之后切换场景只替换视觉风格，既有日历或已加载存档优先。环境、天空、ShaderMaterial、曝光均勾选 Local To Scene，实例间的资源隔离在场景加载时完成。

编辑器保存的环境为正午预览；动态循环在运行时由 EC 组件驱动。在 Remote 场景树中可以检查日月、WorldEnvironment 和宿主的 `time_of_day` 元数据。运行时改 Profile 颜色、曝光补偿或天体大小后，调用组件 `RefreshProfile()` 即可刷新，不会重置日历。

## 调用

### 运行时 GM

进入 `spacelevel` 后按 **F8** 或右上角 **时间 GM** 打开面板。拖动时间滑条会立即更新太阳、月亮、天空和曝光；拖动期间暂时停表，松开后恢复原来的播放状态。也可输入日期、小时和分钟（回车提交），一键跳到黎明、正午、黄昏或午夜，独立暂停/调整时钟倍率，选择八种月相或拖动月相滑条。

面板打开时暂时关闭项目输入层，避免点击控件触发角色攻击或移动镜头；收起或退出场景后恢复之前的输入状态。世界模拟不会因此暂停。修改的是当前日历，后续正常存档会包含修改；面板本身不主动写盘。即使游戏暂停也可操作面板。

`spacelevel/TimeGm` 是直接放在场景中的 `res://assets/scenes/ui/time_of_day_gm.tscn` 实例，所有 Control、样式和 F8 Shortcut 都已序列化。其 EC 组件只绑定已有控件；`EnvironmentRigPath` 指向旁边的环境宿主，`StartOpen` 控制是否默认展开。

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

## 原作者纸片云

当前云直接使用[原仓库](https://github.com/xinyangaa/Unity_URP_Genshin_Impact_Programmed_Skybox/tree/ef1bd4c6fa486977a323c3badaba5740feae1d55)提供的 cloud.fbx、原始 UV2、RGBA 云图和噪声文件。`EnvironmentRig/OriginalClouds/BaseClouds` 是场景内的真实 MeshInstance3D，材质和网格均已序列化，不在运行时创建。上一版自制图集和 32 张卡片布局不再参与渲染。

`EnvironmentRig → Components → DayNightEnvironmentComponent3D → Profile → Original Cloud Timeline` 可控制启用状态、CloudEvolutionPeriodSeconds（原默认 80 秒）以及 CloudTimeline。展开其 Curves 可直接编辑原作者的 22 条材质曲线，包括 SDF、四种明暗颜色、亮边颜色和日月切换；未额外柔化、改写首尾关键帧。噪声每秒偏移 0.0025 UV，材质平铺应用两次，与源 Shader 相同。

云形使用原 80 秒 Timeline 节奏，颜色按原曲线映射到本项目的 20 分钟昼夜。F8 时间 GM 的暂停、快进和回退同步控制云。编辑器显示起始时刻的静态帧；运行时逐帧更新。现有日月轨道、月相、相机跟随和正交／透视切换保持接入。

原仓库缺少示例场景引用的高空及第二层云网格（GUID `4e842a0dab424954fbbe40e4d81b5a3d`），其噪声材质 GUID 也无法对应到上传文件。因此当前原样恢复的是仓库提供的底层云，噪声使用同仓库 Noise_091；尚不能声称完整复现文章 GIF。原始文件校验、资源出处和必要引擎适配见 `assets/environment/CLOUD_ART.md`。

## 场景相机与投影

`spacelevel/CameraRig/Camera3D` 是真实序列化的场景相机。直接在 Inspector 调整 Transform、Projection、FOV、Size、Near/Far，并用 Camera3D 的 Preview 检查构图。运行时不创建全局相机，也不覆盖投影、FOV 或初始构图。

`CameraRig → Components → SceneCameraComponent3D` 是唯一的相机行为组件：`TargetPath = ../Player`，`CameraPath = Camera3D`。它在目标准备完成后自动绑定，处理跟随、前瞻、平滑和震动。原来的 LevelCamera / GlobalCamera 两层组件已经合并移除。其 `Profile` 只保留跟随参数；构图以 Camera3D 为准。CameraModule 只路由 Follow、Focus、Shake 等调用，不创建或销毁相机节点。

天空会自动适配相机投影：正交使用 Profile 的 `OrthographicSkyFov = 65` 和 `OrthographicSkyPitchDegrees = -33` 进行横版背景构图；透视/视锥模式关闭这两个覆盖，使用相机真实投影、朝向和 FOV。透视的视野大小取决于 FOV 和距离，正交的 Size 对透视不起作用。该切换在时钟暂停时也生效。

项目自带 `Scene Sky Preview` 编辑器插件，让本地相机切换和天空/日月尺寸参数的修改也能直接在编辑器预览中生效；游戏里的同样逻辑由 EC 环境组件执行。编辑器自由视角仍用于浏览，最终构图请看场景 Camera3D Preview。

## 模型边界

这是可配置的游戏天球模型，不是现实天文历：日出 06:00、日落 18:00、太阳正午达到设定高度；月球轨道按月相相对太阳偏移，因此上弦月傍晚高悬、满月午夜高悬。月盘通过球面朝向和日照方向计算连续明暗，月光按可见照明比例衰减。尚不模拟季节、经纬度、日食月食或天气变化。

日夜共用物理光照单位和相机曝光。夜间天空强度是为可玩性设置的美术补光，可在 Profile 中调低；月光本身默认 0.3 lux。星光和天体盘不写入环境辐照贴图，以免与方向灯重复照明。Shader 不使用 TIME；云的噪声与材质曲线由游戏时钟驱动，暂停时保持不变，画面仍正常逐帧绘制。

参考：[Godot 物理光照与相机单位](https://docs.godotengine.org/en/4.6/tutorials/3d/physical_light_and_camera_units.html)、[天空 Shader](https://docs.godotengine.org/en/4.6/tutorials/shaders/shader_reference/sky_shader.html)。

## 验证

```powershell
dotnet build yu.csproj
& 'D:/DCC/Godot_v4.6.1-stable_mono_win64/Godot_v4.6.1-stable_mono_win64.exe' --headless --path . res://assets/scenes/time_of_day_smoke.tscn
```

检查跨日、暂停、速度、月相环绕、非法输入原子恢复、日月位置、存档往返、实例资源隔离、GM、烘焙云资源、相机自动绑定和跟随、保留编辑器构图、正交/透视天空切换以及退出释放。
