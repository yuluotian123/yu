# InputModule 使用说明

## 概述

`InputModule` 是项目里的统一 action 输入模块，负责：

- 查询输入状态：`IsPressed`、`IsJustPressed`、`IsJustReleased`
- 事件型显式消费：`TryHandleJustPressed`、`TryHandleJustReleased`
- 持续输入显式接管：`TryHandlePressed`、`TryHandleActionStrength`、`TryHandleAxis`、`TryHandleVector`
- 方向输入辅助：`GetAxis`、`GetVector`、`GetMouseDelta`
- 输入缓冲与长按时长：`IsBuffered`、`ConsumeBufferedAction`、`GetHoldTime`
- 输入层管理：`EnableLayer`、`DisableLayer`、`IsLayerEnabled`
- 运行时模拟输入：`SimulateInputEvent`、`SimulateActionPress`、`SimulateActionRelease`

模块底层仍然基于 Godot 的 `Input` 和 `InputMap`，但业务侧应尽量统一通过 `IInputModule` 访问 action。

## InputMap 命名格式

不做编辑器插件，也不扩展原生 InputMap UI。  
`action group` 直接编码进 InputMap 的 action 名里。

格式：

```text
基础Action名|GroupId|GroupId...
```

示例：

```text
camera_up|move_up
combat_up|move_up
ui_cancel|cancel
camera_up|move_up|nav_up
```

规则：

- 基础 action 名继续沿用现有风格：`camera_xxx`、`combat_xxx`、`ui_xxx`
- `|` 后面跟一个或多个 `groupId`
- `groupId` 建议统一用小写下划线，如 `move_up`、`confirm`、`cancel`
- `|` 是保留分隔符，不应出现在普通 action 名或 groupId 里

## 基础名与真实名

业务代码继续使用基础 action 名：

```csharp
_input.IsPressed("camera_up");
_input.TryHandlePressed("combat_up");
_input.TryHandleJustPressed("ui_cancel");
```

`InputModule` 会在运行时自动完成：

- `baseActionName -> rawActionName`
- `groupId -> baseActionName集合`
- `baseActionName -> 同组baseActionName集合`

也就是说，如果 InputMap 里配置的是：

```text
camera_up|move_up
combat_up|move_up
```

那业务侧仍然只写：

```csharp
_input.TryHandlePressed("camera_up");
_input.TryHandlePressed("combat_up");
```

## Action Group 语义

`InputLayerManager` 会根据 `InputModule` 解析出的 group 信息扩展 consume 范围。

含义：

- 如果某个 action 被 consume
- 同组的其他 action 也会一并视为已 consume

例如：

```text
camera_up|move_up
combat_up|move_up
```

当高优先级层处理 `combat_up` 时：

- `combat_up` 会被 consume
- `camera_up` 也会因为同属 `move_up` 组而一起被 consume

## 两种消费语义

### 事件型：仅消费当前帧

适用接口：

- `TryHandleJustPressed`
- `TryHandleJustReleased`

行为说明：

- 消费状态只持续当前帧
- 同一帧内，更低优先级层再次调用同组 action 的 `TryHandle...` 会返回 `false`
- `IsJustPressed` 等纯查询接口不带副作用

### 持续型：接管直到释放

适用接口：

- `TryHandlePressed`
- `TryHandleActionStrength`
- `TryHandleAxis`
- `TryHandleVector`

行为说明：

- 高层一旦成功接管持续输入，会一直持有到 release
- 持有期间，更低优先级层无法再接管同组 action
- 同一层重复查询同一组输入会继续成功，不会重复创建锁
- `IsPressed`、`GetAxis`、`GetVector`、`GetActionStrength` 仍然只是纯查询

## 非法配置校验

`InputModule` 会检查“基础名冲突”。

例如如果同时存在：

```text
camera_up|move_up
camera_up|nav_up
```

那么它们的基础名都是 `camera_up`，这会造成运行时歧义。  
当前策略是：

- 启动或刷新时输出 warning
- 跳过该基础名映射
- 直到命名冲突被修复

## 使用示例

### 纯查询

```csharp
bool isPressed = _input.IsPressed("combat_attack");
bool justPressed = _input.IsJustPressed("combat_jump");
bool justReleased = _input.IsJustReleased("combat_dodge");
float strength = _input.GetActionStrength("combat_aim");
Vector2 move = _input.GetVector("combat_left", "combat_right", "combat_up", "combat_down");
```

### 事件型消费

```csharp
if (_input.TryHandleJustPressed("ui_cancel"))
{
    CloseWindow();
}

if (_input.TryHandleJustReleased("combat_attack", "UI"))
{
    EndAttackBlock();
}
```

### 持续型接管

```csharp
if (_input.TryHandlePressed("camera_drag", "UI"))
{
    BeginUiDrag();
}

if (_input.TryHandleVector(
        "camera_left",
        "camera_right",
        "camera_up",
        "camera_down",
        out var cameraMove))
{
    MoveCamera(cameraMove);
}

if (_input.TryHandleActionStrength("combat_aim", out var aimStrength))
{
    UpdateAim(aimStrength);
}
```

## 2D 相机示例

```csharp
_input.TryHandleVector("camera_left", "camera_right", "camera_up", "camera_down", out var moveAxis);
CameraMoveAxis = moveAxis;

IsSpeedupPressed = _input.TryHandlePressed("camera_speedup");
IsDraggingCamera = _input.TryHandlePressed("camera_drag");
CameraDragDelta = _input.GetMouseDelta();

ZoomInRequested = _input.TryHandleJustPressed("camera_zoom_in");
ZoomOutRequested = _input.TryHandleJustPressed("camera_zoom_out");
```

## 注意事项

1. `GetMouseDelta()` 仍然只是基于帧差计算，不会单独被 consume。
2. 持续输入接管和事件型消费都经过同一套 action group 扩展逻辑。
3. `TryHandleJustPressed` 和 `TryHandleJustReleased` 仍然只是当前帧消费。
4. 直接调用 Godot `Input.IsAction...("基础名")` 不再可靠，因为原生 Input 只认识完整 action 名；业务逻辑应统一走 `IInputModule`。

## 相关文件

- `scripts/gamelogic/input/IInputModule.cs`
- `scripts/gamelogic/input/InputModule.cs`
- `scripts/gamelogic/input/InputLayer.cs`
- `scripts/gamelogic/input/InputTracker.cs`
