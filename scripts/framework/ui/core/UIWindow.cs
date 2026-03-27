using Godot;

namespace Framework.UI
{
    /// <summary>
    /// 窗口级 UI 基类（对标 TEngine UIWindow）。
    /// <para>
    /// UIWindow 代表一个完整的 UI 面板（全屏/弹窗），由 <see cref="UIModule"/> 统一管理其
    /// 加载、显示、隐藏、销毁及层级排序。
    /// </para>
    /// <para>
    /// 子类必须用 <see cref="WindowAttribute"/> 标记层级和资源路径。
    /// </para>
    /// <remarks>
    /// Godot vs Unity 差异说明：
    /// Unity 的 <c>SetActive(false)</c> 同时关闭逻辑激活和渲染；
    /// Godot 的 <c>Control.Visible = false</c> 只控制渲染，节点仍在树中正常运行。
    /// 因此本框架不需要维护独立的逻辑状态字段：
    /// <list type="bullet">
    ///   <item><see cref="Active"/> = 业务层的激活意图（ShowUI/HideUI 控制）</item>
    ///   <item><c>Owner.Visible</c> = Godot 节点渲染状态，由框架综合 Active + 全屏遮挡后写入</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Window(UILayer.Normal, "res://assets/ui/main_menu.tscn", fullScreen: true)]
    /// public class MainMenuWindow : UIWindow
    /// {
    ///     [UIBind("%")] private Button _btnStart;
    ///     [UIBind("%")] private Label  _titleLabel;
    ///
    ///     protected override void OnCreate()
    ///     {
    ///         _btnStart.Pressed += OnStartClicked;
    ///         AddUIEvent&lt;string&gt;(Events.GameNotice, OnGameNotice);
    ///     }
    ///
    ///     protected override void OnRefresh()
    ///     {
    ///         _titleLabel.Text = UserDatas?[0] as string ?? "Main Menu";
    ///     }
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public abstract class UIWindow : UIBase
    {
        // ──────────────────────────────────────────────
        //  窗口属性（由 UIModule 在构造/加载时填充）
        // ──────────────────────────────────────────────

        /// <summary>窗口唯一名称（类型全名，由 UIModule 填充）。</summary>
        public string WindowName { get; internal set; }

        /// <summary>UI 层级。</summary>
        public UILayer Layer { get; internal set; }

        /// <summary>PackedScene 资源路径。</summary>
        public string AssetPath { get; internal set; }

        /// <summary>
        /// 是否为全屏窗口。
        /// 全屏窗口打开时，同层中深度更低的所有窗口的 <c>Owner.Visible</c> 将被置为 false。
        /// </summary>
        public bool FullScreen { get; internal set; }

        /// <summary>隐藏后自动关闭的等待秒数（≤0 表示立即关闭）。</summary>
        public float HideTimeToClose { get; internal set; }

        /// <summary>
        /// 业务层激活状态（对齐 TEngine Active）。
        /// <para>
        /// ShowUI → <c>true</c>，HideUI → <c>false</c>。
        /// <see cref="UIModule.RecalcDepthAndVisibility"/> 只在 <c>Active == true</c> 时
        /// 才会恢复 <c>Owner.Visible</c>，确保全屏遮挡不覆盖业务层的隐藏意图。
        /// </para>
        /// </summary>
        public bool Active { get; private set; }

        /// <summary>
        /// 当前 Godot 节点是否实际可见（Active 且未被全屏遮挡）。
        /// </summary>
        public bool IsVisible => Owner != null && GodotObject.IsInstanceValid(Owner) && Owner.Visible;

        /// <summary>异步加载是否已完成。</summary>
        public bool IsLoadDone { get; internal set; }

        /// <summary>
        /// 资源句柄引用（由 UIModule 在加载时赋值，关闭时 Release）。
        /// 用于在窗口关闭时正确释放 PackedScene 的框架引用计数。
        /// </summary>
        internal ResourceHandleBase ResourceHandle { get; set; }

        /// <summary>
        /// 渲染深度（ZIndex）。值越大渲染越靠前。
        /// 由 UIModule 在每次 Push/Pop 时重新计算并写入 Owner.ZIndex。
        /// </summary>
        public int Depth { get; internal set; }

        // ──────────────────────────────────────────────
        //  隐藏计时器（内部使用）
        // ──────────────────────────────────────────────

        private SceneTreeTimer _hideTimer;

        // ──────────────────────────────────────────────
        //  框架内部方法
        // ──────────────────────────────────────────────

        /// <summary>
        /// 初始化窗口（资源加载完成后由 UIModule 调用）。
        /// 依次执行：AutoBind → BindMemberProperty → RegisterEvent → OnCreate。
        /// </summary>
        internal override void InternalCreate()
        {
            if (Owner != null)
                Owner.ZIndex = Depth;

            base.InternalCreate();   // AutoBind → BindMemberProperty → RegisterEvent → OnCreate → IsPrepare=true
        }

        /// <summary>刷新窗口数据（每次 ShowUI 时由 UIModule 调用）。</summary>
        internal override void InternalRefresh()
        {
            base.InternalRefresh();
        }

        /// <summary>每帧更新（由 UIModule.Process 驱动，仅 IsPrepare 且 IsVisible 时调用）。</summary>
        internal override void InternalUpdate(double delta)
        {
            if (!IsPrepare || !IsVisible) return;
            base.InternalUpdate(delta);
        }

        /// <summary>销毁窗口（由 UIModule.CloseUI 调用）。</summary>
        internal override void InternalDestroy()
        {
            CancelHideTimer();

            if (Owner != null && GodotObject.IsInstanceValid(Owner))
                Owner.QueueFree();

            base.InternalDestroy();
            Active = false;
            IsLoadDone = false;
        }

        /// <summary>
        /// 设置业务层激活状态（ShowUI/HideUI 时由 UIModule 调用）。
        /// <para>同时同步 Godot 节点 <c>Owner.Visible</c>、触发 <see cref="OnSetVisible"/> 回调并传播至子 Widget。</para>
        /// </summary>
        internal void SetActive(bool active)
        {
            if (Active == active) return;
            Active = active;

            // 传播到 Godot 节点 + OnSetVisible + 子 Widget
            base.InternalSetVisible(active);
        }

        /// <summary>
        /// 仅控制 Godot 节点渲染可见性，不改变 <see cref="Active"/> 业务状态，
        /// 也不触发 <see cref="OnSetVisible"/> 回调。
        /// <para>
        /// 供 <see cref="UIModule"/> 的全屏遮挡专用：
        /// 全屏窗口打开时遮挡下层窗口，关闭时恢复——但只有 <c>Active == true</c> 的窗口才会被恢复。
        /// </para>
        /// </summary>
        internal void SetNodeVisible(bool visible)
        {
            if (Owner != null && GodotObject.IsInstanceValid(Owner))
                Owner.Visible = visible;
        }

        /// <summary>
        /// 启动隐藏计时器（HideTimeToClose > 0 时，超时后自动调用 CloseUI）。
        /// 由 UIModule.HideUI 调用。
        /// </summary>
        internal void StartHideTimer(SceneTree tree)
        {
            CancelHideTimer();
            if (HideTimeToClose <= 0f) return;

            _hideTimer = tree.CreateTimer(HideTimeToClose);
            _hideTimer.Timeout += OnHideTimerTimeout;
        }

        private void CancelHideTimer()
        {
            if (_hideTimer == null) return;
            _hideTimer.Timeout -= OnHideTimerTimeout;
            _hideTimer = null;
        }

        private void OnHideTimerTimeout()
        {
            _hideTimer = null;
            UIModule.Instance?.CloseWindowByName(WindowName);
        }

        // ──────────────────────────────────────────────
        //  更新深度到 Godot 节点
        // ──────────────────────────────────────────────

        internal void ApplyDepth()
        {
            if (Owner != null && GodotObject.IsInstanceValid(Owner))
                Owner.ZIndex = Depth;
        }
    }
}
