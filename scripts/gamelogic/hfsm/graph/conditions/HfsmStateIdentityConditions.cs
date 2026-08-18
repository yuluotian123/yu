using System;
using System.Collections.Generic;
using Godot;

namespace GameLogic
{
    public enum HfsmStringComparison
    {
        Equal,
        NotEqual
    }

    public class HfsmStringCondition : HfsmBlackboardConditionBase
    {
        public string Value { get; set; } = string.Empty;
        public HfsmStringComparison Comparison { get; set; }

        public override string Description => $"{ParameterKey} {(Comparison == HfsmStringComparison.Equal ? "==" : "!=")} {Value}";

        public override bool IsMet(HfsmRuntime runtime)
        {
            SyncLegacyParameterName();
            if (runtime == null || string.IsNullOrWhiteSpace(ParameterKey))
                return false;

            bool equal = string.Equals(
                runtime.GetValue(ParameterKey, string.Empty),
                Value ?? string.Empty,
                StringComparison.Ordinal);
            return Comparison == HfsmStringComparison.Equal ? equal : !equal;
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            SyncLegacyParameterName();
            var root = new VBoxContainer();

            var key = new LineEdit { Text = ParameterKey, PlaceholderText = "Blackboard key" };
            key.TextChanged += changed =>
            {
                Parameter ??= new GraphBlackboardKeyReference();
                Parameter.Key = changed.Trim();
                ParameterName = Parameter.Key;
            };
            root.AddChild(key);

            var comparison = new OptionButton();
            foreach (string name in Enum.GetNames<HfsmStringComparison>())
                comparison.AddItem(name);
            comparison.Select((int)Comparison);
            comparison.ItemSelected += index => Comparison = (HfsmStringComparison)index;
            root.AddChild(comparison);

            var value = new LineEdit { Text = Value, PlaceholderText = "Expected value" };
            value.TextChanged += changed => Value = changed;
            root.AddChild(value);
            return root;
        }
    }

    public class HfsmCurrentStateCondition : HfsmConditionBase
    {
        private string _stateNamesOrIds = string.Empty;
        private HashSet<string> _states;

        public string StateNamesOrIds
        {
            get => _stateNamesOrIds;
            set
            {
                _stateNamesOrIds = value ?? string.Empty;
                _states = null;
            }
        }
        public bool Negate { get; set; }

        public override string Description => $"Current state {(Negate ? "not in" : "in")} [{StateNamesOrIds}]";

        public override bool IsMet(HfsmRuntime runtime)
        {
            if (runtime == null)
                return false;

            HashSet<string> states = GetStates();
            bool match = states.Contains(runtime.CurrentStateName) || states.Contains(runtime.CurrentStateId);
            return Negate ? !match : match;
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            var states = new LineEdit
            {
                Text = StateNamesOrIds,
                PlaceholderText = "State names or ids, comma separated"
            };
            states.TextChanged += changed => StateNamesOrIds = changed;
            root.AddChild(states);

            var negate = new CheckBox { Text = "Negate", ButtonPressed = Negate };
            negate.Toggled += changed => Negate = changed;
            root.AddChild(negate);
            return root;
        }

        private HashSet<string> GetStates()
        {
            if (_states != null)
                return _states;

            _states = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(StateNamesOrIds))
                return _states;

            string[] values = StateNamesOrIds.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (int i = 0; i < values.Length; i++)
                _states.Add(values[i]);
            return _states;
        }
    }
}
