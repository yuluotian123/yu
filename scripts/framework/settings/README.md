# Framework Settings

`Settings` 是框架配置资源入口，目前聚合 `ResourceSetting` 和 `ConfigSetting`，供项目场景或启动代码统一配置资源与配置表模块。

## 字段

- `resourceSetting`：资源缓存、并发加载和 profiler 配置。
- `configSetting`：配置表运行时目录。

## 使用方式

在 Godot 中创建 `Settings` Resource，配置子资源后由项目入口加载并传给对应模块。业务系统不应直接修改共享设置资源。

## 当前注意事项

- 字段使用小写命名，与项目其他公开 C# 属性风格不一致。
- 当前没有统一的 Settings 加载和校验入口。
- 新增设置项时应明确默认值、平台差异和运行时是否允许修改。
- 资源路径、缓存大小和并发数应在启动时验证并输出明确错误。

