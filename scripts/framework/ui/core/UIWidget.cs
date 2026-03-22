using Godot;

namespace Framework.UI
{
    /// <summary>
    /// 组件级 UI 基类（对标 TEngine UIWidget）。
    /// <para>
    /// UIWidget 是可复用的 UI 组件，通过父级 <see cref="UIBase"/> 的
    /// <see cref="UIBase.CreateWidget{T}(string)"/> 创建并绑定到场景树中的 Control 节点。
    /// </para>
    /// <para>
    /// 设计理念：组合优于继承——通过多个 Widget 的组合构建复杂 UI 界面，
    /// 每个 Widget 职责单一，独立管理自身的生命周期与事件订阅。
    /// </para>
    /// <example>
    /// <code>
    /// public class TabButtonWidget : UIWidget
    /// {
    ///     [UIBind("%")] private Button _btnTab;
    ///     [UIBind("%")] private Label  _textName;
    ///
    ///     private int _tabIndex;
    ///     private System.Action&lt;int&gt; _onClick;
    ///
    ///     public override void BindMemberProperty() { }
    ///
    ///     protected override void OnCreate()
    ///     {
    ///         _btnTab.Pressed += () => _onClick?.Invoke(_tabIndex);
    ///     }
    ///
    ///     public void SetData(string name, int index, System.Action&lt;int&gt; onClick)
    ///     {
    ///         _textName.Text = name;
    ///         _tabIndex      = index;
    ///         _onClick       = onClick;
    ///     }
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public abstract class UIWidget : UIBase
    {
        // ──────────────────────────────────────────────
        //  Widget 属性
        // ──────────────────────────────────────────────

        /// <summary>
        /// 所属的顶层窗口（由框架在创建时赋值）。
        /// </summary>
        public UIWindow OwnerWindow { get; private set; }

        /// <summary>
        /// Widget 可见性（直接控制 <see cref="UIBase.Owner"/> 节点的 Visible 属性）。
        /// </summary>
        public bool Visible
        {
            get => Owner?.Visible ?? false;
            set
            {
                if (Owner != null) Owner.Visible = value;
                OnSetVisible(value);
            }
        }

        // ──────────────────────────────────────────────
        //  框架内部方法
        // ──────────────────────────────────────────────

        /// <summary>由 UIBase.CreateWidgetInternal 调用，设置所属窗口。</summary>
        internal void SetOwnerWindow(UIWindow window)
        {
            OwnerWindow = window;
        }

        internal override void InternalCreate()
        {
            base.InternalCreate();
        }

        internal override void InternalDestroy()
        {
            base.InternalDestroy();
            OwnerWindow = null;
        }

        internal override void InternalSetVisible(bool visible)
        {
            // UIWidget 的可见性由 Visible 属性统一管理，这里调用基类更新子 widgets 及回调
            base.InternalSetVisible(visible);
        }
    }
}
