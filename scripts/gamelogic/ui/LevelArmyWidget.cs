using Godot;
using Framework.UI;
using Framework;

namespace GameLogic.UI
{
    /// <summary>
    /// 关卡底部单位栏 Widget，负责展示玩家当前队列。
    /// </summary>
    public class LevelArmyWidget : UIWidget
    {
        [UIBind("%")] private HBoxContainer _itemContainer;

        private PlayerArmyComponent _armyComponent;

        /// <summary>
        /// 创建底部单位栏并完成首次刷新。
        /// </summary>
        protected override void OnCreate()
        {
            RefreshArmyList();
        }

        /// <summary>
        /// 窗口刷新时同步底部单位栏内容。
        /// </summary>
        protected override void OnRefresh()
        {
            RefreshArmyList();
        }

        /// <summary>
        /// 注册单位列表与选中状态变化事件。
        /// </summary>
        public override void RegisterEvent()
        {
            AddUIEvent(GameRtsEvents.ArmyRosterChanged, OnArmyRosterChanged);
            AddUIEvent(GameRtsEvents.ArmySelectionChanged, OnArmySelectionChanged);
        }

        /// <summary>
        /// 当玩家单位列表变化时刷新底部列表。
        /// </summary>
        private void OnArmyRosterChanged()
        {
            RefreshArmyList();
        }

        /// <summary>
        /// 当选中单位变化时刷新底部高亮。
        /// </summary>
        private void OnArmySelectionChanged()
        {
            RefreshArmyList();
        }

        /// <summary>
        /// 根据当前玩家状态重建底部单位按钮列表。
        /// </summary>
        private void RefreshArmyList()
        {
            if (_itemContainer == null)
                return;

            for (int i = _itemContainer.GetChildCount() - 1; i >= 0; i--)
                _itemContainer.GetChild(i).QueueFree();

            var playerState = ResolvePlayerState();
            if (playerState == null)
                return;

            for (int i = 0; i < playerState.OwnedUnits.Count; i++)
            {
                var snapshot = playerState.OwnedUnits[i];
                if (snapshot == null)
                    continue;

                var itemRoot = CreateItemNode();
                _itemContainer.AddChild(itemRoot);

                var itemWidget = CreateWidget<LevelArmyItemWidget>(itemRoot);
                itemWidget.SetData(snapshot, OnItemClicked);
                itemWidget.SetSelected(snapshot.UnitId == playerState.SelectedUnitId);
            }
        }

        /// <summary>
        /// 处理底部单位按钮点击事件。
        /// </summary>
        private void OnItemClicked(string unitId)
        {
            _armyComponent = ResolveArmyComponent();
            _armyComponent?.SelectUnitById(unitId, focusCamera: true);
        }

        /// <summary>
        /// 获取当前场景中的玩家军队组件。
        /// </summary>
        private PlayerArmyComponent ResolveArmyComponent()
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree == null)
                return null;

            var levelRoot = tree.Root.GetNodeOrNull<Node>("Root/Spacelevel");
            if (levelRoot == null)
                return null;

            var playerController = levelRoot.GetNodeOrNull<PlayerController>("PlayerController");
            Debugger.Info($"LevelArmyWidget resolved PlayerController: {playerController}");
            return playerController?.GetComponent<PlayerArmyComponent>();
        }

        /// <summary>
        /// 获取当前玩家状态，用作底部单位栏的显示数据源。
        /// </summary>
        private static PlayerState ResolvePlayerState()
        {
            return RootModule.Instance?.GameState?._PlayerState;
        }

        /// <summary>
        /// 动态创建一个单位栏条目根节点。
        /// </summary>
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
