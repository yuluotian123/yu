# 项目文档

Godot C# 游戏项目的统一文档入口。当前引擎与构建方式见[引擎构建与启动](tools/engine.md)。

## 项目与框架

| 文档 | 内容 |
| --- | --- |
| [项目架构](architecture/project.md) | 目录、模块、启动流程与开发入口 |
| [EC 框架](architecture/ec-framework.md) | GameObject / Component 生命周期、顺序与场景接线 |
| [编码约定](architecture/coding-patterns.md) | Godot C#、资源与模块集成 |
| [框架手册](framework/README.md) | 核心、事件、资源、配置、UI、对象池、流程与工具 |
| [图系统](graph/README.md) | 图编辑器、节点扩展、序列化与运行时 |
| [世界架构](architecture/world.md) | 状态归属、生命周期、关卡与离屏模拟 |

## 玩法与配置

| 文档 | 内容 |
| --- | --- |
| [角色系统](gameplay/characters.md)、[主角人物档案](characters/protagonist.md) | 角色组件、主角设定与完整动作预览 |
| [角色图](gameplay/character-graphs.md)、[角色数据](gameplay/character-data.md) | 输入、行为图与属性配置 |
| [世界与关卡](gameplay/world.md) | 对象、交互、入口、道路、角色名册与离屏模拟 |
| [NPC 与 AI](gameplay/npc-ai.md)、[居民日常示例](gameplay/npc-daily-goals.md) | Goal、Plan、Action、选择器及完整配置 |
| [能力](gameplay/abilities.md)、[武器与背包](gameplay/weapons.md) | Timeline、装备、攻击与物品实例 |
| [大剑架势切换](gameplay/combat-stances.md) | 三段直选、过渡、外置粒子与 Weapon/Ability 接口 |
| [输入](gameplay/input.md)、[HFSM](gameplay/hfsm.md) | 操作绑定与分层状态机 |
| [对话](gameplay/dialogue.md)、[任务](gameplay/missions.md)、[存档](gameplay/saves.md) | 对话内容、任务推进与持久化 |

## 美术与渲染

[美术制作入口](art/README.md)汇总场景、房屋、植被、角色与生成提示词。

| 文档 | 内容 |
| --- | --- |
| [相机系统](rendering/camera.md)、[HD-2D 相机](rendering/hd2d-camera.md) | 运行时架构与场景构图 |
| [DDGI](rendering/ddgi-implementation.md) | 引擎实现、场景接线、参数与验证 |
| [场景表面](rendering/town-rendering.md)、[后处理](rendering/relink-post.md) | 材质、光照、像素取样与颜色处理 |
| [双手大剑移动参考](art/greatsword-movement-references.md) | 持剑步法参考、三段移动约束与来源 |
| [天空与云](rendering/sky-clouds.md)、[昼夜](rendering/time-of-day.md)、[天气](rendering/weather.md) | 环境渲染与运行时驱动 |

## 开发工具与许可

- [配置编辑器](tools/config-editor.md)、[运行时调试面板](tools/runtime-gm.md)
- [架势资源清理与提交检查](tools/project-cleanup-2026-11-09.md)
- [Godot MCP 工具包](tools/godot-mcp/README.md)、[工具包配套技能](tools/godot-mcp/skills/)
- [项目许可中文说明](licenses/project-LICENSE.zh-CN.md)、[Godot MCP 许可](licenses/godot-mcp-LICENSE)、[第三方归属](licenses/godot-mcp-attributions.md)

## 维护约定

项目说明、模块手册、工具指南与提示词统一放在 `docs/`，从本索引或对应目录索引链接，不在源码和资源目录重复维护。许可证原文集中在 `docs/licenses/`。

临时日志、截图、验证脚本与生成中间文件使用被忽略的 `.godot/<任务名>/`，完成后清理。保留仍使用的美术源文件、Godot 导入缓存和构建依赖；代码删除前检查动态加载、图节点注册与制作工具引用。

项目自身文档统一使用简体中文，代码标识符、命令和路径保持原样；MCP 配套文档按原版保留。主角需求与改动统一维护在[主角人物档案](characters/protagonist.md)，跳跃只展示完整动作。
