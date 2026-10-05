黑发兽耳大剑主角：游戏动画资源

Player 使用 sprite_frames.tres；所有帧画布为 192x192，角色原点为 (96,168)。
配合 player.tscn 的 Sprite position=(0,-36)，脚底位于原碰撞体底部 y=36。
使用最近邻纹理过滤，保持原有 36x72 碰撞体、控制器和动画状态机。

兼容原有动画名：idle、run、jump、jumpup、inair、isfalling、land、attack、dash。
另外提供 hurt、death；目前项目没有受击/死亡玩法触发器，可由 CharacterAnimationComponent2D.RequestAnimation 发起高优先级请求，恢复时 ClearAnimationRequest。
死亡动画不循环，停留末帧。不要每帧重启同一个请求。

jumpup 使用起跳帧，inair 使用空中收腿帧，isfalling 使用伸腿空中帧，land 使用落地和待机帧。由现有 HFSM 按物理状态切换。
attack 四个关键帧时长为 0.08/0.07/0.09/0.10 秒，总长 0.34 秒，与当前攻击技能时间一致。
dash 复用奔跑中的腾空前冲姿态，以原冲刺技能的 1.25 倍速度播放；尚未新增专门绘制的冲刺图集。

sources/ 保存六组 imagegen 原始透明图集。构建脚本按 alpha 连通区域提取角色，而非简单四等分，避免剑和头发跨格造成串帧。
scripts/tools/build_greatsword_sprites.py --project <项目路径> 可以从原始图集重建 atlas.png、sprite_frames.tres 和 alignment.json，需要 Pillow。
alignment.json 记录每帧原始区域、缩放与锚点。源图关键姿势有绘制差异，若需要更流畅的动作，应继续补绘中间帧。

验证命令：Godot --headless --path . --script res://scripts/test/greatsword_animation_smoke.gd
已有角色/技能行为测试：Godot --headless --path . res://assets/scenes/character_graph_runtime_smoke.tscn
