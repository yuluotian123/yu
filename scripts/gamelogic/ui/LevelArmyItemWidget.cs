using System;
using Godot;
using Framework.UI;
using Framework;

namespace GameLogic.UI
{
    /// <summary>
    /// 单个底部单位栏条目 Widget。
    /// </summary>
    public class LevelArmyItemWidget : UIWidget
    {
        [UIBind("Panel")] private PanelContainer _panel;
        [UIBind("Panel/Content/BtnSelect")] private Button _btnSelect;
        [UIBind("Panel/Content/BtnSelect/VBox/LabelName")] private Label _labelName;
        [UIBind("Panel/Content/BtnSelect/VBox/LabelId")] private Label _labelId;

        private string _unitId = string.Empty;
        private Action<string> _onClick;

        /// <summary>
        /// 创建条目并绑定点击事件。
        /// </summary>
        protected override void OnCreate()
        {
            if (_btnSelect != null)
                _btnSelect.Pressed += OnPressed;
        }

        /// <summary>
        /// 销毁条目前解除点击事件绑定。
        /// </summary>
        protected override void OnDestroy()
        {
            if (_btnSelect != null)
                _btnSelect.Pressed -= OnPressed;
        }

        /// <summary>
        /// 设置条目显示的数据与点击回调。
        /// </summary>
        public void SetData(PlayerUnitSnapshot snapshot, Action<string> onClick)
        {
            if (snapshot == null)
                return;

            _unitId = snapshot.UnitId;
            _onClick = onClick;

            if (_labelName != null)
                _labelName.Text = ResolveDisplayName(snapshot);

            if (_labelId != null)
                _labelId.Text = _unitId.Length > 8 ? _unitId[..8] : _unitId;
        }

        /// <summary>
        /// 解析条目上显示的单位名称。
        /// </summary>
        private static string ResolveDisplayName(PlayerUnitSnapshot snapshot)
        {
            if (!string.IsNullOrEmpty(snapshot.DisplayName))
                return snapshot.DisplayName;

            if (!string.IsNullOrEmpty(snapshot.UnitConfigId))
                return snapshot.UnitConfigId;

            return "Unit";
        }

        /// <summary>
        /// 设置条目的选中高亮表现。
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (_panel == null)
                return;

            _panel.SelfModulate = selected
                ? new Color(0.45f, 0.78f, 1f)
                : new Color(1f, 1f, 1f);
        }

        /// <summary>
        /// 响应按钮点击并通知外部选中对应单位。
        /// </summary>
        private void OnPressed()
        {
            Debugger.Info($"LevelArmyItemWidget pressed: {_unitId}");
            _onClick?.Invoke(_unitId);
        }
    }
}
