# FSM Module

FSM Module 是项目通用有限状态机实现，为 Procedure 和其他非图状态流程提供状态注册、切换、数据共享和统一帧更新。

## 核心类型

- `IFsmModule` / `FsmModule`：创建、查询、更新和销毁状态机。
- `IFsm<T>` / `Fsm<T>`：绑定一个 owner 的状态机实例。
- `FsmState<T>`：状态基类。
- `FsmBase`：非泛型查询与模块管理基类。

## 快速开始

```csharp
public sealed class IdleState : FsmState<CharacterController>
{
    protected override void OnEnter(IFsm<CharacterController> fsm)
    {
    }

    protected override void OnProcess(
        IFsm<CharacterController> fsm,
        double elapseSeconds,
        double realElapseSeconds)
    {
    }
}
```

```csharp
IFsmModule fsmModule = ModuleSystem.GetModule<IFsmModule>();
IFsm<CharacterController> fsm = fsmModule.CreateFsm(
    owner,
    new IdleState(),
    new MoveState());

fsm.Start<IdleState>();
```

状态内部通过 `ChangeState<TState>(fsm)` 切换，也可以使用 FSM 数据接口在状态间共享短期数据。

## 生命周期

1. `FsmModule.CreateFsm()` 注册 owner 类型和可选名称。
2. `Start<TState>()` 进入初始状态。
3. `FsmModule.Process()` 更新所有运行中的 FSM。
4. `DestroyFsm()` 关闭状态并释放数据。
5. `FsmModule.Shutdown()` 销毁全部 FSM。

## 使用约定

- 同一 owner 类型下需要多个 FSM 时必须提供不同名称。
- 状态实例属于一个 FSM，不应跨 FSM 复用有状态实例。
- 状态切换逻辑放在状态或明确的协调器中，不要从任意系统直接控制。
- 图驱动角色状态优先使用 `scripts/gamelogic/hfsm`；本模块适合框架流程和简单状态机。

## 当前注意事项

- 参数和状态错误普遍使用通用 `Exception`，建议改为更明确的异常或 `Try*` API。
- 单文件实现较大且重载较多，可收敛重复的类型/名称校验路径。
- FSM 数据使用 `Dictionary<string, object>`，键名和类型缺少编译期约束。
- 缺少状态切换轨迹、循环切换保护和独立单元测试。
