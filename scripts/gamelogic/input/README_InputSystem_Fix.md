# InputSystem 修复说明

## 问题分析

### 原有问题

#### 1. **UpdateHoldTimes 时序问题** ⚠️ (主要问题)

**问题描述：**
在 `InputModule.Process()` 中，`UpdateHoldTimes()` 在检测到按键释放时会立即从 `_holdTimes` 字典中移除记录，但此时 `IsJustReleased()` 还未被调用，导致 `GetHoldTime()` 返回 0。

**问题代码：**
```csharp
public void Process(double elapseSeconds, double realElapseSeconds)
{
    UpdateHoldTimes(realElapseSeconds);  // ← 这里会移除已释放的按键
    RecordJustPressedActions();          // ← 但这里才记录新按下的
}

private void UpdateHoldTimes(double deltaTime)
{
    foreach (var kvp in _holdTimes)
    {
        if (Godot.Input.IsActionPressed(action))
        {
            _holdTimes[action] += deltaTime;
        }
        else
        {
            toRemove.Add(action);  // ← 立即标记移除
        }
    }
    foreach (var action in toRemove)
    {
        _holdTimes.Remove(action);  // ← 立即删除！
    }
}
```

**实际影响：**
```csharp
// 在 InputComponent 中
if(_inputModule.IsJustReleased("combat_attack"))
{
    // 这里永远返回 0，因为 _holdTimes 已被清空！
    if(_inputModule.GetHoldTime("combat_attack") > 0.1f)
    {
        GD.Print("Long Attack");  // ← 永远不会执行
    }
}
```

#### 2. **性能问题**
- 每帧调用 `InputMap.GetActions()` 获取所有动作
- 产生不必要的 GC 压力

#### 3. **架构问题**
- `InputBuffer` 和 `HoldTime` 功能分离，但本质都是"输入事件的时间追踪"
- 代码重复，维护困难

---

## 解决方案

### 核心改进：统一的 InputTracker

创建了新的 `InputTracker` 类，整合了 `InputBuffer` 和 `HoldTime` 的功能。

#### 关键设计

**1. 统一的数据结构**
```csharp
internal class InputRecord
{
    public string Action;
    public double PressTime;      // 按下时间戳
    public double? ReleaseTime;   // 释放时间戳（null 表示仍在按下）
    public double HoldDuration;   // 持续时间（实时更新）
    
    public bool IsPressed => ReleaseTime == null;
}
```

**2. 延迟清理机制**
```csharp
public void RecordRelease(string action, double currentTime)
{
    if (_records.TryGetValue(action, out var record) && record.IsPressed)
    {
        record.ReleaseTime = currentTime;  // ← 只标记释放时间，不删除记录
    }
}

public void Update(double currentTime, double deltaTime)
{
    foreach (var record in _records.Values)
    {
        if (record.IsPressed)
        {
            // 更新持续时间
            record.HoldDuration += deltaTime;
        }
        else if (record.ReleaseTime.HasValue)
        {
            // 检查是否过期（释放后 1 秒才清理）
            double timeSinceRelease = currentTime - record.ReleaseTime.Value;
            if (timeSinceRelease > MaxRecordAge)
            {
                _recordsToClean.Add(action);
            }
        }
    }
}
```

**3. GetHoldTime 在释放帧仍可用**
```csharp
public float GetHoldTime(string action)
{
    if (_records.TryGetValue(action, out var record))
    {
        return (float)record.HoldDuration;  // ← 即使已释放，记录仍在
    }
    return 0f;
}
```

---

## 时序对比

### 修复前（有问题）
```
Frame N:   按下 attack
           → _holdTimes["attack"] = 0

Frame N+1: 释放 attack
           → UpdateHoldTimes() 检测到释放
           → _holdTimes.Remove("attack")  ❌ 立即删除
           → 用户代码调用 IsJustReleased("attack")
           → 用户代码调用 GetHoldTime("attack")
           → 返回 0  ❌ 错误！
```

