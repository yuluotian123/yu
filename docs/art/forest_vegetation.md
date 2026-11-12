# 森林植被：角色配套版

2026-11-04 的正式关卡使用以人物为基准的像素材质。墙、瓦、木、石、树皮和草甸共用九格笔触原稿，运行图集在 `assets/textures/environment/forest_village/character_style/`。草甸以八米平铺的阶梯色块为底，沿石路烘焙窄土肩，5,531 丛立体草集中于草甸色斑，草根采样同一张线性地表图。三种灌木已缩小，主枝和侧枝组成错层叶扇。正式森林的 3D Film 阶段按整数像素块取平均，UI 在该阶段之后绘制。

石路边的小尖草来自 `GroundContact/Grass_*`，120 个网格块共用 `ground_flora/verge.tres`。阔叶草丛在 `GroundFlora` 下标为 `rosette`。两者按世界位置采样草甸或土肩的底色，双面叶片使用朝上的受光法线。野花也使用双面受光，花瓣颜色来自原网格。重新生成村落后运行 `scripts/tools/art/forest_style/bind_materials.py` 可恢复这些材质设置。

2026-11-03 在 Blender 5.2.3 LTS 中重新制作八种独立植被，Godot 4.8 原生导入。森林聚落的三种乔木和三种灌木已切换为此套资源，274 棵树与 488 丛灌木沿用原摆放、分组和碰撞。原村落场景只更换六处 PackedScene 路径，其余字节不变；旧森林模型仍保留。

风格对照当前狐耳少女的大剑待机图 `assets/textures/sprites/amber_fox_style_v2/greatsword_v4/sources/guard-keyframe-256.png`：暖褐树干、黄绿受光面、低饱和青绿暗面，减少逐叶随机配色与细碎叶脉。叶簇轮廓保留间隙，大块明暗由体积法线和项目原生光照共同产生。人物保持现有精灵与像素采样，植被保持三维模型。

## 参考方式与插件

- [再来一瓶辣椒酱：风格化灌木教程](https://www.bilibili.com/video/BV1Knsd6eEKu/)：根据公开简介与预览分镜确认其使用几何节点散布、噪波风动、主体法线传递、材质配色和透明裁切。画面中未发现制作效果必须使用的第三方植被插件。并未获取作者工程，也没有逐帧观看全部视频。
- [球形法线树视频](https://www.bilibili.com/video/BV1HPnU64EBZ/)及作者公开的 [procedural-tree 说明](https://github.com/nicoo-474/procedural-tree)：分枝分组、叶簇共享球心法线、浏览器程序化建树并导出 GLB；它不是 Blender 插件。

本套资产独立编写 Blender Python 生成器，没有复制第三方网格、贴图或源代码。参考的是体积法线、分枝组织和破碎轮廓的制作原理，不依赖外部生成服务或第三方植被插件。Blender 源文件保存为已实现的可编辑网格，参数和种子保存在生成脚本中，不包含视频作者的几何节点组。

## 独立模型

| 模型 | 内容 | 三角面 | 目标高度 |
| --- | --- | ---: | ---: |
| `oak_broad` | 宽冠橡树 | 6872 | 14.8 m |
| `hornbeam_tall` | 高冠分枝树 | 6872 | 17.2 m |
| `oak_sweeping` | 弯干舒展树 | 6116 | 11.4 m |
| `hazel_round` | 错层榛木叶扇 | 1208 | 1.19 m |
| `hazel_tall` | 开叉竖向灌丛 | 1208 | 1.78 m |
| `woodland_low` | 低伏林缘灌丛 | 906 | 0.54 m |
| `oak_sapling` | 林缘幼树 | 3344 | 3.25 m |
| `fern_fan` | 羽状蕨 | 1278 | 约 0.55 m |

高度为独立模型尺寸，村落中继续使用原实例缩放。所有模型底部原点为零；乔木的原树干碰撞结构继续沿用。幼树和蕨类作为新增素材交付，未额外散布到正式关卡。

源码位于 `scripts/tools/art/forest_vegetation/build_vegetation.py`。每个枝冠按椭球梯度生成自定义角点法线，再混入 18% 整体树冠方向；法线烘入 GLB，Godot 材质的 `normal_roundness=0` 保留它们。低频冠形重叠形成连续树冠，枝梢叶卡保留疏松外缘。主干使用连续输运截面和低矮根盘，蕨叶有连接地面的叶轴。

当前四格 512×512 RGBA 叶簇使用折角叶尖、两段叶色和透明空隙。贴图位于 `assets/textures/environment/forest_village/character_style/sprays.png`，Godot 使用带 mipmap 的原生资源，GLB 和 Blender 文件也携带贴图。乔木和灌木共用体积法线材质，叶卡两面的法线方向一致。

## 文件与运行时

- Blender 源文件：`assets/models/environment/forest_vegetation/source/forest_vegetation.blend`。
- 八份 GLB 与原生网格：`assets/models/environment/forest_vegetation/`。
- 独立模型场景、总览场景 `vegetation_gallery.tscn` 与人物对照 `character_style_review.tscn`：`assets/scenes/environment/forest_vegetation/`。
- 材质：`assets/materials/environment/forest_vegetation/`，复用 `leaf_article.gdshader` 与 `town_toon.gdshader`，沿用项目天气和遮挡渐隐协议。
- 元数据、导入检查和切换记录：`assets/resources/art/forest_vegetation/`。

原生 ImporterMesh 生成距离 LOD，树皮表面为 4–5 层附加 LOD，叶卡表面为 1 层；蕨类为 2/1 层。所有模型完整细节少于 8000 三角面。树叶采用深度裁切、原生阴影和轻微风动，没有使用透明混合排序。

导入工具为 `import_vegetation.gd`，重新导入时复用当前场景的碰撞和元数据。`forest_village_whitebox/build_village.gd` 与验证器直接引用现有植被，不再需要一次性路径迁移脚本。

复建顺序：Blender 后台运行 `build_vegetation.py`；Godot headless 运行 `import_vegetation.gd`。`review_vegetation.gd` 在原生 Vulkan 中生成总览与人物同框预览。渲染预览、日志和一次性检查放在忽略目录 `.godot/vegetation_v5/`。

导入检查逐表面核对顶点色、UV、单位长度法线、三角面预算与 LOD；现有村落通行验证 60/60 通过。渲染总览与人物对照已目视检查。完整森林场景的原生验证检查：274 棵树全部来自新预制体、制作法线保留、叶片风动启用，雨天含水量与雪天积雪参数绑定通过。当前引擎与 API 版本按[引擎说明](../tools/engine.md)配置，避免使用旧版 GodotSharp 包造成 `GetDdgiSky` 方法缺失。此套资源的美术细节由项目实际灯光与曝光决定，独立总览使用自己的检视灯光。
