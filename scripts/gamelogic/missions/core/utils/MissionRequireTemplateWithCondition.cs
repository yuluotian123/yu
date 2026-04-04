
using System.Collections.Generic;
using System.Text.Json.Serialization;
using GameLogic.Mission;
using Godot;

public class MissionRequireTemplateWithCondition : MissionRequireTemplate
{
     [JsonInclude] private string eventType;
    [JsonInclude] private int count;
    [JsonInclude] private bool useMessage;

    //条件相关内容
    [JsonInclude] private bool hasCondition;
    [JsonInclude] private ConditionUseMode _useMode;
    [JsonInclude] private readonly List<ConditionBase> _conditions = new List<ConditionBase>();

    public override bool CheckMessage(object message)
    {
        if (message is not GameMessage gameMessage) return false;

        if (!hasCondition)
            return gameMessage.type.ToString() == eventType;
        else
        {
            if(gameMessage.type.ToString() != eventType) return false;

            switch (_useMode)
            {
                case ConditionUseMode.And:
                    foreach (var condition in _conditions)
                        if (!condition.IsConditionMet)
                            return false;

                    return true;

                case ConditionUseMode.Or:
                    foreach (var condition in _conditions)
                        if (condition.IsConditionMet)
                            return true;

                    return false;
            }

            return true;
        }
    }

    public class Handle : MissionRequireTemplateHandle
    {
        private readonly MissionRequireTemplateWithCondition require;
        private int count;

        public Handle(MissionRequireTemplateWithCondition require) : base(require)
        {
            this.require = require;
        }
        protected override bool UseMessage(object message)
        {
            var g = (GameMessage)message;

            if (require.useMessage)
            {
                if (g.hasUsed) return false;
                else
                {
                    g.Use();
                    return ++count == require.count;
                }
            }
            else
                return ++count == require.count;
        }
    }

    public override Control CreateEditUI()
{
    var root = new VBoxContainer();
    root.CustomMinimumSize = new Vector2(380, 0);

    // 基础字段
    var eventTypeRow = new HBoxContainer();
    eventTypeRow.AddChild(new Label { Text = "事件类型：" });
    var eventTypeInput = new LineEdit { Text = eventType, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
    eventTypeInput.TextChanged += (newText) => eventType = newText;
    eventTypeRow.AddChild(eventTypeInput);
    root.AddChild(eventTypeRow);

    var countRow = new HBoxContainer();
    countRow.AddChild(new Label { Text = "计数：" });
    var countInput = new SpinBox { Value = count, MinValue = 1, MaxValue = 999 };
    countInput.ValueChanged += (value) => count = (int)value;
    countRow.AddChild(countInput);
    root.AddChild(countRow);

    var useMessageCheck = new CheckBox { Text = "使用消息", ButtonPressed = useMessage };
    useMessageCheck.Toggled += (pressed) => useMessage = pressed;
    root.AddChild(useMessageCheck);

    root.AddChild(new HSeparator());

    var hasConditionCheck = new CheckBox { Text = "启用条件", ButtonPressed = hasCondition };
    hasConditionCheck.Toggled += (pressed) => hasCondition = pressed;
    root.AddChild(hasConditionCheck);

    var useModeRow = new HBoxContainer();
    useModeRow.AddChild(new Label { Text = "条件组合：" });
    var useModeOption = new OptionButton();
    useModeOption.AddItem("全部满足 (And)", (int)ConditionUseMode.And);
    useModeOption.AddItem("任意满足 (Or)", (int)ConditionUseMode.Or);
    useModeOption.Selected = (int)_useMode;
    useModeOption.ItemSelected += (idx) => _useMode = (ConditionUseMode)(int)idx;
    useModeRow.AddChild(useModeOption);
    root.AddChild(useModeRow);

    root.AddChild(new HSeparator());
    root.AddChild(new Label { Text = "条件列表：" });

    // 可排序条件列表
    var listControl = new ReorderableListControl<ConditionBase>(
        items: _conditions,
        buildItemUi: condition => condition.CreateEditUI(),
        getItemLabel: condition => condition.GetType().Name,
        availableTypes: SubTypeCache.GetSubTypes<ConditionBase>(),
        factory: type => (ConditionBase)System.Activator.CreateInstance(type)
    );

    root.AddChild(listControl.Build());

    return root;
}


}