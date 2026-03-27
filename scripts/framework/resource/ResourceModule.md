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
- **统一句柄管理**：同步/异步加载均返回 `ResourceHandle<T>`，每个句柄持有一个框架引用计数
- **框架引用计数 + LRU 缓存**：引用计数归零的资源才可被 LRU 淘汰，避免正在使用的资源被误淘汰
- **请求合并**：同路径并发异步请求自动合并为一个后台任务，每个句柄各持有一个引用计数
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
    ├── IResourceCache.cs       # 缓存策略接口（含 Acquire/Release/GetRefCount）
    └── ResourceCache.cs        # LRU 缓存实现（引用计数 + LRU 淘汰）
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

// 同步加载（返回句柄，引用计数 +1）
var handle = res.LoadAsset<Texture2D>("res://assets/icon.png");
if (handle.IsValid)
{
    sprite.Texture = handle.Asset;
}
// 使用完毕后释放（引用计数 -1）
handle.Release();

// 异步加载（返回句柄，加载成功后引用计数 +1）
var asyncHandle = res.LoadAssetAsync<PackedScene>("res://scenes/level.tscn")
   .OnCompleted(h => {
       if (h.IsValid) AddChild(h.Asset.Instantiate());
       // 不再需要时释放
       // h.Release();
   });
```

---

## 核心概念

### ResourceHandle\<T\> — 统一资源句柄

**所有加载接口（同步和异步）均返回 `ResourceHandle<T>`**，每个有效句柄持有一个框架引用计数。

| 属性 / 方法 | 说明 |
|---|---|
| `Asset` | 加载完成的资源实例（`T`），未完成时为 `null` |
| `Status` | `None / Loading / Succeed / Failed` |
| `IsDone` | 是否已完成（成功或失败） |
| `IsValid` | 是否加载成功且资源有效 |
| `Progress` | 加载进度 0~1 |
| `Error` | 失败原因描述 |
| `OnCompleted(cb)` | 注册完成回调，已完成时立即触发，支持链式调用 |
| **`Release()`** | **释放句柄：框架引用计数 -1，清空 Asset 引用。幂等，可安全多次调用** |

### 双层引用计数机制

资源的生命周期由**框架引用计数**和 **Godot 引用计数**两层共同管理：

| 层级 | 作用 | 控制方式 |
|---|---|---|
| **框架引用计数** | 决定资源是否可被缓存淘汰 | `Handle.Release()` 减 1，归零后可被 LRU 淘汰 |
| **Godot 引用计数** | 决定资源内存是否释放 | 当无任何 C# 变量/节点引用时自动归零释放 |

**引用计数流转：**

```
LoadAsset / LoadAssetAsync（成功）→ 框架 RefCount +1
     ↓
handle.Release()                  → 框架 RefCount -1
     ↓
RefCount == 0                     → 资源变为"可淘汰"
     ↓
缓存满，LRU 淘汰                  → 从缓存移除（Godot 引用 -1）
     ↓
无其他 C# 引用                    → Godot 引用计数归零 → 内存释放
```

### LRU 缓存淘汰策略

- 缓存容量由 `ResourceSetting.MaxCacheSize` 控制（默认 128）
- 当缓存满时，**只淘汰 `RefCount == 0` 的资源**（从最近最少使用的开始）
- 若所有资源的 `RefCount > 0`，则新资源仍会强制加入缓存（超容量运行），并发出警告日志
- `ForceUnloadAsset(path)` 可无视引用计数强制移除

### 多句柄共享

对同一路径的多次加载会命中缓存，但每次调用都返回一个新句柄、各自持有一个引用计数：

```csharp
var h1 = res.LoadAsset<Texture2D>("res://icon.png");  // RefCount = 1
var h2 = res.LoadAsset<Texture2D>("res://icon.png");  // RefCount = 2（同一资源）
h1.Release();  // RefCount = 1
h2.Release();  // RefCount = 0 → 可被 LRU 淘汰
```

异步加载合并请求时同理，每个成功的句柄各持有一个引用。

---

## API 参考

### IResourceModule

```csharp
// 同步加载（返回句柄，成功时 RefCount +1）
ResourceHandle<T> LoadAsset<T>(string path) where T : Resource;

