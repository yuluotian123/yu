using System;
using Godot;

namespace GameLogic
{
    public enum CharacterGraphRelationKind
    {
        Flow,
        Interrupt,
        Completion
    }

    public class CharacterGraphConnection : FlowConnection
    {
        public CharacterGraphRelationKind RelationKind { get; set; } = CharacterGraphRelationKind.Flow;
        public float WindowStart { get; set; }
        public float WindowEnd { get; set; } = -1f;
        public int RequestPriority { get; set; }

        public bool IsWithinWindow(double elapsed)
        {
            return elapsed >= Math.Max(0f, WindowStart) &&
                (WindowEnd < 0f || elapsed <= WindowEnd);
        }

        public override string GetDisplayName()
        {
            return RelationKind == CharacterGraphRelationKind.Flow
                ? base.GetDisplayName()
                : $"{RelationKind} [{WindowStart:0.##}, {(WindowEnd < 0f ? "any" : WindowEnd.ToString("0.##"))}]";
        }

        public override Control CreateInspectorUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 6);

            var kind = new OptionButton();
            foreach (string name in Enum.GetNames<CharacterGraphRelationKind>())
                kind.AddItem(name);
            kind.Select((int)RelationKind);
            kind.ItemSelected += index => RelationKind = (CharacterGraphRelationKind)index;
            root.AddChild(kind);

            root.AddChild(BuildSpin("Window Start", WindowStart, -1, 9999, value => WindowStart = (float)value));
            root.AddChild(BuildSpin("Window End (-1 = any)", WindowEnd, -1, 9999, value => WindowEnd = (float)value));
            root.AddChild(BuildSpin("Request Priority", RequestPriority, -1000, 1000, value => RequestPriority = (int)value));
            return root;
        }

        private static Control BuildSpin(string label, double value, double min, double max, Action<double> setter)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(130, 0) });
            var spin = new SpinBox { Value = value, MinValue = min, MaxValue = max, Step = 0.01 };
            spin.ValueChanged += value => setter(value);
            row.AddChild(spin);
            return row;
        }
    }
}
