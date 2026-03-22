# EventModule — 事件模块

## 设计理念

对齐 [TEngine](https://github.com/Alex-Rachel/TEngine) 的 `EventDispatcher` 设计，采用 `Action<T>` 泛型委托风格，解决了旧版 `EventHandler<GameEventArgs>` 设计的三大问题：

| 旧版问题 | 新版方案 |
|---|---|
| 必须继承 `GameEventArgs`，每个事件都要新建子类 | 直接传参，无需额外类 |
| 回调中强转 `(MyEventArgs)e`，编译期无检查 | 泛型委托，编译期类型安全 |
| 回调中 Subscribe 新处理器会被当帧意外执行 | dirty 缓冲机制，执行完再合并 |
| `GetHashCode()` 有 ID 碰撞风险 | `EventId.Get<T>()` 单调递增，天然无碰撞 |

---

## 核心组件

```
EventId.cs            — 事件 ID 生成器（单调递增，泛型类型绑定）
EventDelegateData.cs  — 单个 eventId 的委托容器（dirty 机制）
IEventModule.cs       — 模块接口
EventModule.cs        — 模块实现
```

---

## 定义事件 ID

`EventId` 提供两种方式，可以混用：

### ① 字符串方式（对齐 TEngine，推荐）

```csharp
public static class GameEvents
{
    // 同一字符串始终返回相同 ID，首次调用时自动分配
    public static readonly int GameNotice = EventId.Get("game.notice");
    public static readonly int PlayerHit  = EventId.Get("game.player.hit");
}
```

> 优点：可读性强，天然文档化；动态/配置驱动场景下也可直接传字符串。  
> 注意：`EventId.Get(string)` 区分大小写；建议统一用小写点分格式，如 `"module.event"`。

### ② 泛型哑类型方式（零运行时查找）

```csharp
public static class GameEvents
{
    // 用 private struct 作为哑类型，保证 ID 全局唯一
    private struct NoticeTag    { }
    private struct PlayerHitTag { }

    public static readonly int GameNotice = EventId.Get<NoticeTag>();
    public static readonly int PlayerHit  = EventId.Get<PlayerHitTag>();
}
```

> `EventId.Get<T>()` 利用 CLR 泛型静态字段唯一性 + `Interlocked.Increment`，
> 同一类型全生命周期返回相同值，零字典查找开销，适合高频注册场景。

两种方式共享同一个全局单调递增计数器，生成的 ID **不会互相碰撞**。

---

## 订阅 / 取消订阅

```csharp
// 获取模块
var ev = ModuleSystem.GetModule<IEventModule>();

// 订阅（无参）
ev.Subscribe(GameEvents.GameNotice, OnGameNotice);
void OnGameNotice() { GD.Print("收到通知"); }

// 订阅（1 参数）
ev.Subscribe<string>(GameEvents.GameNotice, OnGameNoticeMsg);
void OnGameNoticeMsg(string msg) { GD.Print(msg); }

// 订阅（多参数）
ev.Subscribe<string, int>(GameEvents.PlayerHit, OnPlayerHit);
void OnPlayerHit(string name, int damage) { ... }

// 取消订阅（在 _ExitTree / OnDestroy 等清理处调用）
ev.Unsubscribe<string>(GameEvents.GameNotice, OnGameNoticeMsg);
```

- 重复订阅：返回 `false` 并打 Error 日志，**不抛异常**。
- 取消不存在的订阅：打 Warn 日志，**不抛异常**。

---

## 发送事件

```csharp
// 无参
ev.Send(GameEvents.GameNotice);

// 1 参数
ev.Send<string>(GameEvents.GameNotice, "Hello!");

// 多参数
ev.Send<string, int>(GameEvents.PlayerHit, "Player", 42);
```

- 所有 `Send` 均为**同步立即**派发，无队列延迟。
- 在回调执行期间调用 `Subscribe` / `Unsubscribe` 是安全的（通过 dirty 缓冲机制），
  新增/删除的处理器会在当次派发完毕后才生效。

---

## 支持的参数数量

| 方法 | 参数数量 |
|---|---|
| `Subscribe(id, Action)` | 0 |
| `Subscribe<T1>(id, Action<T1>)` | 1 |
| `Subscribe<T1,T2>(id, ...)` | 2 |
| `Subscribe<T1,T2,T3>(id, ...)` | 3 |
| `Subscribe<T1,T2,T3,T4>(id, ...)` | 4 |

`Send` / `Unsubscribe` 同上。如需 4 个以上参数，建议将相关参数封装为一个结构体再传递。
