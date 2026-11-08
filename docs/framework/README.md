<a id="framework"></a>
# 框架

`scripts/framework` 保存可复用框架能力。业务层通过公开接口获取模块，不直接依赖内部实现类。

## 模块索引

- [核心](core.md)：ModuleSystem、模块生命周期和帧更新。
- [配置](config.md)：配置表加载、缓存、查询与生成链。
- [事件](events.md)：同步类型化委托事件。
- [有限状态机](fsm.md)：通用有限状态机。
- [对象池](pooling.md)：C# 对象池和 Godot Node 池。
- [流程](procedures.md)：基于 FSM 的游戏主流程。
- [资源](resources.md)：资源句柄、异步加载、缓存和性能分析。
- [界面](ui.md)：窗口、Widget、层级和自动绑定。
- [设置](settings.md)：框架设置资源。
- [工具](utilities.md)：时间、JSON、日志和通用键类型。

## 基本用法

```csharp
IResourceModule resources = ModuleSystem.GetModule<IResourceModule>();
IEventModule events = ModuleSystem.GetModule<IEventModule>();
```

新模块遵循 `IXxxModule -> XxxModule` 命名规则，并在项目退出时完整释放事件、句柄和静态状态。

模块职责和接入方式见 [项目总览](../architecture/project.md) 及各模块文档。
