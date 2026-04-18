# InputModule 使用说明

## 概览

`InputModule` 提供统一的 action 输入访问接口，分成三类能力：

- 纯查询：`IsPressed`、`IsJustPressed`、`IsJustReleased`、`GetActionStrength`、`GetAxis`、`GetVector`
- 显式消费：`TryConsumeJustPressed`、`TryConsumeJustReleased`
- 显式占用：`TryConsumePressed`、`TryConsumeActionStrength`、`TryConsumeAxis`、`TryConsumeVector`

此外还提供两类消费状态查询：

- `IsActionConsumed`：只查询当前帧 consume
- `IsActionHeldConsumed`：只查询 held consume

## InputMap 命名

支持把 action group 直接写在 InputMap action 名里：

```text
BaseAction|GroupId|GroupId...
```

示例：

```text
camera_up|move_up
combat_up|move_up
ui_cancel|cancel
camera_up|move_up|nav_up
```

业务代码始终使用基础 action 名，`InputModule` 会在运行时自动解析 group 并扩展消费判定。

## 查询与消费的职责分离

当前版本的约定是：

1. `Is*` / `Get*` 只负责查询输入状态或输入值
2. `TryConsume*` 只负责 consume / held consume

这意味着业务代码应当先查，再 consume：

```csharp
if (_input.IsJustPressed("ui_cancel") &&
    _input.TryConsumeJustPressed("ui_cancel"))
{
    CloseWindow();
}
```

```csharp
Vector2 move = _input.GetVector("camera_left", "camera_right", "camera_up", "camera_down");
if (move != Vector2.Zero &&
    _input.TryConsumeVector("camera_left", "camera_right", "camera_up", "camera_down"))
{
    MoveCamera(move);
}
```

## Query Filter Params

为了让查询接口也能按“某个处理层的视角”过滤掉已经被占用的输入，以下查询接口都补了可选参数：

- `IsPressed`
- `IsJustPressed`
- `IsJustReleased`
- `GetActionStrength`
- `GetAxis`
- `GetVector`

统一规则：

- `handlerLayer`
  含义：可选的查询视角层。省略时使用 action 默认层。
- `filterConsumed = false`
  含义：保持纯查询行为，不做消费过滤。
- `filterConsumed = true` 对 `IsJustPressed` / `IsJustReleased`
  含义：按 `IsActionConsumed` 过滤当前帧 consume。
- `filterConsumed = true` 对 `IsPressed` / `GetActionStrength` / `GetAxis` / `GetVector`
  含义：按 `IsActionHeldConsumed` 过滤 held consume。

`includeSamePriority` 的默认值与对应的 consume API 保持一致：

- `IsJustPressed` / `IsJustReleased`：默认 `false`
- `IsPressed` / `GetActionStrength` / `GetAxis` / `GetVector`：默认 `true`

示例：

```csharp
bool canUseCancel = _input.IsJustPressed(
    "ui_cancel",
    handlerLayer: "UI",
    filterConsumed: true);

Vector2 move = _input.GetVector(
    "camera_left",
    "camera_right",
    "camera_up",
    "camera_down",
    handlerLayer: "Camera",
    filterConsumed: true);
```

## 两类消费状态查询

### `IsActionConsumed`

只查询当前帧 consume：

- 只看 `_consumedActions`
- 不看 held lock
- 适合回答“这帧是否已经被处理过”

### `IsActionHeldConsumed`

只查询 held consume：

- 只看 `_heldConsumedActions`
- 不混入当前帧 consume
- 适合回答“当前是否被持续占用”

## 注意事项

1. `TryConsume*` 不会帮你判断输入是否成立，调用前应先用 `Is*` / `Get*` 判断。
2. `IsActionConsumed` 和 `IsActionHeldConsumed` 语义刻意分离，不要混用。
3. `GetMouseDelta()` 仍然只是基于帧差计算，不参与 consume。
4. 业务逻辑不要直接调用 Godot 的 `Input.IsAction...("基础名")`，因为原生 `Input` 只认识真实 action 名。

## 相关文件

- `scripts/gamelogic/input/IInputModule.cs`
- `scripts/gamelogic/input/InputModule.cs`
- `scripts/gamelogic/input/InputLayer.cs`
- `scripts/gamelogic/input/InputTracker.cs`
