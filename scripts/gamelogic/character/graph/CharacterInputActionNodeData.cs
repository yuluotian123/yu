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
        public float AxisThreshold { get; set; } = 0.1f;
        public float HoldTime { get; set; }
        public float ValueScale { get; set; } = 1f;
        public bool InvertValue { get; set; }

        public override List<string> GetGraphTypes() => new() { CharacterGraphAsset.CharacterGraphTypeName };
        public override string GetMenuName() => "输入事件（Input Action）";
        public override string GetCategory() => "角色 / 输入";
        public override Color GetNodeColor() => new(0.25f, 0.7f, 0.95f);
        public override string GetDisplayName() => TriggerMode == CharacterInputTriggerMode.Axis1D
            ? $"Axis: {NegativeAction} / {PositiveAction}"
            : string.IsNullOrWhiteSpace(ActionName) ? "输入事件（Input Action）" : $"Input: {ActionName}";
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
                    Mathf.Max(0f, AxisThreshold),
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
            AddEditorFields(root, context);
            context.GraphNode.AddChild(root);
        }

        public override Control CreateInspectorUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(280f, 0f) };
            AddEditorFields(root, context);
            return root;
        }

        private void AddEditorFields(VBoxContainer root, GraphEditorContext context)
        {
            void Changed() => context?.CurrentGraph?.MarkDirty();
            var mode = new OptionButton { Name = "TriggerMode" };
            foreach (string label in new[] { "按下（Pressed）", "松开（Released）", "按住（Held）", "一维轴（Axis1D）" })
                mode.AddItem(label);
            mode.Select((int)TriggerMode);
            root.AddChild(mode);
            var fields = new VBoxContainer { Name = "ModeFields" }; root.AddChild(fields);
            void Text(string name, string label, string value, Action<string> setter)
            {
                fields.AddChild(new Label { Text = label });
                var input = new LineEdit { Name = name, Text = value };
                input.TextChanged += text => { setter(text.Trim()); Changed(); };
                fields.AddChild(input);
            }
            void Refresh()
            {
                foreach (Node child in fields.GetChildren()) { fields.RemoveChild(child); child.QueueFree(); }
                if (TriggerMode == CharacterInputTriggerMode.Axis1D)
                {
                    Text("NegativeAction", "负向输入", NegativeAction, value => NegativeAction = value);
                    Text("PositiveAction", "正向输入", PositiveAction, value => PositiveAction = value);
                    AddFloatField(fields, "触发阈值", AxisThreshold, value => { AxisThreshold = Mathf.Max(0, value); Changed(); });
                    AddFloatField(fields, "数值缩放", ValueScale, value => { ValueScale = value; Changed(); }, -100, 100);
                    AddCheckBox(fields, "反向", InvertValue, value => { InvertValue = value; Changed(); });
                }
                else
                {
                    Text("ActionName", "输入名称", ActionName, value => ActionName = value);
                    if (TriggerMode == CharacterInputTriggerMode.Pressed)
                        AddFloatField(fields, "缓存秒数", BufferTime, value => { BufferTime = value; Changed(); });
                    if (TriggerMode == CharacterInputTriggerMode.Held)
                        AddFloatField(fields, "按住秒数", HoldTime, value => { HoldTime = value; Changed(); });
                }
                Text("HandlerLayer", "输入层（可选）", HandlerLayer, value => HandlerLayer = value);
                AddCheckBox(fields, "消费输入", ConsumeInput, value => { ConsumeInput = value; Changed(); });
            }
            mode.ItemSelected += index =>
            {
                TriggerMode = (CharacterInputTriggerMode)index; Changed();
                Callable.From(() => { if (GodotObject.IsInstanceValid(fields) && !fields.IsQueuedForDeletion()) Refresh(); }).CallDeferred();
            };
            Refresh();
        }

        private static void AddFloatField(VBoxContainer root, string label, float value, Action<float> setter, double min = 0, double max = 999999)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = label });
            var spin = new SpinBox
            {
                MinValue = min, MaxValue = max, Value = value,
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
            if (TriggerMode != CharacterInputTriggerMode.Axis1D) return value;
            if (InvertValue) value = -value;
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
