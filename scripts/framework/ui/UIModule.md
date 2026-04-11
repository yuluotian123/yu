# UIModule 使用说明

## 结构概览

UI 模块当前分成四层：

- `IUIModule`：对外入口
- `UIModule`：窗口加载、显示、隐藏、关闭和层级管理
- `UIWindow`：一个完整窗口
- `UIWidget`：窗口内部可复用子组件

和资源模块的协作关系现在也更清楚了：

- `UIModule` 通过 `IResourceModule.LoadSceneAsync()` 请求窗口场景
- `UIWindow` 明确持有 `SceneHandle`
- `SceneHandle` 负责实例化 `PackedScene` 并绑定节点生命周期

## 窗口加载流程

现在窗口加载流程是：

1. `ShowUIAsync<T>()` 创建或复用窗口对象
2. 如果窗口第一次出现，`UIModule` 调用 `LoadSceneAsync()`
3. `UIWindow` 记录当前 `SceneHandle` 和加载版本
4. 加载完成后，`UIModule` 用 `SceneHandle.InstantiateAndBind<Control>(...)` 把控件挂到目标 `CanvasLayer`
5. `UIWindow.InternalDestroy()` 统一释放自己的 `SceneHandle`

这版行为比之前更稳定：

- 窗口加载中再次 `ShowUIAsync()`，不会丢掉新的完成回调
- 窗口加载中被关闭，旧异步回调不会把已关闭窗口重新创建出来
- `UIModule` 不再用模糊的 `IDisposable` 管场景句柄，语义更直接

## 基本用法

```csharp
var ui = ModuleSystem.GetModule<IUIModule>();

ui.ShowUI<MainMenuWindow>("v1.0.0");

ui.ShowUIAsync<BagWindow>(
    win => Debugger.Info($"Opened: {win.WindowName}"),
    "My Bag");
```

关闭与隐藏：

```csharp
ui.CloseUI<MainMenuWindow>();
ui.HideUI<BagWindow>();
ui.CloseAll();
```

## 定义一个窗口

每个 `UIWindow` 子类都应该标记 `[Window]`：

```csharp
[Window(
    layer: UILayer.Normal,
    assetPath: "res://assets/ui/main_menu.tscn",
    fullScreen: true,
    hideTimeToClose: 10f)]
public class MainMenuWindow : UIWindow
{
    [UIBind("%")] private Button _btnStart;
    [UIBind("%")] private Label _labelVersion;

    protected override void OnCreate()
    {
        _btnStart.Pressed += OnStartClicked;
    }

    protected override void OnRefresh()
    {
        _labelVersion.Text = UserDatas?[0] as string ?? "Unknown";
    }
}
```

## UIWindow 现在负责什么

`UIWindow` 现在除了原有 UI 生命周期外，还负责：

- 记录自己的 `SceneHandle`
- 记录当前场景加载版本
- 管理窗口加载完成回调队列
- 在销毁时统一释放 `SceneHandle`

这意味着窗口自己的生命周期语义变得更完整：

- `UIModule` 管窗口集合和显示规则
- `UIWindow` 管自己持有的场景句柄

## Layer 与显示规则

每个 `UILayer` 对应一个 `CanvasLayer`。

`UIModule` 每次窗口变化时会：

- 重新计算同层深度
- 处理同层 `fullScreen` 窗口的遮挡
- 保证 `Active == false` 的窗口不会被误恢复可见

## 推荐约定

- 打开窗口统一走 `ShowUI()` / `ShowUIAsync()`
- 不要在业务层自己手动调用 `LoadSceneAsync()` 再给窗口塞节点
- 需要窗口自动跟场景资源释放联动时，交给 `UIModule + UIWindow + SceneHandle` 这一套默认流程
- 隐藏后希望保留状态时用 `HideUI()`
- 明确不再需要窗口时用 `CloseUI()`
