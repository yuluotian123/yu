# InputModule 使用说明

## 概述

`InputModule` 仍然是项目里的统一 action 输入模块，负责：

- action 查询：`IsPressed`、`IsJustPressed`、`IsJustReleased`
- 方向输入：`GetAxis`、`GetVector`
- 输入缓冲：`IsBuffered`、`ConsumeBufferedAction`
- 长按时长：`GetHoldTime`
- 输入层管理：`EnableLayer`、`DisableLayer`、`IsLayerEnabled`
- 运行时模拟输入：`SimulateInputEvent`、`SimulateActionPress`、`SimulateActionRelease`

当前版本只做了一个与相机相关的低侵入扩展：

- `GetMouseDelta()`：返回当前帧鼠标位置相对上一帧的差值

这意味着：

- 键盘和滚轮 action 仍然完全走 Godot 的 `Input` 查询
- 模块不再接管鼠标 action 的按下/释放语义
- 模块不处理 UI 是否拦截鼠标输入

## 初始化

`RootModule._Ready()` 中初始化：

```csharp
ModuleSystem.GetModule<IInputModule>();
```

当前低侵入方案下，不需要把 `_UnhandledInput` 转发给 `InputModule`。

## InputMap 约定

### 命名约定

- `ui_xxx` -> `UI`
- `combat_xxx` -> `Combat`
- `camera_xxx` -> `Camera`
- 其他 -> `Global`

### 相机相关 action

```text
camera_left
camera_right
camera_up
camera_down
camera_speedup
camera_drag
camera_zoom_in
camera_zoom_out
```

建议绑定：

- `camera_drag` -> 鼠标左键
- `camera_zoom_in` -> 滚轮上
- `camera_zoom_out` -> 滚轮下

## 核心 API

### action 查询

```csharp
bool isPressed = _input.IsPressed("combat_attack");
bool justPressed = _input.IsJustPressed("combat_jump");
bool justReleased = _input.IsJustReleased("combat_dodge");
float strength = _input.GetActionStrength("combat_aim");
```

这些接口继续直接基于 `Godot.Input`。

### action 拦截

```csharp
if (_input.TryHandleJustPressed("ui_cancel"))
{
    CloseWindow();
}

if (_input.TryHandleJustPressed("combat_attack", "UI"))
{
    BlockAttackFromUI();
}
```

说明：

- `TryHandleJustPressed` / `TryHandleJustReleased` 是显式消费接口
- 成功处理后，会把该 action 记为“当前层已消费”
- 同帧内更低优先级层再次 `TryHandle...` 同一个 action 时会返回 `false`
- 消费状态只持续当前帧，下一帧会自动清空
- 默认按 action 前缀推断处理层，也可以显式传入 `handlerLayer`

手动消费与查询：

```csharp
_input.ConsumeAction("ui_cancel");

bool consumedForCombat = _input.IsActionConsumed("ui_cancel", "Combat");
```

适用场景：

- UI 和 Combat 竞争同一个 action
- 更高优先级系统想在同帧阻止低优先级系统重复处理
- 事件型输入分发，不改变原有 `IsJustPressed` 语义

### 方向输入

```csharp
Vector2 move = _input.GetVector("combat_left", "combat_right", "combat_up", "combat_down");
Vector2 axis = _input.GetAxis("combat_left", "combat_right", "combat_up", "combat_down");
```

### 鼠标位移

```csharp
Vector2 mouseDelta = _input.GetMouseDelta();
```

说明：

- `GetMouseDelta()` 返回主视口鼠标位置的帧差分
- 第一帧或无法读取视口时返回 `Vector2.Zero`
- 这个值不区分 UI 是否消费鼠标

### 输入缓冲与长按

```csharp
if (_input.IsBuffered("combat_attack", 0.2f))
{
    NextCombo();
    _input.ConsumeBufferedAction("combat_attack");
}

if (_input.IsJustReleased("combat_attack"))
{
    float holdTime = _input.GetHoldTime("combat_attack");
}
```

## 2D 相机示例

```csharp
CameraMoveAxis = _input.GetVector("camera_left", "camera_right", "camera_up", "camera_down");
IsSpeedupPressed = _input.IsPressed("camera_speedup");
CameraDragDelta = _input.GetMouseDelta();
IsDraggingCamera = _input.IsPressed("camera_drag");
ZoomInRequested = _input.IsJustPressed("camera_zoom_in");
ZoomOutRequested = _input.IsJustPressed("camera_zoom_out");
```

推荐消费方式：

- WASD 平移：读 `GetVector(...)`
- Shift 加速：读 `IsPressed("camera_speedup")`
- 拖拽平移：`IsPressed("camera_drag") && GetMouseDelta() != Vector2.Zero`
- 滚轮缩放：`IsJustPressed("camera_zoom_in")` / `IsJustPressed("camera_zoom_out")`

## 注意事项

1. `InputModule` 目前不处理“UI 是否阻断相机鼠标输入”。
2. 如果 UI 上点击或滚轮也不希望触发相机，后续需要单独引入未消费事件链路。
3. 当前 `GetMouseDelta()` 采用帧差分实现，不是事件累计。
4. `TryHandleJustPressed` / `TryHandleJustReleased` 只实现事件型当前帧消费，不处理 `IsPressed` 的持续拦截。
5. 本方案的目标是尽量不改变 `InputModule` 原有职责和语义。

## 相关文件

- `scripts/gamelogic/input/IInputModule.cs`
- `scripts/gamelogic/input/InputModule.cs`
- `scripts/gamelogic/input/InputLayer.cs`
- `scripts/gamelogic/input/InputTracker.cs`
