# 美术与渲染文档

当前主线为 PC / Godot 4.8 Forward+：手绘场景材质、自研 DDGI、昼夜与天气、Relink 后处理。

| 文档 | 内容 |
| --- | --- |
| [DDGI 接入与维护](ddgi_implementation.md) | Spacelevel 接线、参数、源码归属、支持范围与验证 |
| [小镇卡通渲染](town_rendering.md) | 直接光、PBR / Toon 材质、植被、阴影与物理强度 |
| [Relink 后处理](relink_post.md) | 捕获依据、HDR LUT / Film / Bloom 顺序及参数 |
| [天空与云](sky_clouds.md) | 原始资源、移植边界、云层与辐照输入 |
| [HD-2D 相机](hd2d_camera.md) | 场景相机、投影与跟随 |
| [角色精灵](character_sprites.md) | 精灵资源与显示约定 |

系统维护入口：[引擎构建与启动](../../scripts/tools/engine/README.md)、[昼夜](../../scripts/gamelogic/timeofday/README.md)、[天气](../../scripts/gamelogic/weather/README.md)、[Runtime GM](../../scripts/gamelogic/gm/README.md)。旧 GI 设计与源码评估已合并到 DDGI 指南；RTX 分支历史统一见引擎说明。
