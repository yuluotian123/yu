# ResourceModule 资源管理说明

## 概览

当前资源模块仍然保持原有主结构：

- `Handle + Loader + Cache + Module.Tick()`

这版实现的重点有三件事：

- 通用资源与场景资源分离
- 取消、关闭、引用计数语义收口
- 增加 profiler 快照、日志和可视化浮层

核心职责边界如下：

- `ResourceHandle<T>`：通用资源句柄
- `SceneHandle`：`PackedScene` 专用句柄
- `ResourceModule`：资源入口、主线程收口、profiler 汇总
- `IResourceLoader` / `GodotResourceLoader`：异步加载任务推进
- `IResourceCache` / `ResourceCache`：缓存与框架层引用计数

## 文件结构

```text
scripts/framework/resource/
├── IResourceModule.cs
├── ResourceModule.cs
├── ResourceProfiler.cs
├── ResourceProfilerOverlay.cs
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

## 核心类型

### ResourceHandle<T>

`ResourceHandle<T>` 是通用资源句柄，适用于：

- `Texture2D`
- `AudioStream`
- `Material`
- 配置资源
- `PackedScene` 资源本身

它负责：

- 保存状态、进度、错误
- 提供 `Task`、`OnCompleted()`、`WithCancellation()`
- 管理框架层引用计数归还
- 实现 `IDisposable`

它不负责：

- 场景实例化
- 节点生命周期绑定
- UI 节点树协作

常见写法：

```csharp
using var handle = ModuleSystem.GetModule<IResourceModule>()
    .LoadAsset<Texture2D>("res://assets/icon.png");

if (handle.IsValid)
    sprite.Texture = handle.Asset;
```

异步写法：

```csharp
using var handle = await ModuleSystem.GetModule<IResourceModule>()
    .LoadAssetAsync<Texture2D>("res://assets/icon.png")
    .Task;

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
- 提供 `BindTo(node)`，让句柄跟节点离树自动联动释放

推荐写法：

```csharp
var sceneHandle = ModuleSystem.GetModule<IResourceModule>()
    .LoadSceneAsync("res://assets/scenes/level.tscn");

await sceneHandle.Task;

if (sceneHandle.IsValid)
{
    var level = sceneHandle.InstantiateAndBind<Node>(root);
}
```

如果你只想拿 `PackedScene` 资源本身，而不是处理实例生命周期，也可以继续使用：

```csharp
using var handle = ModuleSystem.GetModule<IResourceModule>()
    .LoadAsset<PackedScene>("res://assets/scenes/level.tscn");
```

### ResourceHandleStatus

当前句柄状态包括：

- `Loading`
- `Succeed`
- `Failed`
- `Cancelled`
- `Released`

其中：

- `Released` 是终态
- 句柄释放后不会再回到 `None`
- 异步完成结果不会再把已终止句柄“复活”

## ResourceModule 的职责

`ResourceModule` 当前只做这些事：

- 对外提供同步/异步加载入口
- 命中缓存时创建句柄并分配框架引用
- 将取消请求收口到主线程处理
- 每帧调用 `_loader.Tick(...)`
- 模块关闭时统一结束未完成请求
- 汇总 profiler 数据
- 管理 profiler 浮层

它不再负责：

- 场景实例化细节
- UI 句柄生命周期逻辑

## UI 与 SceneHandle 的协作

现在 UI 层与资源模块的协作方式已经固定下来：

1. `UIModule.ShowUIAsync()` 调用 `LoadSceneAsync()`
2. `UIWindow` 记录自己的 `SceneHandle` 和加载版本号
3. 加载完成后，`UIModule` 使用 `SceneHandle.InstantiateAndBind<TNode>(Action<TNode>)` 创建控件并挂到 `CanvasLayer`
4. `UIWindow.InternalDestroy()` 统一释放自己的 `SceneHandle`

这样做的结果是：

- `UIWindow` 持有的是明确的 `SceneHandle`，不是模糊的 `IDisposable`
- 重复 `ShowUIAsync()` 时，回调会排队，不会丢
- 加载过程中窗口被关闭时，旧回调不会再把窗口恢复出来
- UI 生命周期不再和通用资源句柄混在一起

## 加载与引用计数语义

### 同步加载

`LoadAsset<T>()` 的流程：

1. 创建 `ResourceHandle<T>`
2. 路径非法时立即 `Failed`
3. 先查缓存
4. 缓存命中则直接完成句柄
5. 否则走 `_loader.LoadSync(path)`
6. 成功后写入缓存，并在类型匹配时增加引用计数

### 异步加载

`LoadAssetAsync<T>()` 的流程：

1. 创建 `ResourceHandle<T>`
2. 路径非法时立即 `Failed`
3. 先查缓存
4. 缓存命中则立即完成
5. 否则交给 loader 发起/合并异步请求

### 引用计数

只有在句柄真正成功且类型匹配时，才会：

- `_cache.Acquire(path)`
- `handle.MarkReferenceAcquiredInternal()`

这避免了错误类型加载导致的误增引用问题。

## 取消与关闭

### CancellationToken

`WithCancellation()` 当前不会在线程回调里直接改句柄状态，而是：

