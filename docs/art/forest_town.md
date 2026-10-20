# 森林小镇

`assets/scenes/spacelevel.tscn` 通过 `levels/forest_town_level.tscn` 接入 `environment/forest_town/forest_town.tscn`。白盒基础场景继续提供原有角色、相机、环境、天气、DDGI 和玩法模块；新场景提供完整建筑、林木、植物、道路与原生碰撞。

参考原画：`assets/textures/concepts/environments/forest_town_v1/scene-concept.png`。场景在固定 Godot 相机与环境下重建；材质使用手绘笔触、分区配色和绘画式直接光照。

## 独立模型与面数

所有物件在 Blender MCP 中制作，分别导出 GLB 和 Godot 模型场景。八栋建筑都有完整四面外墙、完整屋顶、侧后方门窗与梁柱；没有用单面立面或背景图片代替完整建筑。树木使用连续弯曲环线制作树干、渐细主枝、二三级分叉、曲线根部和树结；独立的叶簇曲面附着于枝梢。灌木使用弯曲枝条和带透明轮廓的橡树叶簇。

预算同时检查 Blender 多边形与 Godot 导入后三角面，均不超过 8000。重复物件共享对应的独立模型。

| 模型 | 独立种类 | 每个模型三角面 |
| --- | ---: | ---: |
| 完整建筑 | 8 | 4959–6249 |
| 树木 | 5 | 6984–7816 |
| 灌木 | 4 | 2520 |
| 蕨类 / 林间花草 | 2 | 126 / 840 |
| 横向带叶枝梢 | 1 | 1544 |
| 石墙、围栏、阶梯、木桶、木箱、平台、高处街道 | 8 | 48–1980 |
| 地面与散布铺石（切出河岸） | 1 | 4670 |
| 河面 / 河岸 / 河石 / 芦苇 | 4 | 5760 / 1204 / 92 / 808 |

合计 33 个独立模型、209 个场景实例：8 栋建筑、48 棵树、111 个植物实例、40 个道具、1 个地面和 1 个河面。完整清单及逐项面数记录在 `assets/resources/art/forest_town_v1/models.json`。

## 建筑与街道层次

用户确认“允许调整模型和摆放”后，主屋增加了完整低侧翼、高低屋顶、窗台植物、檐口叶簇和双面招牌。房屋比例按现有角色和固定镜头重新调整，两侧前景建筑围合街道；高处平台、12 级石阶与延伸街道组成远景通路。古橡树加粗，横向枝梢补充画面上方的树叶遮挡，深青色灌木和围栏形成前景。

石阶视觉使用独立台阶，碰撞使用坡度约 23 度的原生凸多边形坡面；后方街道和平台有连续碰撞支撑。八栋建筑仍具备完整四面外观。

## 材质与贴图

- 材质在 `assets/materials/environment/forest_town/`。建筑、树皮、地面使用现有 `town_toon.gdshader`；叶簇使用现有 `leaf_article.gdshader`，保留原生光照、天气与 DDGI 材质协议。树皮和树叶分别绑定到同一独立模型的两个网格表面。
- 当前手绘灰度图集为 `assets/textures/environment/forest_town/painterly_atlas_v3.png`，实际尺寸 **1254×1254**。16 个分区覆盖灰泥、木材、屋瓦、石材、树皮、泥土、叶片、苔藓、玻璃与树冠光斑遮罩；旧 `painted_atlas.png` 保留为早期版本。
- 颜色来自模型的 `Palette` 顶点色，灰泥、木材、屋瓦、石材与玻璃分表面绑定材质。暖灰泥使用较高的材质阴影抬升，深木框、窗玻璃和前景右屋使用较低值，保持明暗对比；背景与前景植物采用不同顶点色。地面使用连续空间顶点色消除方格感。
- `town_toon.gdshader` 和 `leaf_article.gdshader` 新增可选 `painterly_strength`，默认 0，保留其他材质原有行为。森林材质启用分段直接光照；地面使用世界空间树冠遮罩，并降低道路中央的遮罩强度。该遮罩是材质绘画效果，不修改真实灯光或阴影贴图。原生高光、天气和 DDGI 材质提取协议继续沿用。
- 新叶簇图集 `oak_sprigs_v2.png` 为内置 ImageGen 生成的灰度 RGBA 贴图，实际尺寸 **1254×1254**，四个分区包含圆裂橡树叶、枝梢、叶脉和透明空隙。每棵树使用 572–660 个四三角面叶簇，每株灌木 420 个；形体细节由曲面与透明轮廓共同表现。
- 叶簇采用作者制作的圆润体积法线。材质的 `normal_roundness`、`gradient_strength`、`wind_strength` 均为 0，避免旧材质内六棵合并树的固定中心覆盖新模型法线；`vertex_color_strength=1`、`alpha_cutoff=0.38`、`texture_detail=0.30`。
- `painterly_atlas_v3.res`、`oak_sprigs_v2.res` 是原生带 mipmap 纹理，配合材质采样降低远处细纹闪烁；保留原 PNG 的内容与尺寸。树皮 UV 按环段重复，避免整棵树共用一段被拉长的纹理。
- 两个图集均使用内置 ImageGen 生成；完整提示词和参考路径记录在 `painterly_generation_v3.json`、`foliage_generation_v2.json`，位于 `assets/resources/art/forest_town_v1/`。
- GLB 导出固定使用活动顶点色作为 `COLOR_0`，避免材质混合节点生成的白色通道覆盖配色。

