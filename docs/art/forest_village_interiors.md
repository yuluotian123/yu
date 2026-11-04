# 森林聚落室内与正门

正式森林关卡的十户房屋现在都能从正门进入独立室内关卡。在室内走向“出门”标记，会返回本户楼梯外的平地；返回点与触发区分开，避免自动反复进出。后侧不开设通路。

杂货与待客户沿用原 `world_workshop.tscn`、关卡 ID、地点 ID、传送门身份和仓储对象身份，升级为带起居区的室内仓储。两处储物点与水瓶仍可使用。原先位于院落接近点的宽触发箱改为对准实际 `DoorAnchor` 的 0.82 × 1.65 × 0.36 米 `Area3D`；它只检测角色，不添加实体阻挡。其余九户使用同样的正门接线方式。各户的装饰家具包含简化碰撞；未给床、炉台等新增生活交互。

| 室内 | 布置 | 模型三角面 |
| --- | --- | ---: |
| 村长家 | 议事桌、账册架与家庭起居 | 16,449 |
| 杂货与待客户 | 柜台、货架、床位与原有仓储交互 | 21,705 |
| 工具修补户 | 石地、炉台、铁砧与工具架 | 25,341 |
| 木工户 | 木板围护、工作台与木料 | 21,537 |
| 草药照护户 | 草药瓶、晾晒束、调配台与照护床 | 32,979 |
| 伐木户 | 原木围护、斧锯与柴堆 | 23,985 |
| 猎采户 | 猎采架、晾晒物与采集篮 | 19,989 |
| 东田农户 | 备餐桌、粮袋与蔬菜篮 | 26,113 |
| 西田农户 | 家庭桌、织架与谷物储备 | 20,805 |
| 小畜养户 | 蜂蜜罐、蜂框与饲料箱 | 27,105 |

室内使用与外观相同的手绘图集、顶点色、2048×2048 切线空间法线和粗糙度图，总计 236,008 三角面，按关卡分别加载。采用屋顶与前墙剖开的主层室内，保留完整边界碰撞；后墙、窗框、炉台、柜架和床具均为可编辑模型。各室内保存独立导航、暖炉光、窗边补光及跟随镜头。室内不挂接室外降水表面组件。

十处楼梯使用连续的隐藏缓坡碰撞，视觉台阶保留。坡脚埋入地面 4.5 厘米，避免当前玩家方形碰撞体卡在薄尖边；架高猎采屋另设与木廊重叠的平缓坡顶。猎采户的旧院外标记曾落在半坡上，现移到楼梯外。修复由共享 `entrance_slopes.gd` 生成，房屋导入器也会复用它。

世界登记继续使用 `GameObject3D`、`WorldLevelContextComponent3D`、`WorldSpawnPointComponent3D` 与 `WorldTransitionComponent3D`。默认世界共 11 个关卡、20 条自动传送路线；通过既有 `WorldAuthoringCompiler` 生成目录与身份，不手写生成目录。

文件位置：

- 室内模型库与源文件：`assets/models/environment/forest_village/interiors/forest_village_interiors.glb`，以及 `source/forest_village_interiors.blend`。GLB 中十户按网格排开展示；运行时使用各户独立的 `.mesh.res`。
- 纯室内环境：`assets/scenes/environment/forest_village/interiors/`。
- 九个新增关卡：`assets/scenes/levels/forest_village_interiors/`；杂货户仍为 `assets/scenes/levels/world_workshop.tscn`。
- 新关卡与地点定义：`assets/resources/world/levels/forest_house_*.tres`、`assets/resources/world/locations/forest_house_*.tres`。
- 室内导航：`assets/resources/world/navigation/forest_village_interiors/`。
- 制作、接线和验证工具：`scripts/tools/art/forest_village_interiors/`。

重建顺序：

1. Blender 后台执行 `build_interiors.py`。
2. C# 构建后，用项目 Godot 执行 `--headless --path . --script res://scripts/tools/art/forest_village_interiors/repair_entrance_slopes.gd`。
3. 执行 `build_scenes.gd`，导入模型、保存十个可运行室内、配置正门并更新世界目录。杂货户的原有交互对象与身份保留。
4. 执行既有 `forest_village_whitebox/rebuild_navigation.gd`。完整森林关卡替换工具也调用同一入口库，重建后继续保留室内接线。

仅刷新十户室内寻路时执行 `res://scripts/tools/art/forest_village_interiors/rebuild_navigation.gd`。它复用房屋导航烘焙函数，只保存各关卡当前引用的导航资源，并检查出生点至出口、房间中央及仓储交互点的连通性。

`probe_entrance_slopes.gd` 使用与实际玩家相同的 0.468 × 0.936 × 0.468 米方形碰撞体，逐户走上楼梯，不发出跳跃指令。`validate_interiors.gd` 使用真实世界会话和玩家运动组件，从院外沿楼梯进入、在室内移动，再自动返回本户院落；同时检查落点不误触、导航、法线和原仓储对象。`render_interiors.gd` 输出十户室内总览。截图和日志保存在 `.godot/forest-village-interiors/`，最终往返结果保存在 `assets/resources/art/forest_village/interiors_validation.json`。

本轮验证：193 项室内运行检查、十户真实自动往返、十处方形角色无跳跃上楼检查全部通过；既有村落通行检查为 60/60，森林导航重烘后 2,496 个多边形、13 条连接通过。C# 构建 0 错误，8 个既有警告。
