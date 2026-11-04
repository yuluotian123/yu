# 森林聚落栅栏与木桥

正式森林关卡共享的聚落环境已替换 24 段院落、生产区栅栏和一座溪流木桥。使用与十户房屋相同的暖褐旧木、浅木、苔灰石和暗铁配色及手绘材质图集，保留森林中的原生光照、DDGI、雨雪和相机材质协议。

栅栏由带斜切帽的倒角木柱、两道横梁、交替方向的斜撑和方形铁钉组成。木柱每个 216 三角面；两种两米栏片各 1,188 三角面，按原始端点分段缩放。木纹沿构件长轴展开，底部以顶点色混入少量苔绿。院落开口、两道简化横栏碰撞及原有端点保持一致。

木桥近景为 16,848 三角面，包含 23 块独立桥板、三根贯通底梁、横向承台、岸侧石座、斜撑扶栏、木扶手与铁钉。桥面占地仍为 5.85 × 3.276 米，中心为 `(28.08, -0.11, 2.34)`，通行表面高度为 0.015 米；桥梁实例根节点位于 `(28.08, 0, 2.34)`。连续简化碰撞承载角色，细小板缝仅作视觉表现，河道中部不增加桥墩。

四种材质复用房屋的 `painterly_house_atlas_v3` 底色、2048×2048 OpenGL +Y 切线空间法线及粗糙度图。木材/石材法线强度为 0.55，铁件为 0.28；木材保持吸水变暗与柔和反光，积雪覆盖处由既有天气着色器减弱细节法线。所有网格具有有效 UV、顶点色、法线和切线，使用 Godot 原生 LOD。源 GLB 内也包含相同材质数据，运行时网格共用项目 `.res` 贴图。

文件位置：

- 模型、运行时网格与 Blender 源文件：`assets/models/environment/forest_village/structures/`；源文件在 `source/forest_village_structures.blend`，可按对象名分别编辑。
- 可实例化模型：`assets/scenes/environment/forest_village/structures/`。
- 共享材质：`assets/materials/environment/forest_village/structures/`。
- 制作、导入、替换和验证脚本：`scripts/tools/art/forest_village_structures/`。
- 模型清单、接线记录和验证结果：`assets/resources/art/forest_village/structures_*.json`。

重建顺序：

1. Blender 后台执行 `build_structures.py`，输出四个 GLB 和独立源文件。
2. 项目 Godot 执行 `--headless --path . --script res://scripts/tools/art/forest_village_structures/import_structures.gd`，生成共享材质、LOD 网格和预制场景。
3. 执行 `apply_structures.gd`，按保存的端点替换栅栏，并替换桥梁实例；可重复执行。只更新聚落中的这些结构。
4. 执行既有 `forest_village_whitebox/rebuild_navigation.gd`，刷新导航并检查 13 条连接。完整 `build_village.gd` 已调用同一结构装配库，重建布局时继续使用正式模型。

普通 Vulkan 渲染进程执行 `validate_structures.gd`，检查独立模型、桥底、实际森林中的栅栏与桥梁、固定光照下的法线开关对照、场景独立天气材质和雨雪状态，并用角色同尺寸胶囊体双向走过木桥。截图位于 `.godot/forest-village-structures/`；原生 `validate_village.gd` 的 60 项通行检查通过，重烘后导航含 2,567 个多边形，13 条连接通过。C# 构建 0 错误，8 个既有警告。
