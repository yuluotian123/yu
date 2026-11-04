# 森林小镇

`assets/scenes/levels/forest_town_level.tscn` 已直接替换为[十户森林聚落](forest_village_whitebox.md)，实例化 `environment/forest_village_whitebox/forest_village_whitebox.tscn`。旧临河小镇布局已移除；关卡路径、UID、世界 ID、角色、天气、DDGI、世界入口及 Resident 生活行为配置沿用。默认出生点移到聚落南入口，仓储间入口位于杂货与待客户门前，导航按新聚落碰撞重新烘焙。喝水、取货与跨关卡配置见 [完整示例](../../doc/NPC_DAILY_GOALS_EXAMPLE.md)。

当前十户建筑使用[第二版概念房屋模型](forest_village_houses.md)，共十种不同体量与屋顶结构，已制作侧面、背面、碰撞及 LOD。下文旧八栋建筑的制作与统计保留为共享森林资产的历史记录。

以下模型制作、材质与性能记录说明共享森林资产；原临河布局及其性能数据属于历史记录。旧 `build_scene.gd` 的临河布局生成配方不再用于正式关卡。更新聚落布局后，用 `forest_village_whitebox/replace_forest_level.gd` 同步正式关卡、入口和导航，再通过关卡配置导出刷新世界目录。

参考原画：`assets/textures/concepts/environments/forest_town_v1/scene-concept.png`。场景在固定 Godot 相机与环境下重建；材质使用手绘笔触、分区配色和绘画式直接光照。

## 独立模型与面数

所有物件在 Blender MCP 中制作，分别导出 GLB 和 Godot 模型场景。八栋建筑都有完整四面外墙、完整屋顶、侧后方门窗与梁柱；没有用单面立面或背景图片代替完整建筑。树木使用连续弯曲环线制作树干、渐细主枝、二三级分叉和曲线根部，树皮有贴合表面的浅疤痕；独立的叶簇曲面附着于枝梢。灌木使用弯曲枝条和带透明轮廓的橡树叶簇。

预算同时检查 Blender 多边形与 Godot 导入后三角面，均不超过 8000。重复物件共享对应的独立模型。

| 模型 | 独立种类 | 每个模型三角面 |
| --- | ---: | ---: |
| 完整建筑 | 8 | 4959–6249 |
| 树木 | 3 | 7296 |
| 灌木 | 4 | 2520 |
| 蕨类 / 林间花草 | 2 | 126 / 840 |
| 横向带叶枝梢 | 1 | 1544 |
| 石墙、围栏、阶梯、木桶、木箱、平台、高处街道 | 8 | 48–1980 |
| 地面与散布铺石（切出河岸） | 1 | 4670 |
| 河面 / 河岸 / 河石 / 芦苇 | 4 | 5760 / 1204 / 92 / 808 |

合计 31 个独立模型、239 个场景实例：8 栋建筑、78 棵树、111 个植物实例、40 个道具、1 个地面和 1 个河面。完整清单及逐项面数记录在 `assets/resources/art/forest_town_v1/models.json`。

## 树木细化与林区密度（2026-10-21）

当前只使用三个独立树型：粗根宽冠古橡树 `ancient_oak`、挺拔分叉树 `branching_oak` 和弯干舒展树 `canopy_oak`。模型采用 24 边树干截面、连续输运环线法线、根部外扩、二三级枝梢和树皮起伏；树冠从原来的 572–660 簇增加为每棵 935–1008 个四三角面叶簇，围绕 16–19 组不对称枝梢体积排列。2026-10-25 修整根部后为 7432 个三角面；2026-10-26 移除每种树的两个独立圆管树结，改成沿原树皮投射、两端收窄的浅纵向疤痕，完整近景模型为 7296 个三角面。疤痕最大起伏约 2–4 毫米，复用命中处的树皮 UV、顶点色和法线，避免浮起的圆环轮廓。树冠与其他保留面的顶点位置保持原样，沿用现有灰度叶簇贴图、顶点配色和真实树冠阴影。

该修整由 `scripts/tools/art/forest_town/repair_tree_knots.py` 完成，完整生成器也复用它，不再生成闭合圆管。检查记录保存在 `assets/resources/art/forest_town_v1/bark_scars_v4.json`；Blender 源文件、三个 GLB、运行时网格、LOD 与模型场景已同步更新。导入时使用 `build_tree_upgrade.gd -- --meshes-only`，保留村庄摆放与碰撞。

