# UI Module

UI Module 管理 Godot Control 场景的异步加载、窗口层级、显示/隐藏/关闭、全屏遮挡、延迟销毁、自动节点绑定和 UI 生命周期。

## 核心类型

- `IUIModule` / `UIModule`：窗口打开、查询和关闭入口。
- `UIWindow`：独立窗口基类。
- `UIWidget`：依附窗口的可复用子组件。
- `WindowAttribute`：声明层级、资源路径、全屏属性和隐藏缓存时间。
- `UIBindAttribute`：按路径或字段名自动绑定场景节点。
- `UILayer`：窗口层级定义。

## 定义窗口

```csharp
[Window(
    UILayer.Normal,
    res://assets/ui/main_menu.tscn,
    fullScreen: true)]
public sealed class MainMenuWindow : UIWindow
{
    [UIBind(Panel/StartButton)]
    private Button _startButton;

    public override void RegisterEvent()
    {
        _startButton.Pressed += OnStartPressed;
    }
}
```

## 打开与关闭

```csharp
IUIModule ui = ModuleSystem.GetModule<IUIModule>();

ui.ShowUIAsync<MainMenuWindow>(window =>
{
    GD.Print(window.IsLoadDone);
});

ui.HideUI<MainMenuWindow>();
ui.CloseUI<MainMenuWindow>();
```

- `HideUI()` 受 `HideTimeToClose` 控制，可暂存窗口节点。
- `CloseUI()` 立即关闭并释放窗口。
- `CloseAll(layer)` 可按层级关闭。
- `IsAnyLoading()` 用于判断是否仍有异步窗口加载任务。

## 使用约定

- UI 类不直接加载自己的场景，由 `WindowAttribute` 和 UIModule 管理。
- 所有信号和 EventModule 订阅必须在窗口销毁路径解绑。
- `UIBind` 字段应与场景结构保持稳定，场景改名后同步更新绑定路径。
- 业务状态应存放在数据模块，窗口只保存展示期状态。

## 当前注意事项

- `UIBind` 使用反射赋值，私有字段可能产生 `CS0649` 警告，需要统一抑制策略。
- 异步打开缺少公开取消句柄，关闭场景时需要防止迟到回调重新挂载窗口。
- 绑定失败应集中报告窗口类型、字段和节点路径，而不是延迟到空引用。
- 窗口类型、资源路径和层级关系建议增加启动期验证及 smoke test。

## 相关文档

- [`scripts/framework/resource/README.md`](../resource/README.md)
- [`scripts/framework/event/README.md`](../event/README.md)