1. 把取消请求提交给 `ResourceModule`
2. 在 `Process()` 中由主线程统一调用 `SetCancelledInternal()`

这样状态流更稳定，也更容易推理。

### Shutdown

模块关闭顺序：

1. 先冲刷主线程取消队列
2. 调用 `_loader.Shutdown(...)`
3. 清空缓存
4. 释放 profiler 浮层

目标是保证：

- 不留下永久 `Loading` 的句柄
- 等待中的 `Task` 能结束
- 关闭时所有在途任务都有一致的收口语义

## Profiler

当前资源 profiler 分为三层：

- 快照接口
- 日志输出
- 运行时浮层

### 1. 快照接口

```csharp
var resource = ModuleSystem.GetModule<IResourceModule>();
var snapshot = resource.GetProfilerSnapshot();

Debugger.Info($"LiveHandles={snapshot.LiveHandleCount}");
Debugger.Info($"CacheCount={snapshot.CacheCount}");
Debugger.Info($"LoaderActive={snapshot.Loader.ActiveCount}");
```

快照包含：

- 句柄状态统计
- 句柄条目列表
- 缓存条目列表
- loader 状态与任务列表
- 待取消队列数量

### 2. 日志输出

```csharp
ModuleSystem.GetModule<IResourceModule>()
    .DumpProfilerToLog(includeHandles: true, includeCacheEntries: true);
```

日志会输出：

- 汇总统计
- loader 任务列表
- cache 条目
- handle 条目

适合快速排查：

- 为什么某个资源一直 `Loading`
- 为什么缓存里还有某个路径
- 为什么引用计数没有归零
- 为什么等待队列堆积

### 3. 可视化浮层

资源模块内置了运行时 profiler 浮层，默认挂在场景根节点下，不依赖业务 UI 窗口系统。

默认热键：

- `` ` ``：显示 / 隐藏浮层
- `F10`：将当前快照输出到日志

之所以不再使用 `F8`，是因为在 Godot 编辑器里运行项目时，`F8` 会被编辑器当作“停止运行”快捷键，游戏窗口会直接退出。

浮层显示内容包括：

- Summary
- Loader Tasks
- Cache Entries
- Handle Entries
- `GC Collect` 按钮，用于直接触发 `GC.Collect()`

代码控制方式：

```csharp
var resource = ModuleSystem.GetModule<IResourceModule>();

resource.SetProfilerOverlayVisible(true);
resource.ToggleProfilerOverlay();
var visible = resource.IsProfilerOverlayVisible;
```

### Profiler 的一个重要细节

`ResourceModule` 内部用 `WeakReference<ResourceHandleBase>` 跟踪句柄对象，所以 profiler 显示的是“当前还活着的句柄对象”，不只是“当前有效句柄”。

这意味着：

- 某个 handle 即使已经 `Released`
- 只要对象还没有被 GC 回收
- profiler 里仍然可能看到它，状态会显示为 `Released`

所以判断是否泄漏时，要先看：

- `Status` 是不是已经 `Released`
- `OwnsReference` 是否已经是 `false`

不要只看它“还在列表里”。

## ResourceSetting

当前 `ResourceSetting` 支持这些配置：

- `MaxCacheSize`
- `MaxConcurrentLoadCount`
- `EnableLog`
- `EnableProfilerOverlay`
- `ShowProfilerOverlayOnStart`
- `ProfilerOverlayRefreshInterval`
- `ProfilerOverlayMaxRows`

其中：

- `EnableProfilerOverlay` 控制是否自动创建 profiler 浮层
- `ShowProfilerOverlayOnStart` 控制启动时是否默认显示
- `ProfilerOverlayRefreshInterval` 控制刷新频率
- `ProfilerOverlayMaxRows` 控制浮层每个区块最多显示多少行

## 对外 API

### IResourceModule

```csharp
ResourceHandle<T> LoadAsset<T>(string path) where T : Resource;
T LoadAssetOnce<T>(string path) where T : Resource;
bool TryLoadAssetOnce<T>(string path, out T asset) where T : Resource;
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

### Profiler Types

```csharp
ResourceProfilerSnapshot
ResourceHandleProfilerEntry
ResourceCacheProfilerEntry
ResourceLoaderProfilerSnapshot
ResourceLoadTaskProfilerEntry
```

## 推荐约定

- 普通资源优先使用 `using var handle = ...`
- 只拿 `PackedScene` 资源本身时使用 `LoadAsset<PackedScene>()`
- 需要实例化场景时优先使用 `LoadSceneAsync()`
- 需要自动释放时优先使用 `InstantiateAndBind()` / `BindTo()`
- 排查资源状态时优先使用 `GetProfilerSnapshot()` 或 `DumpProfilerToLog()`
- 如果只是在 profiler 里看见某个 handle，不要立刻判定泄漏，先看它是否已 `Released`

## 后续扩展边界

后续继续扩展时，建议保持这条边界不动：

- 通用资源能力继续放在 `ResourceHandle<T>`
- 场景与实例节点能力继续放在 `SceneHandle`
- 缓存观测能力继续放在 `IResourceCache`
- 加载观测能力继续放在 `IResourceLoader`
- `ResourceModule` 继续只做编排、收口和 profiler 汇总
