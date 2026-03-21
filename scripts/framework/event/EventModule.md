# EventModule 使用指南

## 概览

`EventModule` 是基于 **发布-订阅（Pub/Sub）** 模式的事件系统，遵循框架的模块化与面向接口设计。

| 文件 | 说明 |
|------|------|
| `GameEventArgs.cs` | 事件参数基类，所有自定义事件参数继承此类 |
| `IEventModule.cs` | 事件模块接口，业务层唯一依赖 |
| `EventModule.cs` | 模块实现，由 `ModuleSystem` 自动管理 |

---

## 快速上手

### 第一步：在 RootModule 中注册模块

```csharp
// scripts/gamelogic/RootModule.cs
public override void _Ready()
{
    // ...其他模块...
    ModuleSystem.GetModule<IEventModule>(); // 注册事件模块
}
```

---

### 第二步：定义事件参数

```csharp
// 推荐放在 scripts/gamelogic/events/ 目录下

/// <summary>玩家受击事件参数。</summary>
public sealed class PlayerHitEventArgs : GameEventArgs
{
    // 使用 GetEventId<T>() 自动获取唯一 ID，无需手动分配
    public override int Id => GameEventArgs.GetEventId<PlayerHitEventArgs>();

    public int Damage     { get; private set; }
    public string Source  { get; private set; }

    /// <summary>创建事件参数（低频事件直接 new 即可）。</summary>
    public static PlayerHitEventArgs Create(int damage, string source)
    {
        var e = new PlayerHitEventArgs();
        e.Damage  = damage;
        e.Source  = source;
        return e;
    }

    public override void Clear()
    {
        Damage = 0;
        Source = null;
    }
}

/// <summary>玩家升级事件参数。</summary>
public sealed class PlayerLevelUpEventArgs : GameEventArgs
{
    public override int Id => GameEventArgs.GetEventId<PlayerLevelUpEventArgs>();

    public int OldLevel { get; private set; }
    public int NewLevel { get; private set; }

    public static PlayerLevelUpEventArgs Create(int oldLevel, int newLevel)
    {
        var e = new PlayerLevelUpEventArgs();
        e.OldLevel = oldLevel;
        e.NewLevel = newLevel;
        return e;
    }

    public override void Clear()
    {
        OldLevel = 0;
        NewLevel = 0;
    }
}
```

---

### 第三步：订阅事件

```csharp
public class PlayerUI : Node
{
    private IEventModule _eventModule;

    public override void _Ready()
    {
        _eventModule = ModuleSystem.GetModule<IEventModule>();

        // 订阅受击事件
        _eventModule.Subscribe(
            GameEventArgs.GetEventId<PlayerHitEventArgs>(),
            OnPlayerHit
        );

        // 订阅升级事件
        _eventModule.Subscribe(
            GameEventArgs.GetEventId<PlayerLevelUpEventArgs>(),
            OnPlayerLevelUp
        );
    }

    public override void _ExitTree()
    {
        // ⚠️ 节点销毁时务必取消订阅，防止内存泄漏
        _eventModule.Unsubscribe(
            GameEventArgs.GetEventId<PlayerHitEventArgs>(),
            OnPlayerHit
        );
        _eventModule.Unsubscribe(
            GameEventArgs.GetEventId<PlayerLevelUpEventArgs>(),
            OnPlayerLevelUp
        );
    }

    private void OnPlayerHit(object sender, GameEventArgs e)
    {
        var args = (PlayerHitEventArgs)e;
        GD.Print($"[UI] 受到 {args.Damage} 点来自 {args.Source} 的伤害！");
        // 更新血条 UI...
    }

    private void OnPlayerLevelUp(object sender, GameEventArgs e)
    {
        var args = (PlayerLevelUpEventArgs)e;
        GD.Print($"[UI] 升级！{args.OldLevel} → {args.NewLevel}");
        // 播放升级动画...
    }
}
```

---

### 第四步：触发事件

```csharp
public class PlayerController : Node
{
    private IEventModule _eventModule;

    public override void _Ready()
    {
        _eventModule = ModuleSystem.GetModule<IEventModule>();
    }

    /// <summary>受到伤害时调用。</summary>
    public void TakeDamage(int damage, string source)
    {
        // HP 计算...

        // ✅ Fire：推入队列，下一帧派发（推荐）
        // 安全：即使在事件处理器中再次调用 Fire 也不会引发递归问题
        _eventModule.Fire(this, PlayerHitEventArgs.Create(damage, source));
    }

    /// <summary>升级时调用。</summary>
    public void LevelUp(int oldLevel, int newLevel)
    {
        // ✅ FireNow：立即派发（当帧响应）
        // 适用于需要在同一帧内立即处理的场景
        _eventModule.FireNow(this, PlayerLevelUpEventArgs.Create(oldLevel, newLevel));
    }
}
```

---

## Fire vs FireNow 的选择

| | `Fire`（推荐） | `FireNow` |
|-|--------------|-----------|
| 派发时机 | 下一帧 `Process` 时 | 调用时立即 |
| 嵌套安全 | ✅ 处理器中再次 `Fire` 会在下下帧处理 | ⚠️ 避免处理器中调用 `FireNow` |
| 适用场景 | 绝大多数游戏事件 | 需要立即同帧响应的场景 |
| 典型例子 | 受击、得分、道具拾取 | 流程控制、状态同步 |

---

## 高频事件：结合对象池减少 GC

对于每帧可能触发多次的事件（如子弹命中、伤害数字），可结合 `IObjectPoolModule` 复用事件参数对象：

```csharp
// 初始化：创建事件参数对象池
var poolModule = ModuleSystem.GetModule<IObjectPoolModule>();
var hitArgsPool = poolModule.CreateObjectPool<PlayerHitEventArgs>(capacity: 64);

// 触发事件：从池中取出
var e = hitArgsPool.Spawn();               // 取出（自动调用 Clear）
// e.Damage = damage; // ⚠️ Spawn 后重新赋值（因为 Clear 已重置）
// 由于 GameEventArgs 的字段是 private set，需改为 public set 或提供 Init 方法：
// e.Init(damage, source);
_eventModule.Fire(this, e);

// 处理器中用完后归还
private void OnPlayerHit(object sender, GameEventArgs e)
{
    var args = (PlayerHitEventArgs)e;
    // ...处理逻辑...
    hitArgsPool.Recycle(args);             // 归还池中（自动调用 Clear）
}
```

---

## 注意事项

1. **务必在节点/对象销毁时 `Unsubscribe`**，否则会持有已销毁对象的引用，导致内存泄漏或空指针异常。
2. **`Fire` 中的事件参数在派发前不要修改**，因为事件是下一帧才处理的，修改会影响派发时读取的数据。
3. **不要在 `Subscribe` 回调中抛出未捕获异常**，`EventModule` 虽然会捕获异常并输出日志，但这表明处理器存在 bug，需及时修复。