三种树的独立长尖根已在 Blender 中替换为与树干焊接的一体根盘，五条短而圆的根脊从树干平滑展开，并在接近地面处没入土中。根部以上的网格位置、UV 和顶点色逐面核对一致，摆放与树干碰撞沿用原值。源文件与独立 GLB / `.mesh.res` 均已更新；修整记录见 `roots_v3.json`，当前导入与 LOD 检查见 `tree_validation.json`。`repair_tree_roots.py` 已接入树木生成脚本，重新生成时也会应用这项修整。

原有 48 棵树改用这三种树型，侧后方增加 30 棵，合计 78 棵。新增树干避开建筑碰撞范围和主通路，通过旋转、缩放与错列打破重复；全部非树物件的摆放保持一致。旧 `forest_column`、`understory_oak` 等未使用的独立导出资源已移除，可编辑原始模型仍保存在 Blender 源文件中。

三种树都带原生距离 LOD，各表面的附加 LOD 数为 `[4,1]`，完整细节保留为 LOD0。RTX 5080、1920×1080、主菜单启动 ForestTownLevel、解除帧率上限的同机固定镜头对照：旧 48 棵树 GPU 中位耗时约 4.51 ms，新 78 棵树约 4.57–4.58 ms；完整帧平均耗时由约 5.42 ms 变为 5.42–5.54 ms。该对照保留相机、灯光、阴影质量、DDGI 和后处理配置，不代表所有位置和设备的表现。源资产统计在 `trees_v2.json`。

## 建筑与街道层次

用户确认“允许调整模型和摆放”后，主屋增加了完整低侧翼、高低屋顶、窗台植物、檐口叶簇和双面招牌。房屋比例按现有角色和固定镜头重新调整，两侧前景建筑围合街道；高处平台、12 级石阶与延伸街道组成远景通路。古橡树加粗，横向枝梢补充画面上方的树叶遮挡，深青色灌木和围栏形成前景。

石阶视觉使用独立台阶，碰撞使用坡度约 23 度的原生凸多边形坡面；后方街道和平台有连续碰撞支撑。八栋建筑仍具备完整四面外观。

## 材质与贴图

- 材质在 `assets/materials/environment/forest_town/`。建筑、树皮、地面使用现有 `town_toon.gdshader`；叶簇使用现有 `leaf_article.gdshader`，保留原生光照、天气与 DDGI 材质协议。树皮和树叶分别绑定到同一独立模型的两个网格表面。
- 当前手绘灰度图集为 `assets/textures/environment/forest_town/painterly_atlas_v3.png`，实际尺寸 **1254×1254**。16 个分区覆盖灰泥、木材、屋瓦、石材、树皮、泥土、叶片、苔藓、玻璃与树冠光斑遮罩；早期 `painted_atlas` 与重复导出贴图已移除。
- 颜色来自模型的 `Palette` 顶点色，灰泥、木材、屋瓦、石材与玻璃分表面绑定材质。暖灰泥使用较高的材质阴影抬升，深木框、窗玻璃和前景右屋使用较低值，保持明暗对比；背景与前景植物采用不同顶点色。地面使用连续空间顶点色消除方格感。
- `town_toon.gdshader` 和 `leaf_article.gdshader` 新增可选 `painterly_strength`，默认 0，保留其他材质原有行为。森林材质启用分段直接光照；原生高光、天气和 DDGI 材质提取协议继续沿用。
- 树荫使用实际树冠、枝干与太阳原生级联阴影。叶簇通过 `ALPHA_SCISSOR_THRESHOLD` 裁出纹理中的透明空隙，实际叶片轮廓参与投影。小镇 9 个接收材质的 `painterly_dapple_strength` 均为 0，停用此前按世界坐标平铺的模拟树荫；`build_scene.gd` 同步保持为 0。树下保留斑驳阴影，开阔街道和河岸不再附加无对应树冠的暗块。此次不增加投影网格或光源，也不调整相机、灯光、阴影质量、DDGI 和后处理参数。
- 新叶簇图集 `oak_sprigs_v2.png` 为内置 ImageGen 生成的灰度 RGBA 贴图，实际尺寸 **1254×1254**，四个分区包含圆裂橡树叶、枝梢、叶脉和透明空隙。当前每棵树使用 935–1008 个四三角面叶簇，每株灌木 420 个；形体细节由曲面与透明轮廓共同表现。
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

