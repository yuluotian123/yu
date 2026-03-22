# InputSystem 使用文档

## 概述

基于 Godot 原生 Input 系统的增强输入模块，专为动作游戏设计。

**核心功能：**
- **输入缓冲（Input Buffer）**：允许提前输入，在时间窗口内有效
- **输入层管理（Layer Management）**：通过命名前缀自动分层
- **长按追踪（Hold Time）**：追踪按键持续时间
- **运行时注入（Simulate Input）**：支持模拟输入事件

## 快速开始

### 1. 初始化模块

```csharp
// RootModule._Ready() 中
ModuleSystem.GetModule<IInputModule>();
```

### 2. 在 Godot 项目设置中配置 InputMap

**命名约定（自动识别层）：**
- `ui_xxx` → UI 层（优先级 100）
- `combat_xxx` → Combat 层（优先级 50）
- `camera_xxx` → Camera 层（优先级 30）
- 其他 → Global 层（优先级 0）

**示例配置：**
```
ui_accept       → UI 层
ui_cancel       → UI 层
combat_attack   → Combat 层
combat_dodge    → Combat 层
camera_rotate   → Camera 层
pause           → Global 层
```

### 3. 使用示例

```csharp
using Framework;
using GameLogic.Input;
using Godot;

public partial class Player : CharacterBody2D
{
    private IInputModule _input;

    public override void _Ready()
    {
        _input = ModuleSystem.GetModule<IInputModule>();
    }

    public override void _PhysicsProcess(double delta)
    {
        // 移动输入
        Vector2 move = _input.GetVector("combat_left", "combat_right", "combat_up", "combat_down");
        Velocity = move * 300f;

        // 跳跃（支持输入缓冲）
        if (_input.IsJustPressed("combat_jump") || _input.IsBuffered("combat_jump", 0.15f))
        {
            Jump();
            _input.ConsumeBufferedAction("combat_jump");
        }

        // 长按蓄力
        if (_input.IsPressed("combat_attack"))
        {
            float holdTime = _input.GetHoldTime("combat_attack");
            if (holdTime >= 1.0f)
                HeavyAttack();
        }

        MoveAndSlide();
    }
}
```

## 核心 API

### 基础查询

```csharp
bool isPressed = _input.IsPressed("combat_attack");
bool justPressed = _input.IsJustPressed("combat_jump");
bool justReleased = _input.IsJustReleased("combat_dodge");
float strength = _input.GetActionStrength("combat_aim");
```

### 轴向输入

```csharp
Vector2 move = _input.GetVector("combat_left", "combat_right", "combat_up", "combat_down");
Vector2 move = _input.GetVector("combat_left", "combat_right", "combat_up", "combat_down", 0.2f); // 自定义死区
```

### 输入缓冲

```csharp
// 检查 0.2 秒内是否按下
if (_input.IsBuffered("combat_attack", 0.2f))
{
    NextCombo();
    _input.ConsumeBufferedAction("combat_attack");
}

_input.ClearBuffer(); // 清除所有缓冲
```

### 长按追踪

```csharp
float holdTime = _input.GetHoldTime("combat_block");
if (holdTime >= 2.0f)
    PerfectBlock();
```

### 输入层管理

```csharp
// 打开 UI 时自动禁用战斗输入
_input.DisableLayer("Combat");
_input.EnableLayer("UI");

// 关闭 UI 时恢复
_input.DisableLayer("UI");
_input.EnableLayer("Combat");

bool isUIActive = _input.IsLayerEnabled("UI");
```

### 运行时模拟输入

```csharp
// 模拟动作按下
_input.SimulateActionPress("combat_attack", 1.0f);

// 模拟动作释放
_input.SimulateActionRelease("combat_attack");

// 模拟自定义 InputEvent
var keyEvent = new InputEventKey
{
    Keycode = Key.Space,
    Pressed = true
};
_input.SimulateInputEvent(keyEvent);
```

**用途：**
- AI 控制角色
- 输入录制/回放
- 教程演示
- 自动化测试

## 输入层自动识别

通过 action 名称前缀自动识别所属层，**无需手动绑定**：

| 前缀 | 层名称 | 优先级 | 用途 |
|------|--------|--------|------|
| `ui_` | UI | 100 | UI 导航和交互 |
| `combat_` | Combat | 50 | 角色战斗输入 |
| `camera_` | Camera | 30 | 摄像机控制 |
| 无前缀 | Global | 0 | 全局输入（暂停等） |

**禁用层后，该层的所有 action 自动失效。**

## 配合 Godot 原生输入消费

```csharp
// UI 中消费输入
public override void _GuiInput(InputEvent @event)
{
    if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
    {
        OnClicked();
        GetViewport().SetInputAsHandled(); // 阻止传递到游戏层
    }
}

// 游戏层接收未消费的输入
public override void _UnhandledInput(InputEvent @event)
{
    if (@event.IsActionPressed("combat_interact"))
    {
        Interact();
        GetViewport().SetInputAsHandled();
    }
}
```

## 动作游戏最佳实践

### 1. 输入缓冲实现连招

```csharp
if (_isAttacking && _input.IsBuffered("combat_attack", 0.3f))
{
    _queueNextAttack = true;
    _input.ConsumeBufferedAction("combat_attack");
}
```

### 2. Coyote Time

```csharp
if (IsOnFloor() && _input.IsBuffered("combat_jump", 0.1f))
{
    Jump();
    _input.ConsumeBufferedAction("combat_jump");
}
```

### 3. 长按区分轻重攻击

```csharp
if (_input.IsJustReleased("combat_attack"))
{
    float holdTime = _input.GetHoldTime("combat_attack");
    if (holdTime < 0.3f)
        LightAttack();
    else
        HeavyAttack();
}
```

### 4. UI 自动屏蔽游戏输入

```csharp
// UIWindow.cs
public override void _Ready()
{
    ModuleSystem.GetModule<IInputModule>().DisableLayer("Combat");
}

public override void _ExitTree()
{
    ModuleSystem.GetModule<IInputModule>().EnableLayer("Combat");
}
```

## 注意事项

1. **action 名称必须在 Godot InputMap 中定义**
2. **遵循命名约定以自动识别层**（`ui_`、`combat_`、`camera_`）
3. **输入缓冲时间建议 0.1-0.3 秒**
4. **配合 `SetInputAsHandled()` 实现真正的输入消费**
5. **模拟输入会触发缓冲和长按追踪**

## 完整示例

```csharp
public partial class Player : CharacterBody2D
{
    private IInputModule _input;

    public override void _Ready()
    {
        _input = ModuleSystem.GetModule<IInputModule>();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // 事件驱动输入（优先级高）
        if (@event.IsActionPressed("combat_interact"))
        {
            TryInteract();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // 轮询式输入（连续输入）
        Vector2 move = _input.GetVector("combat_left", "combat_right", "combat_up", "combat_down");
        Velocity = move * Speed;

        // 输入缓冲
        if (_input.IsBuffered("combat_jump", 0.15f) && IsOnFloor())
        {
            Jump();
            _input.ConsumeBufferedAction("combat_jump");
        }

        MoveAndSlide();
    }
}
```
