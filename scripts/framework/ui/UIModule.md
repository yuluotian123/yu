# UI 模块使用文档

> **命名空间**：`Framework.UI`  
> **对应目录**：`scripts/framework/ui/`

---

## 目录

1. [架构概览](#1-架构概览)
2. [层级系统 UILayer](#2-层级系统-uilayer)
3. [创建一个窗口](#3-创建一个窗口)
   - 3.1 [WindowAttribute 标记](#31-windowattribute-标记)
   - 3.2 [UIBind 节点绑定](#32-uibind-节点绑定)
   - 3.3 [生命周期回调](#33-生命周期回调)
4. [打开 / 关闭 / 隐藏窗口](#4-打开--关闭--隐藏窗口)
5. [查询与状态](#5-查询与状态)
6. [UIWidget 子组件](#6-uiwidget-子组件)
7. [事件系统集成](#7-事件系统集成)
8. [全屏遮挡机制](#8-全屏遮挡机制)
9. [完整示例](#9-完整示例)

---

## 1. 架构概览

```
IUIModule（接口）
   └── UIModule（实现）
           ├── UILayer（CanvasLayer 枚举）
           └── UIWindow（窗口基类，继承 UIBase）
                   └── UIWidget（组件基类，继承 UIBase）
```

| 类 / 接口 | 职责 |
|-----------|------|
| `IUIModule` | 对外暴露的 UI 操作接口，通过 `ModuleSystem.GetModule<IUIModule>()` 获取 |
| `UIModule` | 管理所有窗口的加载、排序、显隐及层级 |
| `UIWindow` | 代表一个完整 UI 面板，每个面板对应一个 Godot `.tscn` 场景 |
| `UIWidget` | 可复用的 UI 子组件，附属于某个 `UIWindow` |
| `UILayer` | 枚举，定义 CanvasLayer 渲染层级 |
| `WindowAttribute` | C# Attribute，为 `UIWindow` 子类声明层级、资源路径等元数据 |
| `UIBindAttribute` | C# Attribute，自动将场景树节点绑定到字段 |

---

## 2. 层级系统 UILayer

每个 `UILayer` 对应一个独立的 Godot `CanvasLayer`，Layer 值越大渲染越靠前：

| 枚举值 | CanvasLayer 值 | 用途建议 |
|--------|---------------|---------|
| `Background` | 0 | 全屏背景、天空盒 UI |
| `Normal` | 10 | 常规游戏面板、HUD |
| `High` | 20 | 二级弹窗、提示面板 |
| `Modal` | 30 | 模态对话框、半透明遮罩 |
| `System` | 40 | 加载界面、系统提示，覆盖所有 UI |
| `Tips` | 50 | 飘字、Toast、引导遮罩等最顶层内容 |

---

## 3. 创建一个窗口

### 3.1 WindowAttribute 标记

每个 `UIWindow` 子类**必须**使用 `[Window]` 特性声明其元数据：

```csharp
[Window(
    layer: UILayer.Normal,         // 所在层级（必填）
    assetPath: "res://assets/ui/main_menu.tscn",  // PackedScene 路径（必填）
    fullScreen: true,              // 是否全屏遮挡同层其他窗口（默认 false）
    hideTimeToClose: 10f           // HideUI 后多少秒自动销毁（默认 10s；≤0 立即销毁）
)]
public class MainMenuWindow : UIWindow { }
```

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `layer` | `UILayer` | — | 渲染层级（必填） |
| `assetPath` | `string` | `""` | `.tscn` 资源路径（res:// 格式） |
| `fullScreen` | `bool` | `false` | 打开时是否遮挡同层较低窗口 |
| `hideTimeToClose` | `float` | `10f` | HideUI 后的缓存存活时间（秒） |

### 3.2 UIBind 节点绑定

在字段上标记 `[UIBind]`，框架会在 `OnCreate` 前自动通过反射从场景树查找并赋值，无需手动 `GetNode`。

**三种绑定方式：**

```csharp
// 方式 1：指定精确路径
[UIBind("Panel/Header/Title")]
private Label _titleLabel;

// 方式 2：路径为空，从字段名推断（去掉 _ / m_ 前缀，首字母大写）
// _titleLabel → 节点名 "TitleLabel"
[UIBind]
private Label _titleLabel;

// 方式 3：Godot UniqueNode（场景中右键节点 → Access as Unique Name）
// _btnStart → GetNode("%BtnStart")
[UIBind("%")]
private Button _btnStart;
```

> **字段名推断规则**：去掉 `_` 或 `m_` 前缀，首字母大写。  
> 例：`_btnClose` → `BtnClose`，`m_labelTitle` → `LabelTitle`

### 3.3 生命周期回调

重写以下虚方法实现窗口逻辑：

| 方法 | 调用时机 | 典型用途 |
|------|----------|---------|
| `BindMemberProperty()` | 首次创建，AutoBind 完成后 | 手动节点绑定、初始化成员 |
| `RegisterEvent()` | 首次创建，`BindMemberProperty` 之后 | 注册 UI 事件（`AddUIEvent`） |
| `OnCreate()` | 首次创建完成后（调用一次） | 绑定按钮信号、创建子 Widget |
| `OnRefresh()` | 每次 `ShowUI` / 刷新时 | 用 `UserDatas` 刷新显示数据 |
| `OnUpdate(delta)` | 每帧（仅窗口可见且已初始化） | 逐帧动画、倒计时逻辑 |
| `OnDestroy()` | 窗口关闭销毁前 | 释放资源、取消订阅外部事件 |
| `OnSetVisible(visible)` | 显隐状态改变时 | 播放显隐动画 |

**完整回调顺序（首次打开）：**

```
AutoBind → BindMemberProperty → RegisterEvent → OnCreate → OnRefresh
```

**再次打开（已存在的窗口）：**

```
OnRefresh（直接刷新数据，不重走 Create 流程）
```

---

## 4. 打开 / 关闭 / 隐藏窗口

首先获取 UI 模块：

```csharp
var ui = ModuleSystem.GetModule<IUIModule>();
```

### 打开窗口

```csharp
// 同步打开（资源已缓存立即显示，否则等待异步加载后自动显示）
ui.ShowUI<MainMenuWindow>();

// 带 UserData 参数（可传任意数量、任意类型）
ui.ShowUI<MainMenuWindow>("玩家姓名", 100);

// 异步打开，加载完成后回调
ui.ShowUIAsync<BagWindow>(
    onComplete: win => { /* 可对窗口做额外操作 */ },
    userData: bagData
);
```

> `ShowUI` 内部就是 `ShowUIAsync` 的无回调版本，两者等价。  
> 若窗口**已存在**（含隐藏状态），会直接置顶并触发 `OnRefresh`，不会重复加载。

### 关闭窗口（销毁节点）

```csharp
// 按类型关闭
ui.CloseUI<MainMenuWindow>();

// 按实例关闭（适合在窗口内部调用）
ui.CloseUI(this);

// 关闭某层所有窗口
ui.CloseAll(UILayer.Normal);

// 关闭全部窗口
ui.CloseAll();
```

### 隐藏窗口（节点保留，缓存复用）

```csharp
// 隐藏：节点保留在树中，超过 HideTimeToClose 秒后自动销毁
ui.HideUI<BagWindow>();
```

> **HideUI vs CloseUI：**  
> - `HideUI`：设置 `Active = false`，节点不销毁，保留状态，超时后自动 `CloseUI`。  
> - `CloseUI`：立即销毁节点，释放资源，调用 `OnDestroy`。

---

## 5. 查询与状态

```csharp
// 是否存在指定窗口（含隐藏/加载中）
bool exists = ui.HasWindow<BagWindow>();

// 获取窗口实例（不存在返回 null）
var bag = ui.GetWindow<BagWindow>();

// 当前最顶层可见窗口名称
string topName = ui.GetTopWindowName();

// 指定层最顶层可见窗口名称
string topNormal = ui.GetTopWindowName(UILayer.Normal);

// 是否有窗口正在异步加载中
bool loading = ui.IsAnyLoading();

// 已打开窗口总数（含隐藏）
int count = ui.WindowCount;
```

---

## 6. UIWidget 子组件

`UIWidget` 用于将复杂窗口拆分为独立的可复用组件，每个 Widget 拥有独立的生命周期与事件订阅。

### 定义 Widget

```csharp
public class TabButtonWidget : UIWidget
{
    [UIBind("%")] private Button _btnTab;
    [UIBind("%")] private Label  _textName;

    private int _tabIndex;
    private System.Action<int> _onClick;

    protected override void OnCreate()
    {
        _btnTab.Pressed += () => _onClick?.Invoke(_tabIndex);
    }

    // 由外部（Window）调用，设置数据
    public void SetData(string name, int index, System.Action<int> onClick)
    {
        _textName.Text = name;
        _tabIndex      = index;
        _onClick       = onClick;
    }
}
```

### 在 Window 中创建 Widget

```csharp
public class ShopWindow : UIWindow
{
    private TabButtonWidget _tabEquip;
    private TabButtonWidget _tabConsume;

    protected override void OnCreate()
    {
        // 通过节点路径创建（路径相对于 Window 的 Owner）
        _tabEquip   = CreateWidget<TabButtonWidget>("Tabs/TabEquip");
        _tabConsume = CreateWidget<TabButtonWidget>("Tabs/TabConsume");

        _tabEquip.SetData("装备", 0, OnTabClicked);
        _tabConsume.SetData("消耗品", 1, OnTabClicked);
    }

    protected override void OnDestroy()
    {
        // Widget 在 Window 销毁时会被框架自动销毁，也可手动提前销毁：
        // DestroyWidget(_tabEquip);
    }
}
```

> Widget 的 `OnCreate`、`OnRefresh`、`OnUpdate`、`OnDestroy` 均由父级（Window）的同名方法**级联触发**，无需手动调用。

---

## 7. 事件系统集成

在 `UIBase` 子类中使用 `AddUIEvent` 订阅事件，**框架会在窗口/Widget 销毁时自动取消订阅**，无需在 `OnDestroy` 中手动清理。

```csharp
protected override void RegisterEvent()
{
    // 无参数事件
    AddUIEvent(EventId.GamePause, OnGamePause);

    // 带 1 个参数
    AddUIEvent<string>(EventId.PlayerNameChanged, OnPlayerNameChanged);

    // 带 2 个参数
    AddUIEvent<int, int>(EventId.ScoreUpdated, OnScoreUpdated);
}

private void OnGamePause() { /* ... */ }
private void OnPlayerNameChanged(string name) { /* ... */ }
private void OnScoreUpdated(int oldScore, int newScore) { /* ... */ }
```

> 推荐在 `RegisterEvent()` 中集中注册，便于维护。

---

## 8. 全屏遮挡机制

当一个 `fullScreen: true` 的窗口被打开时，**同层中深度更低的所有窗口** `Owner.Visible` 会被设为 `false`（但 `Active` 不变）。全屏窗口关闭后，之前被遮挡的窗口自动恢复。

```
// 示例：打开全屏主菜单
ui.ShowUI<MainMenuWindow>();  // fullScreen: true, Layer: Normal

// → Normal 层中排在主菜单下方的所有窗口 Visible = false
// → 主菜单关闭后，它们自动恢复可见（前提是 Active 仍为 true）
```

**关键区别：**

| 状态字段 | 含义 | 由谁控制 |
|----------|------|---------|
| `Active` | 业务层激活意图 | `ShowUI` / `HideUI` |
| `Owner.Visible` | Godot 节点实际渲染 | 框架根据 `Active` + 全屏遮挡综合写入 |

`HideUI` 后 `Active = false`，即使全屏窗口关闭，被隐藏的窗口**不会**自动恢复显示。

---

## 9. 完整示例

### 定义窗口（BagWindow.cs）

```csharp
using Framework;
using Framework.UI;
using Godot;

namespace GameLogic.UI
{
    // 场景节点结构：
    //   BagWindow (Control)
    //     ├── %BtnClose    (Button)
    //     └── %LabelTitle  (Label)
    [Window(UILayer.High, "res://assets/minigame/ui/bag_window.tscn",
            fullScreen: false, hideTimeToClose: 30f)]
    public class BagWindow : UIWindow
    {
        [UIBind("%")] private Button _btnClose;
        [UIBind("%")] private Label  _labelTitle;

        protected override void OnCreate()
        {
            _btnClose.Pressed += OnCloseClicked;
        }

        protected override void OnRefresh()
        {
            // UserDatas[0] 是打开时传入的标题字符串
            string title = UserDatas?.Length > 0 ? UserDatas[0] as string : "背包";
            _labelTitle.Text = title;
        }

        protected override void OnDestroy()
        {
            // 此处做清理工作（事件已由框架自动取消订阅）
        }

        private void OnCloseClicked()
        {
            // HideUI：节点保留，30 秒后自动销毁
            ModuleSystem.GetModule<IUIModule>().HideUI<BagWindow>();
        }
    }
}
```

### 在游戏逻辑中使用

```csharp
var ui = ModuleSystem.GetModule<IUIModule>();

// 打开背包，传入标题
ui.ShowUI<BagWindow>("我的背包");

// 过一会儿再次打开（不重新加载，只刷新标题）
ui.ShowUI<BagWindow>("装备背包");

// 直接关闭（销毁节点）
ui.CloseUI<BagWindow>();

// 关闭 High 层所有窗口
ui.CloseAll(UILayer.High);
```

---

## 附：类关系速查

```
UIBase（抽象）
  ├── UIWindow（抽象）—— 一个完整 UI 面板
  │       由 UIModule 管理生命周期
  │       必须标记 [Window(layer, assetPath, ...)]
  │
  └── UIWidget（抽象）—— 可复用子组件
          由 UIBase.CreateWidget<T>() 创建
          生命周期随父级 UIWindow 联动
```