完整重建基础模型和河流后，再执行 `upgrade_trees.py` 的定义，依次调用 `build_tree('ancient_oak')`、`build_tree('branching_oak')`、`build_tree('canopy_oak')` 和 `finish_tree_upgrade()`。通过 MCP 分多次调用时，把执行命名空间保存在 `bpy.app.driver_namespace` 中。树木更新脚本保留非树资产和布置，重复执行不累积新增树木。只更新树木时使用 `build_tree_upgrade.gd` 导入三个 GLB、生成原生 LOD 并替换场景的 `Trees` 子树，避免重建其他模型和材质。

仅修整共享树模型时，在导入命令末尾追加 `-- --meshes-only`，更新网格及模型预制场景，保留森林小镇和聚落中的所有实例位置。

然后通过同一 Blender MCP 执行 `scripts/tools/art/forest_town/add_river.py`，切出前景河岸并导出独立河流资产。该脚本保留 `FT_GroundBeforeRiver` 源网格，可重复执行，不会累积裁剪或重复散布。若完整重建基础模型，应先执行 `finish()` 再执行河流脚本。

在项目根目录使用当前 Godot 4.8 引擎运行：

```powershell
$forestEngine = 'D:\DCC\Godot_4.8\bin\ddgi_scene\godot.windows.editor.x86_64.mono.ddgi_scene.console.exe'
& $forestEngine --headless --path . --script res://scripts/tools/art/forest_town/build_scene.gd --quit-after 600
& $forestEngine --headless --path . --script res://scripts/tools/art/forest_town/build_tree_upgrade.gd --quit-after 600
```

构建脚本直接读取最新 GLB，并保存带 UV、顶点色和逐表面材质的共享网格与独立模型场景，避免另一个开着的编辑器保留旧导入缓存。正式画面通过主场景进入林间镇检查。

## 运行检查

构建后从主菜单进入林间镇，检查建筑与地面碰撞、角色落地、阶梯坡面、高处街道支撑、水面动画、树冠阴影，以及 NPC 对话和往返仓储间。使用 Forward+ Vulkan 检查 DDGI 和后处理。独立验证器、旧验证报告与临时编辑器同步脚本已移除，模型生成和导入工具保留。

## 性能定位（2026-10-21）

本节记录树木加密前的性能定位：209 个独立实例合计 614396 个三角面，其中树木和植物占 553736 个。单资产小于 8000 面不等于整个场景负载低；固定镜头下统计约 505 次绘制、169 万个图元，包含阴影等重复绘制。临时隐藏植被的对照中，GPU 中位耗时由约 4.7 ms 降至 2.1–2.3 ms；临时隐藏河流几乎无改善。最新树木布置的性能对照见上文。

另一个瓶颈是 DDGI 的 CPU 场景采集：364 个材质表面逐帧重复读取共享材质。现在每份快照只读取每个共享材质一次，并复用白色占位纹理；下帧仍重新采集，保留天气、材质修改与动态物体的及时更新。`DdgiEffect.GetStatus()` 的 `capture_cpu_ms` 可查看这部分耗时，`gpu_update_ms` 只代表 DDGI 探针 GPU 更新，不能当作完整帧耗时。

RTX 5080、当前定制 Godot 4.8、1920×1080、主菜单启动 ForestTownLevel、原固定镜头下，预热后对比结果：

| 指标 | 优化前 | 优化后 |
| --- | ---: | ---: |
| DDGI CPU 采集中位耗时 | 5.6 ms | 1.4–1.5 ms |
| 完整帧平均耗时（临时解除帧率上限） | 10.5 ms | 5.9–6.3 ms |
| 平均帧率（由完整帧平均耗时换算） | 约 95 FPS | 约 158–170 FPS |
| GPU 中位耗时 | 4.7 ms | 4.7–4.8 ms |

恢复项目原有 120 FPS 上限和 VSync 后，360 帧采样平均约 117 FPS。优化后仍有少量约 40 ms 的长帧，尚未消除所有卡顿；场景、材质效果、分辨率、抗锯齿、阴影、后处理及 DDGI 质量配置均未调整。以上是静止镜头的本机测试，不代表所有位置或其他设备的帧率。

以上性能采样为历史基线；后续使用正式关卡和 profiler 重新测量。
