# Object Pool Module

Object Pool Module 统一管理纯 C# 对象池和 Godot Node 对象池，用于减少频繁创建、销毁对象及加载场景产生的开销。

## 核心类型

- `IObjectPoolModule`：对象池创建、查询、销毁和闲置回收入口。
- `IObjectPool<T>`：纯 C# 对象池。
- `IObjectPoolItem`：池化对象的生成、回收和释放回调。
- `INodePool`：基于 `PackedScene` 的 Node 对象池。
- `ObjectPoolModule`：统一更新和管理所有池。

## C# 对象池

```csharp
IObjectPoolModule pools = ModuleSystem.GetModule<IObjectPoolModule>();
IObjectPool<BulletData> bullets = pools.CreateObjectPool<BulletData>(
    capacity: 64,
    autoReleaseInterval: 60f);

BulletData bullet = bullets.Spawn();
bullets.Recycle(bullet);
```

池化类型必须实现 `IObjectPoolItem` 并提供无参构造函数。

## Node 对象池

```csharp
INodePool pool = pools.CreateNodePool(
    res://assets/scenes/bullet.tscn,
    parentNode,
    capacity: 32);

Node bullet = pool.Spawn();
pool.Recycle(bullet);
```

异步创建使用 `CreateNodePoolAsync()`，完成回调可能收到 `null`，调用方必须处理加载失败和 owner 已销毁的情况。

## 生命周期

- 模块每帧更新池的闲置时间。
- `ReleaseAllUnused()` 主动释放所有未使用对象。
- `DestroyObjectPool()` / `DestroyNodePool()` 销毁指定池。
- 模块关闭时销毁全部对象池和持有的 Node。

## 当前注意事项

- Node 回收前必须重置可见性、处理状态、信号和父节点关系。
- 异步创建回调缺少取消句柄，场景退出时可能出现迟到回调。
- 池键由类型/名称或场景路径/名称组成，命名冲突应在创建时明确报错。
- 自动释放策略需要 profiler 数据支持，避免为了省内存反复重建高频对象。
- 建议增加重复回收、跨池回收和已释放对象访问测试。

