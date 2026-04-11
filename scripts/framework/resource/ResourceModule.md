# ResourceModule 资源管理说明

## 设计目标

资源模块仍然保持原来的主结构：

- `Handle + Loader + Cache + Module.Tick()`

这轮调整主要做了两件事：

- 把通用资源与场景资源的职责继续拆清楚
- 加入 profiler，方便直接追踪资源状态

当前边界如下：

- `ResourceHandle<T>` 只负责通用资源加载语义
- `SceneHandle` 只负责 `PackedScene` 的实例化和节点绑定语义
- `ResourceModule` 负责入口、缓存命中、主线程取消和 profiler 汇总
- `IResourceLoader` / `GodotResourceLoader` 负责异步任务推进
- `IResourceCache` / `ResourceCache` 负责缓存和框架层引用计数

## 文件结构

```text
scripts/framework/resource/
├── IResourceModule.cs
├── ResourceModule.cs
├── ResourceProfiler.cs
├── ResourceSetting.cs
├── handle/
│   ├── ResourceHandle.cs
│   └── SceneHandle.cs
├── loader/
│   ├── IResourceLoader.cs
│   ├── GodotResourceLoader.cs
│   └── ResourceLoadTask.cs
└── cache/
    ├── IResourceCache.cs
    └── ResourceCache.cs
```

## 核心职责

### ResourceHandle<T>

`ResourceHandle<T>` 是通用资源句柄，适用于纹理、音频、材质、配置和 `PackedScene` 资源本身。

它负责：

- 保存状态、进度、错误
- 提供 `Task`、`OnCompleted()`、`WithCancellation()`
- 持有并归还框架层引用
- 实现 `IDisposable`

它不负责：

- 场景实例化
- 节点生命周期绑定
- UI 场景树逻辑

推荐写法：

```csharp
using var handle = ModuleSystem.GetModule<IResourceModule>()
    .LoadAsset<Texture2D>("res://assets/icon.png");

if (handle.IsValid)
    sprite.Texture = handle.Asset;
```

### SceneHandle

`SceneHandle` 是 `PackedScene` 的专用包装句柄，内部持有 `ResourceHandle<PackedScene>`。

它负责：

- 暴露场景加载状态
- 提供 `Instantiate()` / `Instantiate<TNode>()`
- 提供 `InstantiateAndBind<TNode>(Node parent)`
- 提供 `InstantiateAndBind<TNode>(Action<TNode> attachInstance)`
- 提供 `BindTo(node)`，让句柄随节点离树自动释放

这样做的好处是：

- 场景相关 API 全部集中在 `SceneHandle`
- 泛型资源句柄不再背负 UI/场景生命周期语义
- 调用方一眼能看出自己拿到的是“资源句柄”还是“场景句柄”

常见用法：

```csharp
var sceneHandle = ModuleSystem.GetModule<IResourceModule>()
    .LoadSceneAsync("res://assets/scenes/level.tscn");

await sceneHandle.Task;

if (sceneHandle.IsValid)
{
    var level = sceneHandle.InstantiateAndBind<Node>(root);
}
```

### UIModule 与 SceneHandle 的协作

现在 UI 这一层不再把场景句柄当成模糊的 `IDisposable` 使用，而是明确由窗口持有 `SceneHandle`。

窗口加载流程现在是：

1. `UIModule.ShowUIAsync()` 请求 `LoadSceneAsync()`
2. `UIWindow` 记录当前 `SceneHandle` 和加载版本号
3. 加载完成后，`UIModule` 通过 `SceneHandle.InstantiateAndBind<TNode>(Action<TNode>)` 创建控件并挂到对应 `CanvasLayer`
4. `UIWindow.InternalDestroy()` 统一释放自己的 `SceneHandle`

这样解决了几个之前容易混乱的点：

- 窗口真正拥有的是“场景句柄”，语义更明确
- 重复 `ShowUIAsync()` 时，回调会排队，不会丢
- 窗口在加载中被关闭时，旧回调不会再把已经关闭的窗口“复活”
- `UIModule` 不需要自己手动处理 `BindTo(control)` 和泛用 `Dispose()` 的混搭逻辑

## 生命周期语义

### ResourceHandleStatus

句柄状态包括：

- `Loading`
- `Succeed`
- `Failed`
- `Cancelled`
- `Released`

`Released` 是终态。句柄一旦释放，不会再回到 `None`，也不会再被异步完成结果覆盖。

### Release / Dispose

通用资源：

- 推荐 `using var handle = ...`
- 或在 `await` / `OnCompleted()` 后显式 `Dispose()`

场景资源：

- 推荐使用 `InstantiateAndBind()`
- 或手动 `BindTo(node)`

这样可以尽量减少手动 `Release()` 遗漏。

## Profiler

资源 profiler 现在覆盖三类信息：

- 句柄状态：路径、请求类型、状态、进度、是否持有引用、错误
- 缓存状态：缓存路径、资源类型、引用计数、LRU 顺序
- Loader 状态：并发槽、等待队列、在途任务、任务进度、合并请求数

### 获取快照

