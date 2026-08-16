# Procedure Module

Procedure Module 基于通用 FSM 管理游戏主流程，例如预加载、主菜单和关卡。每个 Procedure 是绑定到 `IProcedureModule` owner 的 `FsmState<IProcedureModule>`。

## 核心类型

- `IProcedureModule`：初始化、启动、查询和重启流程。
- `ProcedureModule`：持有底层 FSM 并转发流程操作。
- `ProcedureBase`：所有流程状态的基类。

## 快速开始

```csharp
public sealed class MainMenuProcedure : ProcedureBase
{
    protected override void OnEnter(IFsm<IProcedureModule> fsm)
    {
    }
}
```

```csharp
IFsmModule fsms = ModuleSystem.GetModule<IFsmModule>();
IProcedureModule procedures = ModuleSystem.GetModule<IProcedureModule>();

procedures.Initialize(
    fsms,
    new PreloadProcedure(),
    new MainMenuProcedure(),
    new LevelProcedure());

procedures.StartProcedure<MainMenuProcedure>();
```

## 生命周期

- `Initialize()` 创建 Procedure 专用 FSM，只能在启动前调用。
- `StartProcedure<T>()` 进入指定流程。
- Procedure 内通过 FSM 状态切换进入下一流程。
- `RestartProcedure()` 销毁旧 FSM，并以传入列表中的第一个 Procedure 重新启动。
- 模块关闭时由 `FsmModule` 统一销毁状态机。

## 使用约定

- Procedure 只负责顶层阶段切换，不承载具体玩法系统实现。
- 每个流程在退出时取消事件、释放临时资源并关闭专属 UI。
- 流程依赖的模块应在入口处明确获取，避免深层逻辑隐式初始化模块。
- 需要保存的游戏状态放入专门的数据或存档系统，不存放在 Procedure 实例字段中。

## 当前注意事项

- `Initialize()` 前调用查询或启动方法会抛出通用异常。
- Procedure 与 `IFsmModule` 存在显式初始化顺序要求，应由 RootModule 统一编排。
- `RestartProcedure()` 先销毁旧 FSM，再创建新 FSM；中途失败没有回滚。
- 缺少流程切换历史、耗时统计和自动化测试。

## 相关文档

- [`scripts/framework/fsm/README.md`](../fsm/README.md)

