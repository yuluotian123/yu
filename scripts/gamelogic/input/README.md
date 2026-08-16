# Input Module

Input Module 在 Godot `Input` / `InputMap` 之上提供统一的 action 查询、输入消费、优先级层、组合轴、缓冲输入、按住时长和测试模拟接口。

## 核心类型

- `IInputModule`：业务侧输入查询入口。
- `InputModule`：action 缓存、分组解析和每帧采样。
- `InputTracker`：按下、释放、缓冲和按住状态。
- `InputLayerManager`：输入层优先级、启用状态和消费规则。

## 快速开始

```csharp
IInputModule input = ModuleSystem.GetModule<IInputModule>();

Vector2 movement = input.GetVector(
    move_left,
    move_right,
    move_up,
    move_down);

if (input.TryConsumeJustPressed(jump, Gameplay))
{
    Jump();
}
```

## 输入层与消费

- `EnableLayer()` / `DisableLayer()` 控制一组 handler 是否参与输入。
- `TryConsume*()` 查询成功后标记输入已消费。
- `filterConsumed` 控制普通查询是否过滤已被高优先级 handler 消费的输入。
- `includeSamePriority` 控制同优先级层之间是否共享消费结果。

UI、Gameplay、Cutscene 等系统应使用稳定的层名和明确优先级，避免各处自行解释输入抢占规则。

## 缓冲与测试

```csharp
if (input.IsBuffered(attack, 0.15f))
{
    input.ConsumeBufferedAction(attack);
}

input.SimulateActionPress(jump);
input.SimulateActionRelease(jump);
```

模拟接口用于 smoke test 和工具，不应替代 Godot InputMap 配置。

## 当前注意事项

- InputMap action 名称解析和分组规则应形成单独规范，并在启动时报告非法 action。
- `InputModule` 实现较大，查询、解析、采样和消费职责可以进一步拆分。
- 字符串 action/layer 缺少编译期约束，建议集中常量或生成类型化键。
- 输入缓冲依赖模块每帧更新，暂停、时间缩放和物理帧消费需要明确策略。
- 应增加优先级、同级消费、buffer、模拟输入和 InputMap 热更新测试。

