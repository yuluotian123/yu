# Framework Core

`scripts/framework/core` 提供项目框架模块的统一生命周期。业务代码通过模块接口获取实例，`ModuleSystem` 按约定定位实现、懒创建、初始化、轮询并在退出时反向关闭。

## 核心类型

- `Module`：模块实现基类，定义 `Priority`、`OnInit()` 和 `Shutdown()`。
- `IProcessModule`：需要每帧更新的模块接口。
- `ModuleSystem`：模块注册、获取、更新和关闭入口。

## 获取模块

模块默认遵循 `IExampleModule -> ExampleModule` 命名规则，并位于同一命名空间：

```csharp
IResourceModule resources = ModuleSystem.GetModule<IResourceModule>();
IEventModule events = ModuleSystem.GetModule<IEventModule>();
```

`GetModule<T>()` 只接受接口。首次请求时创建实现并调用 `OnInit()`，后续返回同一实例。

自定义实现无法按命名约定发现时，可以显式注册：

```csharp
ModuleSystem.RegisterModule<IMyModule>(new MyModule());
```

## 生命周期

项目入口负责驱动模块系统：

```csharp
public override void _Process(double delta)
{
    ModuleSystem.Process(delta, delta);
}

public override void _ExitTree()
{
    ModuleSystem.Shutdown();
}
```

- `Process()` 按 `Priority` 顺序更新所有 `IProcessModule`。
- 新增或移除更新模块后，执行列表会延迟重建。
- `Shutdown()` 按成功初始化顺序的反向关闭并清空静态状态，某个模块关闭失败不会阻断其余模块清理。

## 新增模块

1. 定义公开接口 `IXxxModule`。
2. 创建继承 `Module` 的 `XxxModule` 实现。
3. 如需帧更新，实现 `IProcessModule`。
4. 在 `OnInit()` 中建立内部状态，在 `Shutdown()` 中释放引用和事件。
5. 业务层只依赖接口，不直接 `new` 模块实现。

## 当前注意事项

- 自动发现依赖接口与实现的名称、命名空间和程序集匹配；其他实现使用显式注册。
- 模块采用懒初始化，跨模块依赖顺序由首次调用隐式决定。
- `OnInit()` 成功后才注册；失败时清理候选实例及此次新建依赖，保留此前已注册模块。循环初始化会明确报错。
- 同一实例重复注册不会重复初始化，不同实例占用同一接口或实现类型会被拒绝。
- 关闭与回滚期间可访问尚未关闭的模块，但不能懒创建新模块。
- 静态全局入口便于使用，但会增加测试隔离和依赖追踪成本。
- 所有模块都必须支持 `Shutdown()` 后重新初始化。

