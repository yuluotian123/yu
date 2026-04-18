using Framework;
using Framework.UI;
using Godot;

namespace GameLogic
{
    /// <summary>
    /// 通知提示 Widget——演示 UIWidget 的基本用法。
    /// <para>
    /// 场景节点结构（%PanelNotice 子节点）：
    /// <code>
    /// PanelNotice (Control)
    ///   ├── %LabelMessage  (Label)
    ///   └── %BtnClose      (Button)
    /// </code>
    /// </para>
    /// </summary>
    public class NoticeWidget : UIWidget
    {
        [UIBind("%")] private Label  _labelMessage;
        [UIBind("%")] private Button _btnClose;

        protected override void OnCreate()
        {
            Debugger.Info("[NoticeWidget] OnCreate");
            _btnClose.Pressed += () => Visible = false;
            Visible = false; // 默认隐藏
        }

        /// <summary>显示一条通知消息。</summary>
        public void ShowNotice(string message)
        {
            _labelMessage.Text = message;
            Visible = true;
        }

        protected override void OnDestroy()
        {
            Debugger.Info("[NoticeWidget] OnDestroy");
        }
    }
}
