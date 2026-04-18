using Godot;

namespace GameLogic
{
    public class LevelTopWidget : Framework.UI.UIWidget
    {
        [Framework.UI.UIBind("%")] private Button _btnPause;
        [Framework.UI.UIBind("%")] private Button _btnStart;
        [Framework.UI.UIBind("%")] private Button _btnSpeedX2;

        protected override void OnCreate()
        {
            _btnPause.Pressed += OnPauseClicked;
            _btnStart.Pressed += OnStartClicked;
            _btnSpeedX2.Pressed += OnSpeedX2Clicked;
        }

        private static void OnPauseClicked()
        {
            RootModule.Instance?.PauseGame();
        }

        private static void OnStartClicked()
        {
            if (RootModule.Instance != null)
            {
                RootModule.Instance.ResumeGame();
                RootModule.Instance.GameSpeed = 1f;
            }
        }

        private static void OnSpeedX2Clicked()
        {
            if (RootModule.Instance != null)
            {
                RootModule.Instance.ResumeGame();
                RootModule.Instance.GameSpeed = 2f;
            }
        }
    }
}
