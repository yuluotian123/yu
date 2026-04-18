using Framework;
using Framework.UI;
using Godot;

namespace GameLogic
{
    [Window(UILayer.Tips, "res://assets/scenes/ui/context_target_menu_window.tscn", fullScreen: false, hideTimeToClose: 0f)]
    public class ContextTargetMenuWindow : UIWindow
    {
        [UIBind("%")] private PanelContainer _panel;
        [UIBind("%")] private Button _btnMoveTo;

        private SelectionComponent _targetSelection;
        private SelectableManagerComponent _selectableManager;
        private PlayerArmyComponent _playerArmy;
        private Vector2 _screenOffset = new Vector2(0f, -56f);

        protected override void OnCreate()
        {
            if (_btnMoveTo != null)
                _btnMoveTo.Pressed += OnMoveToPressed;

            if (Owner != null)
            {
                Owner.MouseFilter = Control.MouseFilterEnum.Ignore;
                Owner.Visible = false;
            }
        }

        protected override void OnRefresh()
        {
            _targetSelection = UserDatas != null && UserDatas.Length > 0
                ? UserDatas[0] as SelectionComponent
                : null;

            if (!HasValidTarget())
            {
                ModuleSystem.GetModule<IUIModule>().CloseUI(this);
                return;
            }

            if (Owner != null)
                Owner.Visible = true;

            UpdateScreenPosition();
        }

        protected override void OnUpdate(double delta)
        {
            if (!HasValidTarget())
            {
                ModuleSystem.GetModule<IUIModule>().CloseUI(this);
                return;
            }

            UpdateScreenPosition();
        }

        protected override void OnDestroy()
        {
            if (_btnMoveTo != null)
                _btnMoveTo.Pressed -= OnMoveToPressed;

            _targetSelection = null;
            _selectableManager = null;
            _playerArmy = null;
        }

        private void OnMoveToPressed()
        {
            if (!HasValidTarget())
            {
                ModuleSystem.GetModule<IUIModule>().CloseUI(this);
                return;
            }

            ResolveDependencies();
            _playerArmy?.CommandSelectedUnitsFollow(_targetSelection);
            ModuleSystem.GetModule<IUIModule>().CloseUI(this);
        }

        private void UpdateScreenPosition()
        {
            if (Owner == null || _targetSelection?.Owner == null)
                return;

            Viewport viewport = Owner.GetViewport();
            if (viewport == null)
                return;

            Vector2 screenPosition = viewport.GetCanvasTransform() * _targetSelection.Owner.WorldPosition2D;
            Vector2 menuSize = Owner.Size;
            if (menuSize == Vector2.Zero)
                menuSize = Owner.GetCombinedMinimumSize();

            Owner.Position = screenPosition
                + _screenOffset
                - new Vector2(menuSize.X * 0.5f, menuSize.Y);
        }

        private void ResolveDependencies()
        {
            _selectableManager ??= RootModule.Instance.GameState?.PlayerState.GetSelectableManager();
            _playerArmy ??= RootModule.Instance.GameState?.PlayerState.GetArmyComponent();
        }

        private bool HasValidTarget()
        {
            return _targetSelection != null
                && _targetSelection.Owner != null
                && GodotObject.IsInstanceValid(_targetSelection)
                && GodotObject.IsInstanceValid(_targetSelection.Owner)
                && _targetSelection.Owner.IsInsideTree();
        }
    }
}
