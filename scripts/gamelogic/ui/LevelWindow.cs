using Framework.UI;
using Godot;

namespace GameLogic.UI
{
    [Window(UILayer.Normal, "res://assets/scenes/ui/level_window.tscn", fullScreen: false)]
    public class LevelWindow : UIWindow
    {
        [UIBind("%")] private Control _topBar;

        private LevelTopWidget _topWidget;

        public override void BindMemberProperty()
        {
            _topWidget = CreateWidget<LevelTopWidget>(_topBar);
        }
    }
}
