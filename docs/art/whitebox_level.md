# 白盒测试关卡

当前游戏预加载入口为 `assets/scenes/levels/whitebox_test.tscn`。`assets/scenes/spacelevel.tscn` 保留原场景 UID，作为同一白盒关卡的兼容入口。

关卡几何只有一块 40 × 30 米的灰色平地，厚 1 米，顶面位于 Y=0，配置原生 StaticBody3D 碰撞和平面导航。场景保留现有玩家、相机、昼夜天气环境、Runtime GM 和两名 NPC。角色行为仍由 GameObject3D / Component3D 组成。

Resident 和 Courier 的生产 AI 图只保留人物根节点、感知、记忆、决策预算。旧 Goal、Plan、Action、目标选择器、已知交互点、home 参数及访问计数已移除。NPC 无目标时正常待机，GM 显示原因。

旧小镇场景、riverside_life、烘焙导航、专用模型、材质、贴图与湖水 Shader 已删除。可复用的表面和植被 Shader 保留原路径及 UID，继续供天气和 DDGI 使用。

测试场景、自动验证与截图脚本已移除。直接运行本白盒关卡，通过 Runtime GM 查看角色、世界和渲染状态。
