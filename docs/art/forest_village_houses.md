# 森林聚落十户房屋模型：V2 概念、V3 细化

十栋建筑依据 `assets/textures/concepts/forest_village/houses_v2/` 的第二版概念图在 Blender 中独立建模，已接入正式森林关卡共享的聚落环境。建筑包含完整外墙、屋顶厚度、檐口、侧后窗、后门或储物门；外观模型通过正门接入[十户独立室内关卡](forest_village_interiors.md)，后侧保持封闭。侧后方依建筑用途补充结构，并保留十种不同的体量与屋顶轮廓。

游戏角色高约 0.936 米，门洞按此尺度制作。Blender 使用 Z 向上、-Y 为正面；导出后 Godot 为 +Y 向上、+Z 为正面。各模型的原点、缩放均已规范，场景中的 `DoorAnchor` 是实际门口，`ApproachAnchor` 是院外通行点；围合院落、门廊及楼梯按各自形状设置简化碰撞。

| 文件名 | 住户与结构 | 三角面 |
| --- | --- | ---: |
| `01_chief` | 村长家，L 形低长屋、四坡瓦顶、议事门廊与后门 | 18,569 |
| `02_host` | 杂货待客户，窄高木楼、交易雨棚、侧阳台与后储物门 | 18,713 |
| `03_smith` | 工具修补户，厚石墙、单坡板岩顶、独立修补炉棚 | 18,800 |
| `04_carpenter` | 木工户，长盐箱木瓦顶、开放工作廊、封闭后墙 | 18,800 |
| `05_herbalist` | 草药照护户，六边形灰泥墙、锥顶与低矮照护附屋 | 18,800 |
| `06_forester` | 伐木户，A 字陡顶、完整原木山墙、后窗与两侧屋面窗 | 18,799 |
| `07_gatherer` | 猎采户，架高木板屋、单坡顶、前侧后连续木廊 | 18,799 |
| `08_farmer_east` | 东田农户，白墙绿门、低瓦顶与可通行 U 形小院 | 18,503 |
| `09_farmer_west` | 西田农户，厚茅草长屋顶、宽储物门、侧面阁楼口 | 18,799 |
| `10_smallholder` | 小畜养户，错层石木双体、草皮覆顶、后侧杂用门 | 18,656 |

V3 总计 187,238 三角面，每栋近景为 18,503–18,800，按三角面执行 10,000–20,000 的预算。去除封闭房体内不可见的墙背面、室内封顶和道具，保留门窗凹槽与外部廊道；不制作房间内景。每栋使用一个独立网格，按材质分表面，Godot 原生 LOD 随距离简化。材质继续使用顶点色与 `town_toon.gdshader`，保留原生光照、DDGI、雨雪和相机遮挡协议。

V3 增加弧面搭接瓦与圆脊瓦、错缝不规则石块、倒角和轻微弯曲的木构、横向搭接木板、细茅草束及散乱檐口。门窗补充窗格、铁件、铰链和挂灯；盆栽、藤叶、木桶、木箱、工具和储物设施覆盖侧面与背面。墙面 UV 连续，木纹顺木板和梁的长轴，避免开窗处纹理接缝与横木上的竖纹。

新灰度手绘材质图集 `assets/textures/environment/forest_village/houses/painterly_house_atlas_v3.png` 为内置 ImageGen 生成，实际 1254×1254 像素，运行时保存含 mipmap 的同名 `.res`。分区包含灰泥、旧木、新木、瓦面、石面、茅草、叶片、草皮和铁件，配合模型顶点色表现参考图中的暖木、土黄、灰石与苔绿。生成提示词和输出记录见 `assets/resources/art/forest_village/houses_texture_v3_generation.json`。材质加强笔触细节，并降低过度的包裹光与阴影提亮，使檐下与凹处保持深度；不把概念图的固定阳光烘焙进贴图。

同目录的 `_normal.png`、`_roughness.png` 和 `_orm.png` 为 Blender Cycles 烘焙的 2048×2048 数据图，带同名 mipmap `.res`。法线采用 OpenGL +Y 切线空间，依据灰度笔触构造材质表面微起伏；不代表对整栋高模的法线转移。粗糙度按图集分区控制：灰泥 0.86–0.98、旧木 0.70–0.91、新木 0.67–0.88、瓦 0.64–0.88、石 0.80–0.98、茅草 0.89–0.99、草皮 0.89–0.99、铁件 0.39–0.66、玻璃 0.10–0.23。完整分区、参数和通道记录在 `assets/resources/art/forest_village/houses_surface_maps_v3.json`。

十类 Godot 材质分别启用 `normal_texture` 与 `roughness_texture`，按材质调节法线强度；粗糙度采样发生在雨雪响应前，继续由天气覆盖。新增粗糙度采样开关默认关闭，仅这些房屋材质启用。Blender/GLB 使用相同粗糙度的 ORM 绿色通道和法线图；ORM 的红色固定 1，蓝色仅铁件为 1，不烘焙环境遮蔽。

