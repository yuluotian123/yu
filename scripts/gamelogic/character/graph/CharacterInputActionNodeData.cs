using System;
using System.Collections.Generic;
using Godot;

namespace GameLogic
{
    public enum CharacterInputTriggerMode
    {
        Pressed,
        Released,
        Held,
        Axis1D
    }

    public class CharacterInputActionNodeData : GraphNodeData
    {
        public string ActionName { get; set; } = string.Empty;
        public string HandlerLayer { get; set; } = string.Empty;
        public string NegativeAction { get; set; } = string.Empty;
        public string PositiveAction { get; set; } = string.Empty;
        public CharacterInputTriggerMode TriggerMode { get; set; } = CharacterInputTriggerMode.Pressed;
        public bool ConsumeInput { get; set; } = true;
        public float BufferTime { get; set; } = 0.12f;
        public float AxisDeadzone { get; set; } = 0.1f;
        public float AxisThreshold { get; set; } = 0.1f;
        public float HoldTime { get; set; }
        public float ValueScale { get; set; } = 1f;
        public bool InvertValue { get; set; }

        public override List<string> GetGraphTypes() => new() { CharacterGraphAsset.CharacterGraphTypeName };
        public override string GetMenuName() => "Input Action";
        public override string GetCategory() => "Character / Input";
        public override Color GetNodeColor() => new(0.25f, 0.7f, 0.95f);
        public override string GetDisplayName() => TriggerMode == CharacterInputTriggerMode.Axis1D
            ? $"Axis: {NegativeAction} / {PositiveAction}"
            : string.IsNullOrWhiteSpace(ActionName) ? "Input Action" : $"Input: {ActionName}";
        public override int GetInputCount() => 0;
        public override int GetOutputCount() => 1;
        public override int GetOutputMaxConnections(int port) => -1;
        public override string GetOutputPortName(int port) => "Triggered";
        public override bool CanBePrime() => false;

        public bool IsTriggered(ICharacterInputProvider provider)
        {
            if (provider == null)
                return false;

            return TriggerMode switch
            {
                CharacterInputTriggerMode.Pressed => provider.IsJustPressed(ActionName, HandlerLayer) ||
                    (BufferTime > 0f && provider.IsBuffered(ActionName, BufferTime)),
                CharacterInputTriggerMode.Released => provider.IsJustReleased(ActionName, HandlerLayer),
                CharacterInputTriggerMode.Held => provider.IsPressed(ActionName, HandlerLayer) &&
                    provider.GetHoldTime(ActionName) >= HoldTime,
                CharacterInputTriggerMode.Axis1D => Mathf.Abs(ResolveValue(provider)) >=
                    Mathf.Max(AxisDeadzone, AxisThreshold),
                _ => false
            };
        }

        public float ReadValue(ICharacterInputProvider provider)
        {
            float value = ResolveValue(provider);
            if (ConsumeInput)
                Consume(provider);
            return value;
        }

        public override void Validate(GraphAsset graph, GraphValidationResult result)
        {
            if (TriggerMode == CharacterInputTriggerMode.Axis1D)
            {
                if (string.IsNullOrWhiteSpace(NegativeAction) || string.IsNullOrWhiteSpace(PositiveAction))
                    result.AddError("Axis1D requires negative and positive InputMap actions.", Id);
            }
            else if (string.IsNullOrWhiteSpace(ActionName))
            {
                result.AddError("Character Input requires an InputMap action.", Id);
            }

            if (graph?.GetIncomingConnections(Id).Count > 0)
                result.AddError("Character Input nodes cannot have incoming connections.", Id);

            if (graph?.GetOutgoingConnections(Id).Count == 0)
                result.AddWarning("Character Input is not connected to an Action.", Id);
        }

