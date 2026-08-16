# Framework

`scripts/framework` 保存可复用框架能力。业务层通过公开接口获取模块，不直接依赖内部实现类。

## 模块索引

- [Core](core/README.md)：ModuleSystem、模块生命周期和帧更新。
- [Config](config/README.md)：配置表加载、缓存、查询与生成链。
- [Event](event/README.md)：同步类型化委托事件。
- [FSM](fsm/README.md)：通用有限状态机。
- [Object Pool](pool/README.md)：C# 对象池和 Godot Node 池。
- [Procedure](procedure/README.md)：基于 FSM 的游戏主流程。
- [Resource](resource/README.md)：资源句柄、异步加载、缓存和 profiler。
- [UI](ui/README.md)：窗口、Widget、层级和自动绑定。
- [Settings](settings/README.md)：框架设置资源。
- [Utility](utility/README.md)：时间、JSON、日志和通用键类型。

## 基本用法

```csharp
IResourceModule resources = ModuleSystem.GetModule<IResourceModule>();
IEventModule events = ModuleSystem.GetModule<IEventModule>();
```

新模块遵循 `IXxxModule -> XxxModule` 命名规则，并在项目退出时完整释放事件、句柄和静态状态。

跨模块优化事项见 [`docs/OPTIMIZATION_ROADMAP.md`](../../docs/OPTIMIZATION_ROADMAP.md)。

