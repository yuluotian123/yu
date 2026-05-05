using Godot;
using System;

namespace GameLogic
{
    public class HfsmAlwaysCondition : HfsmConditionBase
    {
        public override string Description => "Always";

        public override bool IsMet(HfsmRuntime runtime) => true;
    }

    public class HfsmTriggerCondition : HfsmConditionBase
    {
        public string TriggerName { get; set; } = string.Empty;

        public override string Description => string.IsNullOrWhiteSpace(TriggerName)
            ? "Trigger"
            : $"Trigger: {TriggerName}";

        public override bool IsMet(HfsmRuntime runtime)
        {
            return runtime != null &&
                   !string.IsNullOrWhiteSpace(TriggerName) &&
                   runtime.HasTrigger(TriggerName);
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var edit = new LineEdit
            {
                PlaceholderText = "Trigger name",
                Text = TriggerName
            };
            edit.TextChanged += value => TriggerName = value;
            return edit;
        }
    }

    public class HfsmBoolCondition : HfsmBlackboardConditionBase
    {
        private static readonly Type[] BoolValueTypes = { typeof(bool) };

        public bool ExpectedValue { get; set; } = true;

        public override string Description => string.IsNullOrWhiteSpace(ParameterKey)
            ? "Bool"
            : $"{ParameterKey} == {ExpectedValue}";

        public override bool IsMet(HfsmRuntime runtime)
        {
            string key = ParameterKey;
            return runtime != null &&
                   !string.IsNullOrWhiteSpace(key) &&
                   runtime.Blackboard.GetValue(key, false) == ExpectedValue;
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();

            SyncLegacyParameterName();
            root.AddChild(Parameter.CreateEditUI(context, "Blackboard bool", BoolValueTypes));

            var check = new CheckBox
            {
                Text = "Expected value",
                ButtonPressed = ExpectedValue
            };
            check.Toggled += value => ExpectedValue = value;
            root.AddChild(check);

            return root;
        }
    }

    public class HfsmFloatCondition : HfsmBlackboardConditionBase
    {
        private static readonly Type[] FloatValueTypes = { typeof(float), typeof(int) };

        public StateFloatComparison Comparison { get; set; } = StateFloatComparison.GreaterOrEqual;
        public float Value { get; set; }
        public float Tolerance { get; set; } = 0.0001f;

        public override string Description => string.IsNullOrWhiteSpace(ParameterKey)
            ? "Float"
            : $"{ParameterKey} {GetComparisonText()} {Value:0.###}";

        public override bool IsMet(HfsmRuntime runtime)
        {
            string key = ParameterKey;
            if (runtime == null || string.IsNullOrWhiteSpace(key))
                return false;

            float actual = runtime.Blackboard.GetValue(key, 0f);
            return StateFloatComparisonUtility.Evaluate(Comparison, actual, Value, Tolerance);
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();

            SyncLegacyParameterName();
            root.AddChild(Parameter.CreateEditUI(context, "Blackboard number", FloatValueTypes));

            var row = new HBoxContainer();
            var option = new OptionButton();
            foreach (StateFloatComparison comparison in System.Enum.GetValues<StateFloatComparison>())
                option.AddItem(comparison.ToString(), (int)comparison);
            option.Selected = (int)Comparison;
            option.ItemSelected += index => Comparison = (StateFloatComparison)index;
            row.AddChild(option);

            var valueSpin = new SpinBox
            {
                MinValue = -999999,
                MaxValue = 999999,
                Step = 0.01,
                Value = Value,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            valueSpin.ValueChanged += value => Value = (float)value;
            row.AddChild(valueSpin);

            root.AddChild(row);
            return root;
        }

        private string GetComparisonText()
        {
            return StateFloatComparisonUtility.ToOperatorText(Comparison);
        }
    }

    public class HfsmFloatAbsCondition : HfsmBlackboardConditionBase
    {
        private static readonly Type[] FloatValueTypes = { typeof(float), typeof(int) };

        public StateFloatComparison Comparison { get; set; } = StateFloatComparison.Greater;
        public float Value { get; set; }
        public float Tolerance { get; set; } = 0.0001f;

        public override string Description => string.IsNullOrWhiteSpace(ParameterKey)
            ? "Abs Float"
            : $"Abs({ParameterKey}) {GetComparisonText()} {Value:0.###}";

        public override bool IsMet(HfsmRuntime runtime)
        {
            string key = ParameterKey;
            if (runtime == null || string.IsNullOrWhiteSpace(key))
                return false;

            float actual = Mathf.Abs(runtime.Blackboard.GetValue(key, 0f));
            return StateFloatComparisonUtility.Evaluate(Comparison, actual, Value, Tolerance);
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();

            SyncLegacyParameterName();
            root.AddChild(Parameter.CreateEditUI(context, "Blackboard number", FloatValueTypes));

            var row = new HBoxContainer();
            var option = new OptionButton();
            foreach (StateFloatComparison comparison in System.Enum.GetValues<StateFloatComparison>())
                option.AddItem(comparison.ToString(), (int)comparison);
            option.Selected = (int)Comparison;
            option.ItemSelected += index => Comparison = (StateFloatComparison)index;
            row.AddChild(option);

            var valueSpin = new SpinBox
            {
                MinValue = 0,
                MaxValue = 999999,
                Step = 0.01,
                Value = Value,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            valueSpin.ValueChanged += value => Value = (float)value;
            row.AddChild(valueSpin);

            root.AddChild(row);
            return root;
        }

        private string GetComparisonText()
        {
            return StateFloatComparisonUtility.ToOperatorText(Comparison);
        }
    }

    public class HfsmTimerCondition : HfsmConditionBase
    {
        public float Seconds { get; set; } = 1f;

        public override string Description => $"After {Seconds:0.##}s";

        public override bool IsMet(HfsmRuntime runtime)
        {
            return runtime != null && runtime.CurrentStateTime >= Seconds;
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var spin = new SpinBox
            {
                MinValue = 0,
                MaxValue = 999999,
                Step = 0.05,
                Value = Seconds
            };
            spin.ValueChanged += value => Seconds = (float)value;
            return spin;
        }
    }
}
