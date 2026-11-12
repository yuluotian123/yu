# 项目清理与工具维护

运行资源依赖应同时检查 Godot 的 `ResourceLoader.GetDependencies`、文本路径、动态拼接路径、世界目录和制作工具。二进制 `.res` 内的依赖、按目录生成的世界目录、图节点注册以及制作源文件不能仅凭文本搜索判定未使用。

## 当前保留入口

- 引擎与构建：`scripts/tools/engine/launch_godot.ps1`，参数及包准备见[引擎说明](engine.md)。
- DDGI、可见性、植被、资源回收与镜头穿入：`scripts/tools/rendering/` 中对应的维护验证器，按各[渲染手册](../README.md#美术与渲染)运行。
- 退出检查点：`scripts/tools/gameplay/validate_world_exit.gd`；编辑器保存目录缓存：`assets/scenes/tests/world_save.tscn`。
- 武器/架势：`assets/scenes/tests/weapon_stances.tscn`；资源像素与导入检查见 `scripts/tools/art/validate_stance_*.gd`。
- 美术重建：保留当前 Blender、Aseprite、视频、提示词、工作流与对应生成工具，入口见[美术说明](../art/README.md)。

## 2026-11-12 清理

扫描 4,388 个跟踪文件，使用 Godot 原生依赖扫描核对 1,220 个资源。删除 26 个跟踪文件：

- 18 个已合并到植被分块、没有资源消费者的蕨/花/阔叶地被模板网格。生成工具改为只在内存中保留这些模板；草的 MultiMesh 共享网格继续保存。
- 已被后续版本替换的旧地表图集，以及房屋材质未使用的 ORM `.res` 副本。Blender 制作仍使用的 ORM PNG 保留；Godot 导入器不再生成无用副本。
- 已被三段移动预览替换的旧跑步 GIF，同步移除预览清单条目及生成分支。
- 旧正交镜头阴影对照、旧构图节点光束对照两个一次性工具及其 UID；当前验证使用真实主场景与维护中的渲染检查。
- 过时的单次清理提交记录，由本维护页替代。

同时清理 5 个已完成任务的临时目录，共计删除 425 个文件、458,138,367 字节（约 437 MiB）。保留 `.godot/imported`、Mono、NuGet、着色器缓存及当前 DDGI/退出问题的诊断记录。详细清单在本地 `.godot/shadow-cleanup-20261112/removed-files.json`。

同步修正森林相机的 FOV、偏移、俯角与构图节点说明，更新 world/session 存档版本，移除已失去测试夹具的旧性能表。历史崩溃诊断及主角尚未完成的需求继续保留。

清理后验证：Godot 编辑器导入成功；1,201 个序列化资源原生依赖检查无缺失，本地 Markdown 链接无断链；18 个植被模板在内存中生成并合并分块通过，现有村落 361 个 MultiMesh 与 544 个 Mesh 分块加载通过；C# 构建和完整游戏从主菜单进入、移动、跳跃、落地及退出通过。完整游戏退出仍有原先的 3 个 ObjectDB 实例泄漏警告，本轮未解决该独立问题。日志与检查脚本保存在 `.godot/shadow-cleanup-20261112/`。
