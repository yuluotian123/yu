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

    public class HfsmBoolCondition : HfsmConditionBase
    {
        private static readonly Type[] BoolValueTypes = { typeof(bool) };

        public GraphBlackboardKeyReference Parameter { get; set; } = new();
        public string ParameterName { get; set; } = string.Empty;
        public bool ExpectedValue { get; set; } = true;

        public override string Description => string.IsNullOrWhiteSpace(GetParameterKey())
            ? "Bool"
            : $"{GetParameterKey()} == {ExpectedValue}";

        public override bool IsMet(HfsmRuntime runtime)
        {
            string key = GetParameterKey();
            return runtime != null &&
                   !string.IsNullOrWhiteSpace(key) &&
                   runtime.Blackboard.GetValue(key, false) == ExpectedValue;
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();

            Parameter ??= new GraphBlackboardKeyReference { Key = ParameterName };
            if (string.IsNullOrWhiteSpace(Parameter.Key) && !string.IsNullOrWhiteSpace(ParameterName))
                Parameter.Key = ParameterName;

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

        private string GetParameterKey()
        {
            if (!string.IsNullOrWhiteSpace(Parameter?.Key))
                return Parameter.Key;

            return ParameterName;
        }
    }

    public class HfsmFloatCondition : HfsmConditionBase
    {
        private static readonly Type[] FloatValueTypes = { typeof(float), typeof(int) };

        public GraphBlackboardKeyReference Parameter { get; set; } = new();
        public string ParameterName { get; set; } = string.Empty;
        public HfsmFloatComparison Comparison { get; set; } = HfsmFloatComparison.GreaterOrEqual;
        public float Value { get; set; }
        public float Tolerance { get; set; } = 0.0001f;

        public override string Description => string.IsNullOrWhiteSpace(GetParameterKey())
            ? "Float"
            : $"{GetParameterKey()} {GetComparisonText()} {Value:0.###}";

        public override bool IsMet(HfsmRuntime runtime)
        {
            string key = GetParameterKey();
            if (runtime == null || string.IsNullOrWhiteSpace(key))
                return false;

            float actual = runtime.Blackboard.GetValue(key, 0f);
            return Comparison switch
            {
                HfsmFloatComparison.Less => actual < Value,
                HfsmFloatComparison.LessOrEqual => actual <= Value,
                HfsmFloatComparison.Equal => Mathf.Abs(actual - Value) <= Tolerance,
                HfsmFloatComparison.GreaterOrEqual => actual >= Value,
                HfsmFloatComparison.Greater => actual > Value,
                _ => false
            };
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();

            Parameter ??= new GraphBlackboardKeyReference { Key = ParameterName };
            if (string.IsNullOrWhiteSpace(Parameter.Key) && !string.IsNullOrWhiteSpace(ParameterName))
                Parameter.Key = ParameterName;

            root.AddChild(Parameter.CreateEditUI(context, "Blackboard number", FloatValueTypes));

            var row = new HBoxContainer();
            var option = new OptionButton();
            foreach (HfsmFloatComparison comparison in System.Enum.GetValues<HfsmFloatComparison>())
                option.AddItem(comparison.ToString(), (int)comparison);
            option.Selected = (int)Comparison;
            option.ItemSelected += index => Comparison = (HfsmFloatComparison)index;
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
            return Comparison switch
            {
                HfsmFloatComparison.Less => "<",
                HfsmFloatComparison.LessOrEqual => "<=",
                HfsmFloatComparison.Equal => "==",
                HfsmFloatComparison.GreaterOrEqual => ">=",
                HfsmFloatComparison.Greater => ">",
                _ => "?"
            };
        }

        private string GetParameterKey()
        {
            if (!string.IsNullOrWhiteSpace(Parameter?.Key))
                return Parameter.Key;

            return ParameterName;
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
