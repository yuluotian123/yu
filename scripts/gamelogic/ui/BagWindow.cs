using Framework;
using Framework.UI;
using Godot;

namespace GameLogic.UI
{
    /// <summary>
    /// 背包窗口——演示非全屏弹窗、HideUI 缓存与 UserDatas 传参。
    /// <para>
    /// 场景节点结构：
    /// <code>
    /// BagWindow (Control)
    ///   ├── %BtnClose    (Button)
    ///   └── %LabelTitle  (Label)
    /// </code>
    /// </para>
    /// </summary>
    [Window(UILayer.High, "res://assets/minigame/ui/bag_window.tscn",
            fullScreen: false, hideTimeToClose: 30f)]
    public class BagWindow : UIWindow
    {
        [UIBind("%")] private Button _btnClose;
        [UIBind("%")] private Label  _labelTitle;

        protected override void OnCreate()
        {
            Debugger.Info("[BagWindow] OnCreate");
            _btnClose.Pressed += OnCloseClicked;
        }

        protected override void OnRefresh()
        {
            string title = UserDatas?.Length > 0 ? UserDatas[0] as string : "背包";
            _labelTitle.Text = title;
            Debugger.Info($"[BagWindow] OnRefresh title={title}");
        }

        protected override void OnUpdate(double delta)
        {
            // 演示：每帧可在此做滚动/动画逻辑
        }

        protected override void OnDestroy()
        {
            Debugger.Info("[BagWindow] OnDestroy");
        }

        private void OnCloseClicked()
        {
            // HideUI：节点保留，30秒后自动销毁（由 WindowAttribute.hideTimeToClose 控制）
            ModuleSystem.GetModule<IUIModule>().HideUI<BagWindow>();
        }
    }
}
