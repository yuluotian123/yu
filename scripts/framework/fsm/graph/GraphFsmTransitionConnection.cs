using Godot;

namespace Framework
{
    public class GraphFsmTransitionConnection : global::GraphConnection
    {
        public string TransitionName { get; set; } = string.Empty;
        public GraphFsmTransitionCondition Condition { get; set; } = GraphFsmTransitionCondition.Trigger;
        public string TriggerName { get; set; } = string.Empty;
        public string BoolParameterName { get; set; } = string.Empty;
        public bool ExpectedBoolValue { get; set; } = true;
        public float DelaySeconds { get; set; }
        public int Priority { get; set; }

        public override string GetDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(TransitionName))
                return TransitionName;

            return Condition switch
            {
                GraphFsmTransitionCondition.Always => "Always",
                GraphFsmTransitionCondition.Trigger => string.IsNullOrWhiteSpace(TriggerName) ? "Trigger" : $"Trigger: {TriggerName}",
                GraphFsmTransitionCondition.BoolEquals => string.IsNullOrWhiteSpace(BoolParameterName)
                    ? "Bool"
                    : $"{BoolParameterName} == {ExpectedBoolValue}",
                GraphFsmTransitionCondition.Timer => $"After {DelaySeconds:0.##}s",
                _ => "Transition"
            };
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(320f, 0f) };

            var nameEdit = new LineEdit
            {
                PlaceholderText = "Transition name",
                Text = TransitionName
            };
            nameEdit.TextChanged += value => TransitionName = value;
            root.AddChild(nameEdit);

            var conditionOption = new OptionButton();
            foreach (GraphFsmTransitionCondition condition in System.Enum.GetValues<GraphFsmTransitionCondition>())
                conditionOption.AddItem(condition.ToString(), (int)condition);
            conditionOption.Selected = (int)Condition;
            conditionOption.ItemSelected += index => Condition = (GraphFsmTransitionCondition)index;
            root.AddChild(conditionOption);

            var triggerEdit = new LineEdit
            {
                PlaceholderText = "Trigger name",
                Text = TriggerName
            };
            triggerEdit.TextChanged += value => TriggerName = value;
            root.AddChild(triggerEdit);

            var boolParamEdit = new LineEdit
            {
                PlaceholderText = "Bool parameter name",
                Text = BoolParameterName
            };
            boolParamEdit.TextChanged += value => BoolParameterName = value;
            root.AddChild(boolParamEdit);

            var expectedBoolCheck = new CheckBox
            {
                Text = "Expected bool value",
                ButtonPressed = ExpectedBoolValue
            };
            expectedBoolCheck.Toggled += value => ExpectedBoolValue = value;
            root.AddChild(expectedBoolCheck);

            var delaySpin = new SpinBox
            {
                MinValue = 0,
                MaxValue = 999,
                Step = 0.05,
                Value = DelaySeconds
            };
            delaySpin.ValueChanged += value => DelaySeconds = (float)value;
            root.AddChild(new Label { Text = "Delay seconds" });
            root.AddChild(delaySpin);

            var prioritySpin = new SpinBox
            {
                MinValue = -999,
                MaxValue = 999,
                Step = 1,
                Value = Priority
            };
            prioritySpin.ValueChanged += value => Priority = (int)value;
            root.AddChild(new Label { Text = "Priority" });
            root.AddChild(prioritySpin);

            return root;
        }
    }
}
