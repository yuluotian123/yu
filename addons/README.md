# Addons

项目当前启用两个 Godot C# 编辑器插件。

## 插件列表

- [ConfigPlugin](ConfigPlugin/README.md)：把 xlsx 配置表转换为运行时 JSON 和生成 C# 类型。
- [GraphPlugin](GraphPlugin/README.md)：通用图编辑器与 Flow、State、Behavior Tree 运行时框架。

## 开发约定

- 编辑器入口代码使用 `#if TOOLS`，运行时类型不要依赖 `EditorPlugin` 或编辑器控件。
- 插件必须支持禁用、重新启用和 C# 扩展热重载。
- 修改 `plugin.cfg`、脚本类名或资源路径时检查 Godot `.uid` 和序列化引用。
- 插件 README 必须区分当前实现、历史兼容和规划功能。

