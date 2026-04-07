# 对象池模块使用文档

> **命名空间**：`Framework`  
> **对应目录**：`scripts/framework/pool/objectpool/`

---

## 目录

1. [架构概览](#1-架构概览)
2. [纯 C# 对象池](#2-纯-c-对象池)
   - 2.1 [实现 IObjectPoolItem](#21-实现-iobjectpoolitem)
   - 2.2 [创建与使用](#22-创建与使用)
   - 2.3 [池属性配置](#23-池属性配置)
3. [Node 对象池](#3-node-对象池)
   - 3.1 [同步创建](#31-同步创建)
   - 3.2 [异步创建](#32-异步创建)
   - 3.3 [可选实现 IObjectPoolItem](#33-可选实现-iobjectpoolitem)
4. [查询与销毁](#4-查询与销毁)
5. [全局操作](#5-全局操作)
6. [完整示例](#6-完整示例)

---

## 1. 架构概览

```
IObjectPoolModule（接口）
   └── ObjectPoolModule（实现）
           ├── IObjectPool<T>   —— 纯 C# 对象池
           │       T : IObjectPoolItem
           └── INodePool        —— Godot Node 对象池
```

| 类 / 接口 | 职责 |
|-----------|------|
| `IObjectPoolModule` | 对外暴露的对象池管理接口，通过 `ModuleSystem.GetModule<IObjectPoolModule>()` 获取 |
| `IObjectPool<T>` | 管理纯 C# 对象的池，T 必须实现 `IObjectPoolItem` |
| `INodePool` | 管理 Godot Node 的池，Node 由 `PackedScene` 实例化 |
| `IObjectPoolItem` | 池化对象接口，定义 `OnSpawn` / `OnRecycle` 回调 |

---

## 2. 纯 C# 对象池

### 2.1 实现 IObjectPoolItem

所有需要被 `IObjectPool<T>` 管理的类必须实现 `IObjectPoolItem`：

```csharp
public class Bullet : IObjectPoolItem
{
    public Vector2 Position;
    public float Speed;

    // 从池中取出时调用：重置/初始化状态
    public void OnSpawn()
    {
        Position = Vector2.Zero;
        Speed = 0f;
    }

    // 回收到池时调用：清理状态、取消事件订阅等
    public void OnRecycle()
    {
        Speed = 0f;
    }
}
```

### 2.2 创建与使用

```csharp
var poolModule = ModuleSystem.GetModule<IObjectPoolModule>();

// 创建对象池（容量 50，每 30 秒自动释放一次空闲对象）
var bulletPool = poolModule.CreateObjectPool<Bullet>(
    name: "BulletPool",
    capacity: 50,
    autoReleaseInterval: 30f
);

// 取出对象（自动调用 OnSpawn）
var bullet = bulletPool.Spawn();
bullet.Position = new Vector2(100, 200);
bullet.Speed = 500f;

// 回收对象（自动调用 OnRecycle）
bulletPool.Recycle(bullet);
```

> 若池中有空闲对象则直接复用，否则 `new T()` 创建新实例。  
> 回收时若池已满且 `AllowOverflow == false`，对象直接丢弃由 GC 回收。

### 2.3 池属性配置

```csharp
// 动态调整容量
bulletPool.Capacity = 100;

// 允许超容量时不丢弃（扩容）
bulletPool.AllowOverflow = true;

// 修改自动释放间隔（秒），≤0 禁用
bulletPool.AutoReleaseInterval = 60f;

// 查看当前空闲数量
int idle = bulletPool.Count;
```

---

## 3. Node 对象池

Node 对象池专用于管理 Godot `Node` 及其子类。回收时节点 `Visible = false` 并保留在父节点下，取出时重新设为可见。

### 3.1 同步创建

```csharp
var poolModule = ModuleSystem.GetModule<IObjectPoolModule>();

// 同步加载 PackedScene 并创建池（场景较大时会阻塞主线程）
var bulletPool = poolModule.CreateNodePool(
    scenePath: "res://scenes/bullet.tscn",
    parent: GetTree().Root,   // 所有 Node 实例挂载在此节点下
    name: "",                 // 同一场景路径允许多个不同名的池
    capacity: 32,
    autoReleaseInterval: 60f
);

// 取出 Node
var bullet = bulletPool.Spawn() as Bullet;

// 回收 Node
bulletPool.Recycle(bullet);
```

### 3.2 异步创建

```csharp
// 后台加载 PackedScene，加载完成后回调
poolModule.CreateNodePoolAsync(
    scenePath: "res://scenes/bullet.tscn",
    parent: GetTree().Root,
    onCompleted: pool =>
    {
        if (pool == null)
        {
            GD.PrintErr("Node 池创建失败");
            return;
        }
        var bullet = pool.Spawn() as Bullet;
    },
    capacity: 32
);
```

> 推荐在场景初始化阶段使用异步方式预热对象池，避免游戏运行中卡顿。

### 3.3 可选实现 IObjectPoolItem

Node 类可选实现 `IObjectPoolItem`，实现后 Spawn/Recycle 时会自动调用对应方法：

```csharp
public partial class Bullet : Node2D, IObjectPoolItem
{
    public void OnSpawn()
    {
        // 重置位置、速度、动画等
        Position = Vector2.Zero;
        Visible = true;
    }

    public void OnRecycle()
    {
        // 停止动画、清理状态
        SetPhysicsProcess(false);
    }
}
```

> 若 Node 未实现 `IObjectPoolItem`，框架只做 `Visible` 切换，不调用任何回调。

---

## 4. 查询与销毁

```csharp
var poolModule = ModuleSystem.GetModule<IObjectPoolModule>();

// ---- 纯 C# 池 ----

// 是否存在
bool exists = poolModule.HasObjectPool<Bullet>("BulletPool");

// 获取已有的池（不存在返回 null）
var pool = poolModule.GetObjectPool<Bullet>("BulletPool");

// 销毁池并释放所有对象
poolModule.DestroyObjectPool<Bullet>("BulletPool");

// ---- Node 池 ----

// 是否存在
bool nodeExists = poolModule.HasNodePool("res://scenes/bullet.tscn");

// 获取已有的 Node 池（不存在返回 null）
var nodePool = poolModule.GetNodePool("res://scenes/bullet.tscn");

// 销毁池并 QueueFree 所有闲置 Node
poolModule.DestroyNodePool("res://scenes/bullet.tscn");

// 当前管理的对象池总数（C# 池 + Node 池）
int total = poolModule.Count;
```

---

## 5. 全局操作

```csharp
// 立即释放所有池中的空闲对象/Node
poolModule.ReleaseAllUnused();
```

> 适合在场景切换、内存紧张时主动调用，清理所有池的闲置缓存。

---

## 6. 完整示例

### 场景：子弹发射系统

```csharp
using Framework;
using Godot;

public partial class BulletSystem : Node
{
    private INodePool _bulletPool;

    public override void _Ready()
    {
        var poolModule = ModuleSystem.GetModule<IObjectPoolModule>();

        // 异步预热子弹池
        poolModule.CreateNodePoolAsync(
            scenePath: "res://scenes/bullet.tscn",
            parent: this,
            onCompleted: pool => _bulletPool = pool,
            capacity: 100
        );
    }

    public void Fire(Vector2 position, Vector2 direction)
    {
        if (_bulletPool == null) return;

        var bullet = _bulletPool.Spawn() as Bullet;
        if (bullet == null) return;

        bullet.GlobalPosition = position;
        bullet.Direction = direction;
    }

    public void OnBulletHit(Bullet bullet)
    {
        // 回收而非销毁，下次 Fire 时复用
        _bulletPool.Recycle(bullet);
    }
}
```

### 场景：纯 C# 数据对象池

```csharp
using Framework;

public class DamageEvent : IObjectPoolItem
{
    public int Damage;
    public string Source;

    public void OnSpawn()  { Damage = 0; Source = null; }
    public void OnRecycle() { Damage = 0; Source = null; }
}

// 初始化
var poolModule = ModuleSystem.GetModule<IObjectPoolModule>();
var eventPool = poolModule.CreateObjectPool<DamageEvent>(capacity: 200);

// 使用
var evt = eventPool.Spawn();
evt.Damage = 100;
evt.Source = "Player";
// ... 处理伤害逻辑 ...
eventPool.Recycle(evt);
```

---

## 附：两种池对比

| | `IObjectPool<T>` | `INodePool` |
|---|---|---|
| 管理对象 | 纯 C# 类 | Godot Node |
| 必须实现接口 | `IObjectPoolItem`（必须） | `IObjectPoolItem`（可选） |
| 回收方式 | 放回内部列表 | `Visible = false`，保留在父节点 |
| 超容量处理 | 丢弃（GC 回收） | `QueueFree` 销毁 |
| 创建方式 | `CreateObjectPool<T>` | `CreateNodePool` / `CreateNodePoolAsync` |