```csharp
var resource = ModuleSystem.GetModule<IResourceModule>();
var snapshot = resource.GetProfilerSnapshot();

Debugger.Info($"LiveHandles={snapshot.LiveHandleCount}");
Debugger.Info($"CacheCount={snapshot.CacheCount}");
Debugger.Info($"LoaderActive={snapshot.Loader.ActiveCount}");
```

### 直接打日志

```csharp
ModuleSystem.GetModule<IResourceModule>()
    .DumpProfilerToLog(includeHandles: true, includeCacheEntries: true);
```

### 可视化浮层

资源模块现在内置了一个运行时 profiler 浮层。

- `` ` ``：显示 / 隐藏资源 profiler 面板
- `F10`：把当前 profiler 快照直接打印到日志

之所以不再使用 `F8`，是因为在 Godot 编辑器里运行项目时，`F8` 会被编辑器当成“停止运行”快捷键，游戏窗口会直接退出。

浮层内容包括：

- 汇总统计
- loader 在途任务
- cache 条目
- handle 条目

如果你想从代码里控制它，也可以直接调用：

```csharp
var resource = ModuleSystem.GetModule<IResourceModule>();

resource.SetProfilerOverlayVisible(true);
resource.ToggleProfilerOverlay();
var visible = resource.IsProfilerOverlayVisible;
```

相关配置在 `ResourceSetting`：

- `EnableProfilerOverlay`
- `ShowProfilerOverlayOnStart`
- `ProfilerOverlayRefreshInterval`
- `ProfilerOverlayMaxRows`

日志会输出：

- 汇总统计
- 当前 loader 任务列表
- 缓存条目列表
- 句柄列表

这很适合先快速定位：

- 为什么某个资源一直 `Loading`
- 为什么缓存里一直有某个路径
- 为什么引用计数没回到 0
- 为什么等待队列堆积

## 模块关闭顺序

模块 `Shutdown()` 的顺序是：

1. 先冲刷主线程取消队列
2. 调用 `_loader.Shutdown(...)`
3. 再清空缓存

这样可以保证：

- 不留下永久 `Loading` 的句柄
- 异步等待中的 `Task` 能正常结束
- 关闭时 profiler 也能看到一致的收口结果

## 对外 API

### IResourceModule

```csharp
ResourceHandle<T> LoadAsset<T>(string path) where T : Resource;
ResourceHandle<T> LoadAssetAsync<T>(string path) where T : Resource;
SceneHandle LoadSceneAsync(string path);

void ForceUnloadAsset(string path);
void UnloadAllAssets();
bool HasAsset(string path);
int CacheCount { get; }
int GetRefCount(string path);
ResourceProfilerSnapshot GetProfilerSnapshot();
void DumpProfilerToLog(bool includeHandles = true, bool includeCacheEntries = true);
bool IsProfilerOverlayVisible { get; }
void SetProfilerOverlayVisible(bool visible);
void ToggleProfilerOverlay();
void SetLoader(IResourceLoader loader);
void SetCache(IResourceCache cache);
void ReleaseAsset(string path);
```

### ResourceHandle<T>

```csharp
T Asset { get; }
ResourceHandleStatus Status { get; }
bool IsDone { get; }
bool IsValid { get; }
float Progress { get; }
string Error { get; }
Task<ResourceHandle<T>> Task { get; }

ResourceHandle<T> OnCompleted(Action<ResourceHandle<T>> callback);
ResourceHandle<T> WithCancellation(CancellationToken ct);
void Release();
void Dispose();
```

### SceneHandle

```csharp
PackedScene Scene { get; }
ResourceHandleStatus Status { get; }
bool IsDone { get; }
bool IsValid { get; }
float Progress { get; }
string Error { get; }
Task<SceneHandle> Task { get; }

SceneHandle OnCompleted(Action<SceneHandle> callback);
SceneHandle WithCancellation(CancellationToken ct);
Node Instantiate();
TNode Instantiate<TNode>() where TNode : Node;
TNode InstantiateAndBind<TNode>(Node parent) where TNode : Node;
TNode InstantiateAndBind<TNode>(Action<TNode> attachInstance) where TNode : Node;
SceneHandle BindTo(Node node);
void Release();
void Dispose();
```

### Profiler Snapshot Types

```csharp
ResourceProfilerSnapshot
ResourceHandleProfilerEntry
ResourceCacheProfilerEntry
ResourceLoaderProfilerSnapshot
ResourceLoadTaskProfilerEntry
```

## 推荐约定

- 普通资源优先用 `using var handle = ...`
- 只拿 `PackedScene` 资源本身时用 `LoadAsset<PackedScene>()`
- 需要实例化场景时优先用 `LoadSceneAsync()`
- 需要自动释放时优先用 `InstantiateAndBind()` / `BindTo()`
- 需要排查资源问题时优先用 `GetProfilerSnapshot()` 或 `DumpProfilerToLog()`

## 后续扩展边界

后续如果继续扩展，建议保持这条边界不动：

- 通用资源能力继续放 `ResourceHandle<T>`
- 场景与实例节点能力继续放 `SceneHandle`
- 缓存观测能力继续放 `IResourceCache`
- 加载观测能力继续放 `IResourceLoader`
- 模块层继续只做编排、收口和 profiler 汇总
