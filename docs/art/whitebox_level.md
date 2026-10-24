# 白盒测试关卡

`assets/scenes/levels/whitebox_test.tscn` 是基础白盒场景。正式游戏入口为 `assets/scenes/levels/forest_town_level.tscn`，由世界目录加载。

关卡几何只有一块 40 × 30 米的灰色平地，厚 1 米，顶面位于 Y=0，配置原生 StaticBody3D 碰撞和平面导航。场景保留现有玩家、相机、昼夜天气环境、Runtime GM 和两名 NPC。角色行为仍由 GameObject3D / Component3D 组成。

白盒引用的基础图只保留人物根节点、感知、记忆、决策预算。正式小镇为 Resident 覆盖世界组件并绑定 daily_goals.tres，配置了跨关卡喝水和取货；见 [完整示例](../../doc/NPC_DAILY_GOALS_EXAMPLE.md)。未配置目标的角色正常待机，GM 显示原因。

旧小镇场景、riverside_life、烘焙导航、专用模型、材质、贴图与湖水 Shader 已删除。可复用的表面和植被 Shader 保留原路径及 UID，继续供天气和 DDGI 使用。

测试场景、自动验证与截图脚本已移除。直接运行本白盒关卡，通过 Runtime GM 查看角色、世界和渲染状态。