        public override void CreateNodeUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(210f, 0f) };
            root.AddChild(new Label { Text = string.IsNullOrWhiteSpace(ActionName) ? "Action: none" : ActionName });
            root.AddChild(new Label { Text = TriggerMode.ToString() });
            context.GraphNode.AddChild(root);
        }

        public override void CreateUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(220f, 0f) };
            AddEditorFields(root);
            context.GraphNode.AddChild(root);
        }

        public override Control CreateInspectorUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(280f, 0f) };
            AddEditorFields(root);
            return root;
        }

        private void AddEditorFields(VBoxContainer root)
        {
            var action = new LineEdit { Text = ActionName, PlaceholderText = "Logical InputMap action" };
            action.TextChanged += value => ActionName = value.Trim();
            root.AddChild(action);

            var negative = new LineEdit { Text = NegativeAction, PlaceholderText = "Axis negative action" };
            negative.TextChanged += value => NegativeAction = value.Trim();
            root.AddChild(negative);

            var positive = new LineEdit { Text = PositiveAction, PlaceholderText = "Axis positive action" };
            positive.TextChanged += value => PositiveAction = value.Trim();
            root.AddChild(positive);

            var mode = new OptionButton();
            foreach (string value in Enum.GetNames<CharacterInputTriggerMode>())
                mode.AddItem(value);
            mode.Select((int)TriggerMode);
            mode.ItemSelected += index => TriggerMode = (CharacterInputTriggerMode)index;
            root.AddChild(mode);

            var layer = new LineEdit { Text = HandlerLayer, PlaceholderText = "Input layer (optional)" };
            layer.TextChanged += value => HandlerLayer = value.Trim();
            root.AddChild(layer);

            AddFloatField(root, "Buffer", BufferTime, value => BufferTime = Mathf.Max(0f, value));
            AddFloatField(root, "Deadzone", AxisDeadzone, value => AxisDeadzone = Mathf.Clamp(value, 0f, 1f));
            AddFloatField(root, "Axis threshold", AxisThreshold, value => AxisThreshold = Mathf.Max(0f, value));
            AddFloatField(root, "Hold time", HoldTime, value => HoldTime = Mathf.Max(0f, value));
            AddFloatField(root, "Value scale", ValueScale, value => ValueScale = value);
            AddCheckBox(root, "Consume input", ConsumeInput, value => ConsumeInput = value);
            AddCheckBox(root, "Invert value", InvertValue, value => InvertValue = value);
        }

        private static void AddFloatField(VBoxContainer root, string label, float value, Action<float> setter)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = label });
            var spin = new SpinBox
            {
                Value = value,
                Step = 0.01,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            spin.ValueChanged += changed => setter((float)changed);
            row.AddChild(spin);
            root.AddChild(row);
        }

        private static void AddCheckBox(VBoxContainer root, string text, bool value, Action<bool> setter)
        {
            var check = new CheckBox { Text = text, ButtonPressed = value };
            check.Toggled += toggled => setter(toggled);
            root.AddChild(check);
        }

        private float ResolveValue(ICharacterInputProvider provider)
        {
            float value = TriggerMode == CharacterInputTriggerMode.Axis1D
                ? (provider?.GetActionStrength(PositiveAction, HandlerLayer) ?? 0f) -
                  (provider?.GetActionStrength(NegativeAction, HandlerLayer) ?? 0f)
                : provider?.GetActionStrength(ActionName, HandlerLayer) ?? 0f;
            if (InvertValue)
                value = -value;
            return value * ValueScale;
        }

        private void Consume(ICharacterInputProvider provider)
        {
            if (provider == null)
                return;

            switch (TriggerMode)
            {
                case CharacterInputTriggerMode.Pressed:
                    provider.ConsumeJustPressed(ActionName, HandlerLayer);
                    break;
                case CharacterInputTriggerMode.Released:
                    provider.ConsumeJustReleased(ActionName, HandlerLayer);
                    break;
                case CharacterInputTriggerMode.Held:
                    provider.ConsumePressed(ActionName, HandlerLayer);
                    break;
                case CharacterInputTriggerMode.Axis1D:
                    provider.ConsumePressed(NegativeAction, HandlerLayer);
                    provider.ConsumePressed(PositiveAction, HandlerLayer);
                    break;
                default:
                    provider.ConsumePressed(ActionName, HandlerLayer);
                    break;
            }
        }

    }
}