**相机、曝光、灯光、天空、雾、后处理和环境组件沿用任务开始时的值。** 材质调整不通过改变这些画面参数取得效果。场景中一个本轮新增的 `CameraFramingTarget` 标记已不被相机引用；删除请求被自动审批拒绝后保留，不参与渲染或跟随。

## 前景河流

`RPGTown/Water/ForegroundRiver` 是单独制作的 5760 三角面河面，宽 4.8 米，水位 Y=-0.24；弯曲河岸绕过两侧前景灌木，在画面下方露出水面。地面网格实际切除水域，保留手绘顶点色与 UV，并将陆地碰撞收至河岸。水面没有碰撞，不提供游泳逻辑。岸沿、河石和带曲线叶片的芦苇分别制作，原有围栏沿岸重新摆放，两根柱脚均落在陆地上。

专用材质为 `assets/materials/environment/forest_town/river.tres`，着色器为 `assets/shaders/town/forest_river.gdshader`。它使用深青、青绿、浅绿的离散色阶，世界空间移动笔触、断续细波纹、树色反射斑块、两层岸边泡沫和轻微顶点起伏。反射斑块是绘画图案；原生环境反射保留，未加入屏幕空间折射或镜面倒影。河面采用不透明渲染，岸边距离来自作者 UV，不依赖深度纹理、透明排序或新的后处理。动态河面关闭投影和静态 GI 捕获，避免作为实体遮挡 DDGI。

在河面 ShaderMaterial 中可调整 `deep_color` / `middle_color` / `shallow_color`、`flow_speed`、`foam_width`、`ripple_strength`、`reflection_strength` 和 `wave_height`；`river_width` 应与模型的 4.8 米保持一致。Blender 导出时预翻转水面 V，使 Godot 中 V=0 对应小镇一侧河岸，V=1 对应外侧。

## 可编辑源文件与重建

Blender 文件：`assets/models/environment/forest_town/source/forest_town.blend`。`source/.gdignore` 避免 Godot 自动转换 Blender 文件；游戏使用旁边各自独立的 GLB / `.mesh.res` 资源。

在连接的 Blender 中，通过 MCP 执行 `scripts/tools/art/forest_town/build_assets.py` 的定义，再依次调用 `buildings()`、`vegetation()`、`props_and_ground()`、`finish()`。脚本只维护本任务的模型和集合，不合并整个小镇。

然后通过同一 Blender MCP 执行 `scripts/tools/art/forest_town/add_river.py`，切出前景河岸并导出独立河流资产。该脚本保留 `FT_GroundBeforeRiver` 源网格，可重复执行，不会累积裁剪或重复散布。若完整重建基础模型，应先执行 `finish()` 再执行河流脚本。

在项目根目录使用当前 Godot 4.8 引擎运行：

```powershell
$forestEngine = 'D:\DCC\Godot_4.8\bin\ddgi_scene\godot.windows.editor.x86_64.mono.ddgi_scene.console.exe'
& $forestEngine --headless --path . --script res://scripts/tools/art/forest_town/build_scene.gd --quit-after 600
& $forestEngine --headless --path . --script res://scripts/tools/art/forest_town/validate_scene.gd --quit-after 600
& $forestEngine --path . --rendering-driver vulkan --script res://scripts/tools/art/forest_town/capture_scene.gd
```

构建脚本直接读取最新 GLB，并保存带 UV、顶点色和逐表面材质的共享网格与独立模型场景，避免另一个开着的编辑器保留旧导入缓存。实机截图写入忽略目录 `.godot/forest-town/godot-composition.png`，不覆盖参考原画。截图同时将实际 DDGI 状态写入 `.godot/forest-town/ddgi-status.json`。

`editor_sync.gd` 只用于把新布局同步到当前编辑器的 `RPGTown` 节点：先保存带时间戳的子树快照，再替换该子树，校验其他同级节点及其变换均未变化。快照在 `.godot/forest-town/editor-town-before-<时间>.tscn`；新实例不挂工具脚本。它不重载整个 `spacelevel.tscn`。若编辑器已自动刷新外部场景，无须再替换。Blender 预览镜头复制固定 Godot 镜头以便检查构图，不反写 Godot 相机。

## 验证

33 个模型的实际导入面数、UV、顶点色和叶簇表面均已检查，记录在 `godot_validation.json`。相机与环境基准、完整建筑实例、材质绑定、地面碰撞、建筑阻挡、角色落地、台阶坡面、高处街道支撑、水面 UV 方向和河岸碰撞合计 97 项检查通过，记录在 `scene_validation.json`。通过真实主菜单按钮触发关卡加载，并验证退出后再次进入；实际 Forward+ / Vulkan 渲染中水面采样确认发生动画。河流未修改 C# 或渲染器参数。

`material_scope_validation.json` 是此前只改材质阶段的历史记录；随后获得模型和摆放调整授权，最新范围与结果记录在 `reconstruction_v4.json`。

运行日志仍可能包含本机证书、MCP 注册表写权限、未配置全局黑板，以及退出时既有资源清理提示；这些不属于材质或模型校验结果。
