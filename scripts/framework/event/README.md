# Event Module

Event Module 提供基于整数事件 ID 的同步进程内消息分发，用于降低 UI、玩法组件和框架模块之间的直接引用。

## 核心类型

- `IEventModule`：订阅、取消订阅和发送事件。
- `EventModule`：事件处理器存储与同步调用实现。
- `EventId`：项目事件 ID 的集中定义入口。

## 快速开始

```csharp
IEventModule events = ModuleSystem.GetModule<IEventModule>();
int healthChanged = EventId.Get(player.health_changed);

void OnHealthChanged(int value)
{
    GD.Print(value);
}

events.Subscribe<int>(healthChanged, OnHealthChanged);
events.Send(healthChanged, 80);
events.Unsubscribe<int>(healthChanged, OnHealthChanged);
```

当前接口支持零到四个泛型参数，发送是同步的，处理器会在 `Send()` 调用栈内执行。

## 使用约定

- 长生命周期对象在初始化时订阅，在销毁或禁用时取消订阅。
- 事件 ID 集中定义，禁止在业务代码中散落魔法数字。
- 事件用于“发生了什么”，需要返回值或强顺序依赖时应使用明确接口。
- 高频逐帧数据优先使用直接状态读取，避免把事件系统当作数据总线。
- 回调中再次发送事件时应避免循环触发。

## 当前注意事项

- 整数 ID 缺少编译期载荷类型约束，同一 ID 使用不同参数签名会在运行时失败或静默失配。
- 同步分发中单个处理器异常可能中断后续处理器。
- 缺少订阅者归属和自动解绑机制，Node 生命周期错误容易产生悬挂委托。
- 后续可增加类型化事件键、异常隔离和调试快照，但应保持基础路径轻量。