## 文件位置

- GLB 与运行时网格：`assets/models/environment/forest_village/houses/`
- 可编辑 Blender 源文件：上述目录的 `source/forest_village_houses_v2.blend`，`source/.gdignore` 防止编辑器再次导入源文件。
- 可直接实例化场景：`assets/scenes/environment/forest_village/houses/`
- 十户模型展示场景：`assets/scenes/environment/forest_village/houses_gallery.tscn`
- 共享材质：`assets/materials/environment/forest_village/houses/`
- 模型、碰撞和四面特征清单：`assets/resources/art/forest_village/houses_models_v2.json`
- 导入、关卡接线与原生检查记录：同目录 `houses_import_validation.json`、`houses_level_integration.json`、`houses_runtime_validation.json`。

## 重建与关卡接线

先用 Blender 后台执行 `scripts/tools/art/forest_village_houses/bake_surface_maps.py` 烘焙材质数据图。`build_houses.py` 在 Blender 中定义模型，末尾加载 `detail_pass.py` 的 V3 结构、外观裁剪、面数优化和入口检查；加载后依次对 `BUILDERS` 中每个名字调用 `build(name)`，随后调用 `export_all()`、`save_source()`。它创建单独的场景和源文件，不重写原共享森林资产的 Blender 文件。

使用项目自编译 Godot 4.8 执行：

1. `--headless --path . --script res://scripts/tools/art/forest_village_houses/import_houses.gd`：导入 GLB，验证 UV/顶点色/面数，生成材质、LOD、碰撞和展示场景。
2. `--headless --path . --script res://scripts/tools/art/forest_village_houses/apply_houses.gd`：将十栋模型按住户一一替换既有 `Building` 实例，保持原房屋原点与朝向，保留住户和入口标记。所有模型局部位置、旋转均为零，不添加入口对齐或避树偏移；使用新模型制作时的关卡尺度。
3. 按需运行既有 `rebuild_flora.gd` 与 `rebuild_navigation.gd`，使地被避开新碰撞并重烘导航。道路本身与仓储间入口无需移动。

`build_village.gd` 的完整聚落生成流程已改用十栋新模型。独立应用脚本保留当前布局中的入口位置；完整布局生成器则按新模型的 `ApproachAnchor` 重新生成门前路径。

本次按原房屋位置替换后重建地被，使其避开补齐的门前平台与坡道碰撞，保留原密度、材质、风动和天气流程。

## 检查

- 既有 `validate_village.gd`：60 项检查通过，十户门前、井边、桥梁和东岸仍可达。
- `rebuild_navigation.gd`：2,567 个导航多边形，13 条连接通过。
- `validate_houses.gd`：以 Vulkan/Forward+ 原生渲染十栋的正、背、左、右及三分之四视图，检查材质、UV、顶点色、三角面、关卡树干相交和雨雪材质状态。使用普通渲染进程运行该脚本；`--headless` 仅适合解析检查，不生成有效渲染截图。
- 正式关卡重新加载后，十户模型一一对应，局部变换均为单位变换，原房屋原点与朝向检查通过，树干重叠为零。猎采户入口位于木阶，使用同尺寸 `CharacterBody3D` 胶囊体实测到达误差约 3 毫米且保持着地；通行检查按相邻支撑面高度差判断台阶，并计算坡面上的胶囊支撑高度，允许架高木廊。
- 临时截图和日志位于 `.godot/forest-village-houses/`，`overview.png` 为十户总览，`*_four_sides.png` 为各栋的四面图。

V3 保留原房屋位置、朝向和入口，补齐八户台阶至门槛间的平台，给东田农户增加院内门阶；坡道碰撞在平台前沿达到同一高度，避免猎采户的木阶与高木廊连接处卡住胶囊体。村长家门前柱移出门洞通道，右侧长凳缩短并让开门口；西田农户门前晾晒架移除，清开宽储物门；多处木桶、花盆、木堆和后侧公告板调整至墙外与门框之外。入口生成记录保存于各资产的 `entrance_audit`。

房体碰撞从各自楼层的实际 `base` 高度开始，杂货户二三层的挑出体量不再被碰撞体向下投影到地面，底层门口通道保持畅通。

`validate_houses.gd` 使用高 0.936 米、半径 0.25 米的 `CharacterBody3D`，从各栋自身的 `ApproachAnchor` 走到闭合门前，不使用跳跃；同时检查原生切线、法线/粗糙度贴图接线、外观预算和四面画面。近景原生截图为 `level_detail_chief.png`、`level_detail_carpenter.png` 和 `level_detail_farmer_west.png`。

这些检查针对静态外观、简化碰撞和现有世界入口，不构成室内交互或所有硬件的性能测试。V3 增加了近景几何量，本次未重新测量完整关卡帧耗时。
