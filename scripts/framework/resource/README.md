# Resource Module

Resource Module 封装 Godot 资源加载、异步句柄、引用归还、LRU 缓存、场景实例化和运行时 profiler。业务代码应通过 `IResourceModule` 管理长期资源，避免散落 `ResourceLoader.Load()`。

## 核心类型

- `IResourceModule` / `ResourceModule`：加载、释放、缓存和 profiler 入口。
- `ResourceHandle<T>`：资源加载状态、结果、进度和释放句柄。
- `SceneHandle`：`PackedScene` 加载、实例化和 Node 生命周期绑定。
- `IResourceLoader`：实际加载后端。
- `IResourceCache` / `ResourceCache`：LRU 缓存与引用计数。
- `ResourceSetting`：缓存、并发和 profiler 配置。

## 快速开始

同步加载并持有：

```csharp
IResourceModule resources = ModuleSystem.GetModule<IResourceModule>();
ResourceHandle<Texture2D> handle = resources.LoadAsset<Texture2D>(path);
Texture2D texture = handle.Asset;

handle.Release();
```

一次性读取：

```csharp
Texture2D texture = resources.LoadAssetOnce<Texture2D>(path);
```

异步加载：

```csharp
ResourceHandle<Texture2D> handle = resources.LoadAssetAsync<Texture2D>(path);
```

场景加载：

```csharp
SceneHandle scene = resources.LoadSceneAsync(scenePath);
Node instance = scene.Instantiate();
```

具体完成回调和状态判断以 `ResourceHandle<T>` / `SceneHandle` 当前接口为准；使用结束后必须 `Release()` 或 `Dispose()`。

## 缓存与观测

- `GetRefCount(path)` 查看框架引用数。
- `ForceUnloadAsset(path)` 强制移除缓存，调用前确认没有使用者。
- `GetProfilerSnapshot()` 获取句柄、缓存和加载任务快照。
- `ToggleProfilerOverlay()` 在运行时显示资源观测面板。

## 当前注意事项

- 句柄释放必须幂等，业务代码应优先使用明确 owner 绑定或 `using`。
- Godot Resource 自身可能仍被引擎或 Node 引用，框架引用数不等于实际内存立即释放。
- 异步加载需要处理 owner 提前销毁、重复请求和取消需求。
- `ForceUnloadAsset()` 是危险操作，不应作为常规释放方式。
- 缓存容量只按条目数限制，未考虑不同资源的实际内存体积。
