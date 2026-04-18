using Framework.UI;
using Godot;

namespace GameLogic
{
    [Window(UILayer.Normal, "res://assets/scenes/ui/level_window.tscn", fullScreen: false)]
    public class LevelWindow : UIWindow
    {
        [UIBind("%")] private Control _topBar;
        [UIBind("%")] private Control _armyBar;

        private LevelTopWidget _topWidget;
        private LevelArmyWidget _armyWidget;

        /// <summary>
        /// 绑定关卡窗口中的顶部栏与底部单位栏。
        /// </summary>
        public override void BindMemberProperty()
        {
            _topWidget = CreateWidget<LevelTopWidget>(_topBar);
            _armyWidget = CreateWidget<LevelArmyWidget>(_armyBar);
        }
    }
}
