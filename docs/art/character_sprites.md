# 角色美术资源

当前 Player 使用湖畔剑士的 52 张动作画稿：待机 4、跑步 8、攻击 16、跳跃 16、受伤 4、倒地 4。角色设计参考位于 [lakeside_v4](../../assets/textures/concepts/protagonist/lakeside_v4/character-design.png)。

- 原稿与运行图集：[贴图目录](../../assets/textures/sprites/lakeside_greatsword/)。原稿放在 `sources/`，运行图集为 `atlas.png`。
- 动画及锚点记录：[资源目录](../../assets/resources/sprites/lakeside_greatsword/)。`sprite_frames.tres` 被 [Player 场景](../../assets/scenes/player.tscn) 引用；`alignment.json` 保留逐帧裁切和锚点信息。
- 旧版 greatsword 原稿、图集和动画分别保存在同名贴图及资源子目录，供后续美术使用。

图集为 4096×3584，每帧画布 512×512，脚底锚点 (256,448)。AnimatedSprite3D 使用 `pixel_size=0.0025`、子节点缩放 1.3；碰撞尺寸为 (0.468,0.936,0.468)。纹理无损导入、无 mipmap，角色使用线性采样与 alpha 裁切。

动画名保持 idle/run/attack/jumpup/inair/isfalling/land/dash/hurt/death；attack 总长 0.6 秒，与 [攻击时间轴](../../assets/resources/abilities/attack_timeline.tres) 同步。具体帧序及速度以 SpriteFrames 资源为准，空中阶段跟随角色物理状态。

需要从保留原稿重建图集时，执行 [build_lakeside_sprites.gd](../../scripts/tools/build_lakeside_sprites.gd)：

```powershell
godot --headless --path . --script res://scripts/tools/build_lakeside_sprites.gd
```

脚本分别输出贴图到 `assets/textures/sprites/lakeside_greatsword/`、动画和锚点到 `assets/resources/sprites/lakeside_greatsword/`，临时预览位于忽略的 `.godot/art-validation/sprites/`。
