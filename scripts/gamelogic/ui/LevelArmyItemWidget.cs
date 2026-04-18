using System;
using GameLogic;
using Godot;
using Framework.UI;
using Framework;

namespace GameLogic
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

        private GameObject2D _unit;
        private Action<GameObject2D> _onClick;
        private bool _isPressedBound;

        /// <summary>
        /// 创建条目并绑定点击事件。
        /// </summary>
        protected override void OnCreate()
        {
            if (_btnSelect != null)
            {
                _btnSelect.Pressed += OnPressed;
                _isPressedBound = true;
            }
        }

        /// <summary>
        /// 销毁条目前解除点击事件绑定。
        /// </summary>
        protected override void OnDestroy()
        {
            if (_isPressedBound && _btnSelect != null && GodotObject.IsInstanceValid(_btnSelect))
                _btnSelect.Pressed -= OnPressed;

            _isPressedBound = false;
            _unit = null;
            _onClick = null;
        }

        /// <summary>
        /// 设置条目显示的数据与点击回调。
        /// </summary>
        public void SetData(GameObject2D unit, Action<GameObject2D> onClick)
        {
            var gameObject = unit as SerializableGameObject2D;
            var selectable = gameObject?.GetComponent<SelectionComponent>();
            if (unit == null || gameObject == null || selectable == null)
                return;

            _unit = unit;
            _onClick = onClick;

            if (_labelName != null)
                _labelName.Text = ResolveDisplayName(gameObject);

            if (_labelId != null)
                _labelId.Text = gameObject.Name;
        }

        /// <summary>
        /// 解析条目上显示的单位名称。
        /// </summary>
        private static string ResolveDisplayName(SerializableGameObject2D gameObject)
        {
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
            Debugger.Info($"LevelArmyItemWidget pressed: {_unit?.Name}");
            _onClick?.Invoke(_unit);
        }
    }
}