// 异步加载（返回句柄，成功时 RefCount +1）
ResourceHandle<T> LoadAssetAsync<T>(string path) where T : Resource;

// 释放引用计数（由 Handle.Release() 内部调用，不建议外部直接使用）
void ReleaseAsset(string path);

// 强制从缓存移除（无视引用计数，适用于场景切换）
void ForceUnloadAsset(string path);

// 清空全部缓存（强制）
void UnloadAllAssets();

// 查询是否已缓存
bool HasAsset(string path);

// 当前缓存数量
int CacheCount { get; }

// 获取指定资源的框架引用计数
int GetRefCount(string path);

// 替换加载器（须在首次加载前调用）
void SetLoader(IResourceLoader loader);

// 替换缓存（须在首次加载前调用）
void SetCache(IResourceCache cache);
```

### IResourceCache

```csharp
int Count { get; }
bool TryGet(string path, out Resource resource);
void Set(string path, Resource resource);
bool Remove(string path);
bool Contains(string path);
void Clear();

// 引用计数管理
void Acquire(string path);       // RefCount +1
void Release(string path);       // RefCount -1（不低于 0）
int GetRefCount(string path);    // 查询当前 RefCount
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
    public void Acquire(string path) { }
    public void Release(string path) { }
    public int GetRefCount(string path) => 0;
}

ModuleSystem.GetModule<IResourceModule>().SetCache(new NoCacheImpl());
```

### 框架模块中的使用模式

**UIModule**：打开窗口时保存 Handle 到 `UIWindow.ResourceHandle`，关闭时自动 `Release()`。

**ObjectPoolModule**：创建 NodePool 时通过 Handle 加载 PackedScene，销毁池时 `ForceUnloadAsset()`。

**Procedure**：在 `OnLeave` 中释放 Handle，确保场景切换时正确减引用。

---

## 生命周期与内存管理

```
ModuleSystem.GetModule<IResourceModule>()
    └─ ResourceModule.OnInit()      // 读取 ResourceSetting，创建 Loader + Cache
           │
     每帧  └─ ResourceModule.Process()   // 轮询所有 pending ResourceLoadTask
                  └─ task.Poll()          // 调用 LoadThreadedGetStatus
                        ├─ InProgress → 继续等待
                        └─ Loaded → 写入缓存 + 为每个成功 Handle Acquire
                        └─ Failed → 通知 Handle 失败
           │
    关闭   └─ ResourceModule.Shutdown()  // 清空缓存和任务表
```

**资源释放路径：**

```
handle.Release()
    └─ ResourceModule.ReleaseAsset(path)
        └─ cache.Release(path)        // 框架 RefCount -1
              │
              ├─ RefCount > 0 → 资源仍受保护，不可被淘汰
              │
              └─ RefCount == 0 → 资源变为"可淘汰"
                    └─ 下次缓存满时 LRU 淘汰
                          └─ cache.Remove(path)   // 缓存不再持有
                                └─ 若外部也无引用 → Godot 引用计数 = 0 → 自动释放内存
```

**强制释放路径（场景切换等场景）：**

```
ForceUnloadAsset(path)
    └─ cache.Remove(path)            // 直接移除，无视 RefCount
           └─ 若外部也无引用 → Godot 引用计数 = 0 → 自动释放
```

> ⚠️ 注意：`ForceUnloadAsset` 不会使已发出的 Handle 失效，仍持有 `_asset` 引用的代码仍可继续使用该资源，但资源不再受缓存管理。

---

## 扩展与自定义

| 扩展点 | 接口 | 默认实现 |
|---|---|---|
| 加载策略 | `IResourceLoader` | `GodotResourceLoader` |
| 缓存策略 | `IResourceCache` | `ResourceCache`（LRU + 引用计数） |
| 模块整体 | `IResourceModule` | `ResourceModule` |

所有替换操作通过 `IResourceModule.SetLoader()` / `SetCache()` 完成，无需修改框架源码。
