# ResourceModule 资源管理系统文档

## 目录
1. [概述](#概述)
2. [文件结构](#文件结构)
3. [快速开始](#快速开始)
4. [核心概念](#核心概念)
5. [API 参考](#api-参考)
6. [高级用法](#高级用法)
7. [生命周期与内存管理](#生命周期与内存管理)
8. [扩展与自定义](#扩展与自定义)

---

## 概述

ResourceModule 是基于 **yu 框架**（仿 TEngine 风格）为 **Godot 4.6 C#** 设计的模块化资源管理系统。

核心特性：
- **面向接口**：通过 `IResourceModule` 对外暴露，与具体实现解耦
- **同步 / 异步加载**：同步阻塞加载与 Godot 后台线程异步加载并存
- **LRU 缓存**：O(1) 存取，依赖 Godot 原生引用计数判断淘汰时机
- **请求合并**：同路径并发异步请求自动合并为一个后台任务
- **可替换策略**：加载器（`IResourceLoader`）与缓存（`IResourceCache`）均可注入自定义实现
- **零侵入集成**：注册到 `ModuleSystem`，与框架其他模块（FSM、Procedure）保持一致的生命周期

---

## 文件结构

```
scripts/framework/resource/
├── IResourceModule.cs          # 门面接口（对外使用此接口）
├── ResourceModule.cs           # 模块实现（internal，框架自动发现）
├── ResourceSetting.cs          # 配置（GlobalClass，可在 Godot 编辑器中设置）
├── handle/
│   └── ResourceHandle.cs       # ResourceHandleBase / ResourceHandle<T> / ResourceHandleStatus
├── loader/
│   ├── IResourceLoader.cs      # 加载器策略接口
│   ├── GodotResourceLoader.cs  # 默认实现（Godot 原生 ResourceLoader）
│   └── ResourceLoadTask.cs     # 单次异步加载任务（轮询 LoadThreadedGetStatus）
└── cache/
    ├── IResourceCache.cs       # 缓存策略接口
    └── ResourceCache.cs        # LRU 缓存实现
```

---

## 快速开始

### 1. 注册模块

在 `RootModule._Ready()` 中添加一行：

```csharp
ModuleSystem.GetModule<IResourceModule>();
```

### 2. 配置（可选）

在 Godot 编辑器中：
1. 创建 `ResourceSetting` 资源（`assets/config/settings/resourcesettings.tres` 已预建）
2. 将其赋给 `Settings.resourceSetting` 字段
3. 调整 `MaxCacheSize`、`MaxConcurrentLoadCount`、`EnableLog`

### 3. 获取模块并加载资源

```csharp
var res = ModuleSystem.GetModule<IResourceModule>();

// 同步
var tex = res.LoadAsset<Texture2D>("res://assets/icon.png");

// 异步
res.LoadAssetAsync<PackedScene>("res://scenes/level.tscn")
   .OnCompleted(h => {
       if (h.IsValid) AddChild(h.Asset.Instantiate());
   });
```

---

## 核心概念

### ResourceHandle\<T\>

异步加载的返回值，承载资源及加载状态：

| 属性 / 方法 | 说明 |
|---|---|
| `Asset` | 加载完成的资源实例（`T`），未完成时为 `null` |
| `Status` | `None / Loading / Succeed / Failed` |
| `IsDone` | 是否已完成（成功或失败） |
| `IsValid` | 是否加载成功且资源有效 |
| `Progress` | 加载进度 0~1 |
| `Error` | 失败原因描述 |
| `OnCompleted(cb)` | 注册完成回调，已完成时立即触发，支持链式调用 |

### LRU 缓存与 Godot 引用计数

加载完成后资源的引用持有关系：

| 持有者 | 如何消除引用 |
|---|---|
| `ResourceCache`（缓存字典） | `res.UnloadAsset(path)` 或 `res.UnloadAllAssets()` |
| `ResourceHandle<T>._asset`（句柄） | 调用 `handle.Release()` 或让句柄变量被 GC 回收 |
| 用户代码变量（如 `var tex = handle.Asset`） | 置为 `null` 或超出作用域 |
| 场景节点属性（如 `Sprite2D.Texture`） | 从场景树移除节点或将属性置 `null` |

**彻底销毁一份资源**，需消除所有持有者的引用：

```csharp
// 推荐方式：调用 Release()，一次性清空句柄引用 + 从缓存移除
handle.Release();
// 之后确保用户变量和节点属性也不再持有
myTexture = null;
sprite.Texture = null;
// → Godot 引用计数归零 → 自动释放内存
```

- `ResourceCache.Evict()` 中的 `GetReferenceCount() <= 1` 判断：计数为 1 说明只有缓存持有（无句柄在用），可安全淘汰
- **无需也不应该**在框架层再建一套引用计数，完全依赖 Godot 原生机制

---

## API 参考

### IResourceModule

```csharp
// 同步加载（缓存优先）
T LoadAsset<T>(string path) where T : Resource;

// 异步加载（立即返回句柄，Godot 后台线程加载）
ResourceHandle<T> LoadAssetAsync<T>(string path) where T : Resource;

// 从缓存移除（不强制卸载，交由 Godot 引用计数决定）
void UnloadAsset(string path);

// 清空全部缓存
void UnloadAllAssets();

// 查询是否已缓存
bool HasAsset(string path);

// 当前缓存数量
int CacheCount { get; }

// 替换加载器（须在首次加载前调用）
void SetLoader(IResourceLoader loader);

// 替换缓存（须在首次加载前调用）
void SetCache(IResourceCache cache);
```

---

## 高级用法

### 自定义加载器

```csharp
public class MyHotfixLoader : IResourceLoader
{
    public Resource LoadSync(string path) { /* 热更逻辑 */ }
    public ResourceLoadTask RequestAsync(string path, string typeHint = "") { /* ... */ }
    public bool Exists(string path) { /* ... */ }
}

// 在 OnInit 之后、首次加载前替换
ModuleSystem.GetModule<IResourceModule>().SetLoader(new MyHotfixLoader());
```

### 自定义缓存

```csharp
public class NoCacheImpl : IResourceCache
{
    public int Count => 0;
    public bool TryGet(string path, out Resource r) { r = null; return false; }
    public void Set(string path, Resource r) { }
    public bool Remove(string path) => false;
    public bool Contains(string path) => false;
    public void Clear() { }
}

ModuleSystem.GetModule<IResourceModule>().SetCache(new NoCacheImpl());
```

---

## 生命周期与内存管理

```
ModuleSystem.GetModule<IResourceModule>()
    └─ ResourceModule.OnInit()      // 读取 ResourceSetting，创建 Loader + Cache
           │
     每帧  └─ ResourceModule.Process()   // 轮询所有 pending ResourceLoadTask
                  └─ task.Poll()          // 调用 LoadThreadedGetStatus
                        ├─ InProgress → 继续等待
                        └─ Loaded/Failed → 通知 Handles，写入缓存
           │
    关闭   └─ ResourceModule.Shutdown()  // 清空缓存和任务表
```

**资源释放路径：**

```
UnloadAsset(path)
    └─ cache.Remove(path)   // 缓存不再持有强引用
           └─ 若外部也无引用 → Godot 引用计数 = 0 → 自动释放
```

---

## 扩展与自定义

| 扩展点 | 接口 | 默认实现 |
|---|---|---|
| 加载策略 | `IResourceLoader` | `GodotResourceLoader` |
| 缓存策略 | `IResourceCache` | `ResourceCache`（LRU） |
| 模块整体 | `IResourceModule` | `ResourceModule` |

所有替换操作通过 `IResourceModule.SetLoader()` / `SetCache()` 完成，无需修改框架源码。
