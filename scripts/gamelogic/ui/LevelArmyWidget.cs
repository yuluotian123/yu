using System.Collections.Generic;
using Framework;
using Framework.UI;
using Godot;

namespace GameLogic.UI
{
    /// <summary>
    /// Bottom army widget that mirrors the current controllable roster.
    /// </summary>
    public class LevelArmyWidget : UIWidget
    {
        [UIBind("%")] private HBoxContainer _itemContainer;

        private readonly List<LevelArmyItemWidget> _itemWidgets = new();
        private PlayerArmyComponent _armyComponent;
        private SelectableManagerComponent _selectableManager;

        protected override void OnCreate()
        {
            RefreshArmyList();
        }

        protected override void OnRefresh()
        {
            RefreshArmyList();
        }

        protected override void OnDestroy()
        {
            _itemWidgets.Clear();
        }

        public override void RegisterEvent()
        {
            AddUIEvent(GameRtsEvents.ArmyRosterChanged, OnArmyRosterChanged);
            AddUIEvent(GameRtsEvents.ArmySelectionChanged, OnArmySelectionChanged);
        }

        private void OnArmyRosterChanged()
        {
            RefreshArmyList();
        }

        private void OnArmySelectionChanged()
        {
            RefreshArmyList();
        }

        private void RefreshArmyList()
        {
            ClearItemWidgets();

            _armyComponent = ResolveArmyComponent();
            _selectableManager = ResolveSelectableManager();
            if (_armyComponent == null)
                return;

            for (int i = 0; i < _armyComponent.Units.Count; i++)
            {
                var unit = _armyComponent.Units[i];
                var selectable = unit?.GetComponent<SelectionComponent>();
                if (unit == null || selectable == null)
                    continue;

                var itemRoot = CreateItemNode();
                _itemContainer.AddChild(itemRoot);

                var itemWidget = CreateWidget<LevelArmyItemWidget>(itemRoot);
                _itemWidgets.Add(itemWidget);
                itemWidget.SetData(unit, OnItemClicked);
                itemWidget.SetSelected(ReferenceEquals(unit, _selectableManager?.SelectedUnit));
            }
        }

        private void ClearItemWidgets()
        {
            for (int i = _itemWidgets.Count - 1; i >= 0; i--)
                DestroyWidget(_itemWidgets[i]);

            _itemWidgets.Clear();

            if (_itemContainer == null)
                return;

            for (int i = _itemContainer.GetChildCount() - 1; i >= 0; i--)
                _itemContainer.GetChild(i).QueueFree();
        }

        private void OnItemClicked(GameObject2D unit)
        {
            if (unit == null)
                return;

            _selectableManager = ResolveSelectableManager();
            _selectableManager?.Select(unit.GetComponent<SelectionComponent>());
        }

        private PlayerArmyComponent ResolveArmyComponent()
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree == null)
                return null;

            var levelRoot = tree.Root.GetNodeOrNull<Node>("Root/Spacelevel");
            if (levelRoot == null)
                return null;

            var levelArmy = ResolveArmyComponent(levelRoot);
            if (levelArmy != null)
                return levelArmy;

            foreach (Node candidate in levelRoot.FindChildren("*", "", true, false))
            {
                var army = ResolveArmyComponent(candidate);
                if (army != null)
                    return army;
            }

            return null;
        }

        private static PlayerArmyComponent ResolveArmyComponent(Node node)
        {
            if (node is GameObject2D gameObject2D)
                return gameObject2D.GetComponent<PlayerArmyComponent>();

            return null;
        }

        private SelectableManagerComponent ResolveSelectableManager()
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree == null)
                return null;

            var levelRoot = tree.Root.GetNodeOrNull<Node>("Root/Spacelevel");
            if (levelRoot == null)
                return null;

            var manager = ResolveSelectableManager(levelRoot);
            if (manager != null)
                return manager;

            foreach (Node candidate in levelRoot.FindChildren("*", "", true, false))
            {
                manager = ResolveSelectableManager(candidate);
                if (manager != null)
                    return manager;
            }

            return null;
        }

        private static SelectableManagerComponent ResolveSelectableManager(Node node)
        {
            if (node is GameObject2D gameObject2D)
                return gameObject2D.GetComponent<SelectableManagerComponent>();

            return null;
        }

        private static Control CreateItemNode()
        {
            var root = new MarginContainer
            {
                CustomMinimumSize = new Vector2(160f, 72f),
                MouseFilter = Control.MouseFilterEnum.Stop
            };

            var panel = new PanelContainer
            {
                Name = "Panel",
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            root.AddChild(panel);

            var margin = new MarginContainer
            {
                Name = "Content",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            margin.AddThemeConstantOverride("margin_left", 12);
            margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_top", 8);
            margin.AddThemeConstantOverride("margin_bottom", 8);
            panel.AddChild(margin);

            var button = new Button
            {
                Name = "BtnSelect",
                MouseFilter = Control.MouseFilterEnum.Stop,
                FocusMode = Control.FocusModeEnum.None
            };
            margin.AddChild(button);

            var vbox = new VBoxContainer
            {
                Name = "VBox",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            button.AddChild(vbox);

            var nameLabel = new Label
            {
                Name = "LabelName",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Text = "Unit"
            };
            vbox.AddChild(nameLabel);

            var idLabel = new Label
            {
                Name = "LabelId",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Modulate = new Color(0.75f, 0.75f, 0.75f),
                Text = "id"
            };
            vbox.AddChild(idLabel);

            return root;
        }
    }
}
