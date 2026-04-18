using Framework;
using Framework.UI;
using Godot;

namespace GameLogic
{
    /// <summary>
    /// 主菜单窗口——UI框架测试案例（全屏窗口）。
    /// <para>演示内容：</para>
    /// <list type="bullet">
    ///   <item>[UIBind] 自动绑定节点</item>
    ///   <item>OnCreate / OnRefresh / OnDestroy 生命周期</item>
    ///   <item>AddUIEvent 事件订阅（自动清理）</item>
    ///   <item>CreateWidget 创建子 Widget</item>
    ///   <item>ShowUI / CloseUI / HideUI 操作</item>
    /// </list>
    /// <para>对应场景：res://assets/minigame/ui/main_menu_window.tscn</para>
    /// <para>
    /// 场景结构要求（节点设置为 UniqueNode）：
    /// <code>
    /// MainMenuWindow (Control)
    ///   ├── %BtnStart       (Button)
    ///   ├── %BtnBag         (Button)
    ///   ├── %BtnQuit        (Button)
    ///   ├── %LabelVersion   (Label)
    ///   └── %PanelNotice    (Control)  ← NoticeWidget 绑定节点
    /// </code>
    /// </para>
    /// </summary>
    [Window(UILayer.Normal, "res://assets/scenes/ui/main_menu_window.tscn", fullScreen: true)]
    public class MainMenuWindow : UIWindow
    {
        // ── [UIBind] 自动绑定 ──────────────────────
        [UIBind("%")] private Button _btnStart;
        [UIBind("%")] private Button _btnQuit;
        [UIBind("%")] private Label  _labelVersion;
        [UIBind("%")] private Control _panelNotice;

        // ── 子 Widget ──────────────────────────────
        private NoticeWidget _noticeWidget;

        // ───────────────────────────────────────────
        //  BindMemberProperty：创建子 Widget
        // ───────────────────────────────────────────

        public override void BindMemberProperty()
        {
            // 将 %PanelNotice 节点绑定为 NoticeWidget
            _noticeWidget = CreateWidget<NoticeWidget>(_panelNotice);
        }

        // ───────────────────────────────────────────
        //  RegisterEvent：注册事件（销毁时自动取消订阅）
        // ───────────────────────────────────────────

        public override void RegisterEvent()
        {
            // 监听游戏通知事件（参数：string message），销毁时自动取消订阅
            AddUIEvent<string>(GameUIEvents.GameNotice, OnGameNotice);
        }

        // ───────────────────────────────────────────
        //  OnCreate：节点绑定完成后执行一次
        // ───────────────────────────────────────────

        protected override void OnCreate()
        {
            Debugger.Info("[MainMenuWindow] OnCreate");

            _btnStart.Pressed += OnStartClicked;
            _btnQuit.Pressed  += OnQuitClicked;
        }

        // ───────────────────────────────────────────
        //  OnRefresh：每次 ShowUI 时调用（刷新数据）
        // ───────────────────────────────────────────

        protected override void OnRefresh()
        {
            string version = UserDatas?.Length > 0 ? UserDatas[0] as string : "v1.0.0";
            _labelVersion.Text = $"Version: {version}";
            Debugger.Info($"[MainMenuWindow] OnRefresh, version={version}");
        }

        // ───────────────────────────────────────────
        //  OnDestroy：销毁时
        // ───────────────────────────────────────────

        protected override void OnDestroy()
        {
            Debugger.Info("[MainMenuWindow] OnDestroy");
        }

        // ───────────────────────────────────────────
        //  按钮事件处理
        // ───────────────────────────────────────────

        private void OnStartClicked()
        {
            Debugger.Info("[MainMenuWindow] 开始游戏");
            var ui = ModuleSystem.GetModule<IUIModule>();
            ui.CloseUI<MainMenuWindow>();
            // 进入游戏 Procedure（示例）
            ModuleSystem.GetModule<IEventModule>().Send(GameUIEvents.GameStart);
            
            //ModuleSystem.GetModule<IProcedureModule>();
        }

        private void OnQuitClicked()
        {
            Debugger.Info("[MainMenuWindow] 退出游戏");
            // Godot 退出
            if(Engine.GetMainLoop() is SceneTree tree)
                tree.Quit();
        }

        // ───────────────────────────────────────────
        //  事件回调
        // ───────────────────────────────────────────

        private void OnGameNotice(string message)
        {
            _noticeWidget?.ShowNotice(message);
        }
    }
}