### 修复后（正确）
```
Frame N:   按下 attack
           → _tracker.RecordPress("attack", time)
           → record.PressTime = time
           → record.HoldDuration = 0

Frame N+1: 释放 attack
           → _tracker.Update() 更新 HoldDuration
           → _tracker.RecordRelease("attack", time)
           → record.ReleaseTime = time  ✓ 只标记，不删除
           → 用户代码调用 IsJustReleased("attack")
           → 用户代码调用 GetHoldTime("attack")
           → 返回正确的 HoldDuration  ✓ 正确！

Frame N+60: (1秒后)
           → _tracker.Update() 检测到过期
           → 清理记录
```

---

## 其他改进

### 1. 缓存动作列表
```csharp
private List<string> _cachedActions;

private void CacheActions()
{
    _cachedActions = new List<string>();
    var actionList = Godot.InputMap.GetActions();
    foreach (var action in actionList)
    {
        _cachedActions.Add(action.ToString());
    }
}
```
**效果：** 避免每帧调用 `InputMap.GetActions()`，减少 GC 压力。

### 2. HoldTime 上限
```csharp
private const float MaxHoldTime = 10f;

record.HoldDuration = System.Math.Min(
    record.HoldDuration + deltaTime, 
    MaxHoldTime
);
```
**效果：** 防止长时间按住导致数值过大。

### 3. 简化的执行流程
```csharp
public void Process(double elapseSeconds, double realElapseSeconds)
{
    _currentTime += realElapseSeconds;
    
    // 1. 更新追踪器（包含持续时间更新和过期清理）
    _tracker.Update(_currentTime, realElapseSeconds);
    
    // 2. 清除层消费状态
    _layerManager.ClearAllConsumed();
    
    // 3. 记录本帧的输入事件
    RecordInputEvents();
}
```

---

## 使用示例

### 长按检测（现在可以正常工作）
```csharp
if (_inputModule.IsJustReleased("combat_attack"))
{
    float holdTime = _inputModule.GetHoldTime("combat_attack");
    
    if (holdTime > 0.5f)
    {
        GD.Print("Heavy Attack!");  // ✓ 现在可以正确检测
    }
    else if (holdTime > 0.1f)
    {
        GD.Print("Normal Attack");
    }
    else
    {
        GD.Print("Quick Tap");
    }
}
```

### 输入缓冲（功能保持不变）
```csharp
// 允许提前 0.2 秒输入跳跃
if (_inputModule.IsBuffered("jump", 0.2f) && isGrounded)
{
    Jump();
    _inputModule.ConsumeBufferedAction("jump");
}
```

---

## 文件变更

### 新增文件
- `scripts/gamelogic/input/InputTracker.cs` - 统一的输入追踪器

### 修改文件
- `scripts/gamelogic/input/InputModule.cs` - 使用 InputTracker 替代 InputBuffer 和 HoldTime

### 可删除文件（可选）
- `scripts/gamelogic/input/InputBuffer.cs` - 已被 InputTracker 替代（保留也无影响）

---

## 测试建议

1. **长按检测测试**
   - 按住按键不同时长（0.1s, 0.5s, 1s）
   - 验证 `GetHoldTime()` 返回正确值

2. **输入缓冲测试**
   - 提前按下按键
   - 验证 `IsBuffered()` 在缓冲时间内返回 true

3. **性能测试**
   - 监控 GC 分配
   - 验证不再每帧调用 `InputMap.GetActions()`

4. **边缘情况测试**
   - 快速连续按下/释放
   - 输入层禁用/启用切换
   - 长时间按住（超过 10 秒）

---

## 总结

通过引入统一的 `InputTracker`，我们：
1. ✅ 解决了 HoldTime 的时序问题
2. ✅ 提升了性能（缓存动作列表）
3. ✅ 简化了架构（统一管理）
4. ✅ 提高了可维护性
5. ✅ 增强了扩展性（未来可轻松添加双击、连击等功能）
