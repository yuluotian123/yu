#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic.AI;

[Tool]
public partial class NpcExpressionProperty : EditorProperty
{
    readonly NpcGraphAsset _graph;
    readonly GodotObject _owner;
    readonly string _property;
    NpcValueType _type;
    readonly VBoxContainer _body = new();
    NpcExpressionDefinition _shown;
    bool _built;

    public NpcExpressionProperty()
    {
        AddChild(_body); SetBottomEditor(_body);
    }
    public NpcExpressionProperty(NpcGraphAsset graph, GodotObject owner, string property, NpcValueType type) : this()
    {
        _graph = graph; _owner = owner; _property = property; _type = type;
        if (owner is NpcEffectDefinition) Label = "效果值";
    }

    public override void _UpdateProperty()
    {
        if (!GodotObject.IsInstanceValid(_owner)) return;
        if (_owner is NpcEffectDefinition) Label = "效果值";
        var value = _owner.Get(_property).AsGodotObject() as NpcExpressionDefinition;
        var type = NpcExpressionEditorCatalog.ExpectedType(_graph, _owner, _property) ?? _type;
        if (_built && value == _shown && type == _type) return;
        _type = type;
        _shown = value; Rebuild();
    }

    void Rebuild()
    {
        if (!GodotObject.IsInstanceValid(_body) || _body.IsQueuedForDeletion()) return;
        foreach (Node child in _body.GetChildren())
        {
            GraphEditorSignalCleanup.PrepareForRemoval(child); _body.RemoveChild(child);
        }
        _built = true;
        if (_owner is NpcEffectDefinition or NpcBindingDefinition && _shown != null &&
            NpcExpressionEditorCatalog.TypeOf(_shown, NpcExpressionEditorCatalog.For(_graph,_owner)) is var actual &&
            actual != NpcValueType.Unknown && actual != _type)
            _body.AddChild(new Label { Text = "值的类型不匹配，请清空后重新配置。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _body.AddChild(NpcExpressionEditor.Build(_graph, _owner, _type, _shown, (value, structural) =>
        {
            _shown = value;
            EmitChanged(_property, value);
            if (structural) Callable.From(Rebuild).CallDeferred();
        }));
    }
}

/// <summary>Edits immutable replacements so native Inspector undo retains the previous expression.</summary>
public static class NpcExpressionEditor
{
    static readonly NpcExpressionKind[] Comparisons = { NpcExpressionKind.Equal, NpcExpressionKind.NotEqual,
        NpcExpressionKind.Greater, NpcExpressionKind.AtLeast, NpcExpressionKind.Less, NpcExpressionKind.AtMost };
    static readonly NpcExpressionKind[] Arithmetic = { NpcExpressionKind.Add, NpcExpressionKind.Subtract,
        NpcExpressionKind.Multiply, NpcExpressionKind.Divide, NpcExpressionKind.Minimum, NpcExpressionKind.Maximum, NpcExpressionKind.Clamp01 };
    static string Op(NpcExpressionKind kind) => kind switch
    {
        NpcExpressionKind.Equal => "=", NpcExpressionKind.NotEqual => "≠", NpcExpressionKind.Greater => ">",
        NpcExpressionKind.AtLeast => "≥", NpcExpressionKind.Less => "<", NpcExpressionKind.AtMost => "≤",
        NpcExpressionKind.Add => "+", NpcExpressionKind.Subtract => "−", NpcExpressionKind.Multiply => "×",
        NpcExpressionKind.Divide => "÷", NpcExpressionKind.Minimum => "最小值", NpcExpressionKind.Maximum => "最大值",
        NpcExpressionKind.Clamp01 => "限制到 0～1", _ => kind.ToString()
    };
    public static NpcExpressionDefinition Constant(NpcValueType type) => new() { ValueType = type, Boolean = true };
    public static NpcExpressionDefinition Operation(NpcExpressionKind kind, params NpcExpressionDefinition[] inputs) =>
        new() { Kind = kind, Inputs = new(inputs) };
    public static NpcExpressionDefinition Reference(NpcEditorSymbol symbol) => new()
        { Kind = symbol.Parameter ? NpcExpressionKind.Parameter : NpcExpressionKind.Fact, Key = symbol.Key };

    public static Control Build(NpcGraphAsset graph, GodotObject owner, NpcValueType expected,
        NpcExpressionDefinition value, Action<NpcExpressionDefinition, bool> changed)
    {
        bool action = owner is NpcActionDefinition or NpcEffectDefinition or NpcTargetSelectorDefinition;
        var view = new Builder(() => NpcExpressionEditorCatalog.For(graph,owner));
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        if (value == null)
        {
            var create = new Button { Text = expected == NpcValueType.Boolean ? "添加条件" : "添加数值表达式" };
            create.Pressed += () => changed(expected == NpcValueType.Boolean ? view.DefaultCondition() : Constant(expected), true);
            root.AddChild(create);
        }
        else
        {
            root.AddChild(expected == NpcValueType.Boolean
                ? view.Group(value.Kind is NpcExpressionKind.All or NpcExpressionKind.Any ? value : Operation(NpcExpressionKind.All, value), changed, 0)
                : view.Value(value, expected, changed, 0));
            var clear = new Button { Text = "清空表达式", Flat = true, TooltipText = "必填字段清空后，需要重新配置才能通过图验证。" };
            clear.Pressed += () => changed(null, true); root.AddChild(clear);
        }
        return root;
    }

    sealed record Choice(string Label, string Group, string Search, Func<NpcExpressionDefinition> Create);
    sealed class Builder
    {
        readonly Func<IReadOnlyList<NpcEditorSymbol>> _collect;
        public Builder(Func<IReadOnlyList<NpcEditorSymbol>> collect) => _collect = collect;
        NpcValueType Type(NpcExpressionDefinition value) => NpcExpressionEditorCatalog.TypeOf(value, _collect());
        public NpcExpressionDefinition DefaultCondition()
        {
            var first = _collect().FirstOrDefault(s => s.Type == NpcValueType.Number);
            return Operation(NpcExpressionKind.AtLeast, first == null ? Constant(NpcValueType.Number) : Reference(first), Constant(NpcValueType.Number));
        }
        static NpcExpressionDefinition Replace(NpcExpressionDefinition source, int index, NpcExpressionDefinition value) =>
            Operation(source.Kind, source.Inputs.Select((x, i) => i == index ? value : x).ToArray());
        static HFlowContainer Row() => new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        static Button Button(string text) => new() { Text = text, CustomMinimumSize = new(92, 0),
            ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

        public Control Group(NpcExpressionDefinition group, Action<NpcExpressionDefinition, bool> set, int depth)
        {
            var root = new VBoxContainer();
            if (depth > 24) { root.AddChild(new Label { Text = "条件嵌套过深" }); return root; }
            var header = Row();
            var mode = new OptionButton(); mode.AddItem("全部满足（AND）"); mode.AddItem("任一满足（OR）");
            mode.Select(group.Kind == NpcExpressionKind.Any ? 1 : 0);
            mode.ItemSelected += i => set(Operation(i == 0 ? NpcExpressionKind.All : NpcExpressionKind.Any, group.Inputs.ToArray()), true);
            header.AddChild(mode); root.AddChild(header);
            for (int i = 0; i < group.Inputs.Count; i++)
            {
                int index = i;
                var line = new HBoxContainer();
                var content = Condition(group.Inputs[i], (e, rebuild) => { group = Replace(group, index, e); set(group, rebuild); }, depth + 1);
                content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; line.AddChild(content);
                var remove = new Button { Text = "×", TooltipText = "删除条件", SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
                remove.Pressed += () => set(Operation(group.Kind, group.Inputs.Where((_, n) => n != index).ToArray()), true);
                line.AddChild(remove); root.AddChild(line);
            }
            if (group.Inputs.Count == 0) root.AddChild(new Label { Text = group.Kind == NpcExpressionKind.All ? "空条件组：始终满足" : "空条件组：始终不满足" });
            var footer = Row(); var add = new Button { Text = "+ 条件" }; var nested = new Button { Text = "+ 条件组" };
            add.Pressed += () => set(Operation(group.Kind, group.Inputs.Append(DefaultCondition()).ToArray()), true);
            nested.Pressed += () => set(Operation(group.Kind, group.Inputs.Append(Operation(NpcExpressionKind.Any, DefaultCondition())).ToArray()), true);
            footer.AddChild(add); footer.AddChild(nested); root.AddChild(footer); return root;
        }

        Control Condition(NpcExpressionDefinition expression, Action<NpcExpressionDefinition, bool> set, int depth)
        {
            if (expression.Kind is NpcExpressionKind.All or NpcExpressionKind.Any) return Group(expression, set, depth);
            if (expression.Kind is NpcExpressionKind.Not or NpcExpressionKind.Known)
            {
                var box = new VBoxContainer(); box.AddChild(new Label { Text = expression.Kind == NpcExpressionKind.Not ? "以下条件不满足（NOT）" : "以下参数已知" });
                if (expression.Inputs.Count == 1) box.AddChild(expression.Kind == NpcExpressionKind.Not
                    ? Condition(expression.Inputs[0], (e, b) => set(Operation(expression.Kind, e), b), depth + 1)
                    : Value(expression.Inputs[0], Type(expression.Inputs[0]), (e, b) => set(Operation(expression.Kind, e), b), depth + 1, true));
                return box;
            }
            // A boolean reference is presented as “parameter = true” without changing storage on selection.
            var condition = Comparisons.Contains(expression.Kind) && expression.Inputs.Count == 2
                ? expression : Operation(NpcExpressionKind.Equal, expression, Constant(NpcValueType.Boolean));
            var left = condition.Inputs[0]; var right = condition.Inputs[1]; var type = Type(left);
            var row = Row();
            row.AddChild(Value(left, type, (e, b) =>
            {
                var nextType = Type(e); var op = nextType == NpcValueType.Number || condition.Kind is NpcExpressionKind.Equal or NpcExpressionKind.NotEqual ? condition.Kind : NpcExpressionKind.Equal;
                left = e; right = nextType == type ? right : Constant(nextType);
                condition = Operation(op, left, right); set(condition, true);
            }, depth + 1, true));
            var operators = type == NpcValueType.Number ? Comparisons : Comparisons.Take(2).ToArray();
            var picker = Operator(operators, condition.Kind, op => { condition = Operation(op, left, right); set(condition, true); }); row.AddChild(picker);
            row.AddChild(Value(right, type, (e, b) => { right = e; condition = Operation(condition.Kind, left, right); set(condition, b); }, depth + 1));
            return row;
        }

        static OptionButton Operator(NpcExpressionKind[] kinds, NpcExpressionKind selected, Action<NpcExpressionKind> changed)
        {
            var picker = new OptionButton { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
            foreach (var kind in kinds) picker.AddItem(Op(kind));
            picker.Select(Math.Max(0, Array.IndexOf(kinds, selected)));
            picker.ItemSelected += i => changed(kinds[(int)i]); return picker;
        }

        public Control Value(NpcExpressionDefinition expression, NpcValueType expected,
            Action<NpcExpressionDefinition, bool> set, int depth, bool anyType = false)
        {
            var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            if (depth > 24) { box.AddChild(new Label { Text = "表达式嵌套过深" }); return box; }
            Container row = expression.Kind == NpcExpressionKind.Literal ? new HBoxContainer() : Row();
            var symbol = _collect().FirstOrDefault(s => s.Key == expression.Key && s.Parameter == (expression.Kind == NpcExpressionKind.Parameter));
            string label = expression.Kind switch
            {
                NpcExpressionKind.Literal => expression.ValueType switch { NpcValueType.Number => "数字", NpcValueType.Boolean => "布尔值", _ => "文本" },
                NpcExpressionKind.Fact or NpcExpressionKind.Parameter => symbol?.Label ?? "未注册：" + expression.Key,
                _ => "计算"
            };
            var choose = Button(label); choose.TooltipText = expression.Key;
            if (expression.Kind == NpcExpressionKind.Literal)
            {
                choose.Text = "⋯"; choose.CustomMinimumSize = new(24, 0);
                choose.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
                choose.TooltipText = label + "常量；点击切换为已注册参数或其他常量";
            }
            choose.Pressed += () => Pick(choose, expected, anyType, e => set(e, true)); row.AddChild(choose);
            if (expression.Kind == NpcExpressionKind.Literal)
            {
                if (expression.ValueType == NpcValueType.Number)
                {
                    var number = new SpinBox { MinValue = 0, MaxValue = 100, AllowGreater = true, AllowLesser = true, Step = .001, Value = expression.Number, CustomMinimumSize = new(78, 0) };
                    number.ValueChanged += n => set(new() { Number = n }, false); row.AddChild(number);
                }
                else if (expression.ValueType == NpcValueType.Boolean)
                {
                    var boolean = new CheckBox { Text = "真", ButtonPressed = expression.Boolean };
                    boolean.Toggled += b => set(new() { ValueType = NpcValueType.Boolean, Boolean = b }, false); row.AddChild(boolean);
                }
                else
                {
                    var text = new LineEdit { Text = expression.Symbol, CustomMinimumSize = new(110, 0), PlaceholderText = "文本常量" };
                    text.TextChanged += s => set(new() { ValueType = NpcValueType.Symbol, Symbol = s }, false); row.AddChild(text);
                }
            }
            box.AddChild(row);
            if (Arithmetic.Contains(expression.Kind))
            {
                var math = Row();
                var operation = Operator(Arithmetic, expression.Kind, op =>
                {
                    int count = op == NpcExpressionKind.Clamp01 ? 1 : 2;
                    set(Operation(op, Enumerable.Range(0, count).Select(i => i < expression.Inputs.Count ? expression.Inputs[i] : Constant(NpcValueType.Number)).ToArray()), true);
                });
                for (int i = 0; i < expression.Inputs.Count; i++)
                {
                    int index = i;
                    if (i == 1 || expression.Inputs.Count == 1) math.AddChild(operation);
                    else if (i > 1) math.AddChild(new Label { Text = Op(expression.Kind) });
                    math.AddChild(Value(expression.Inputs[i], NpcValueType.Number, (e, b) => { expression = Replace(expression, index, e); set(expression, b); }, depth + 1));
                }
                box.AddChild(math);
            }
            return box;
        }

        void Pick(Control anchor, NpcValueType expected, bool anyType, Action<NpcExpressionDefinition> selected)
        {
            var choices = _collect().Where(s => anyType || s.Type == expected)
                .Select(s => new Choice(s.Label == s.Key ? s.Label : s.Label + "  [" + s.Key + "]", s.Group, s.Key + " " + s.Label + " " + s.Type, () => Reference(s))).ToList();
            foreach (var type in new[] { NpcValueType.Number, NpcValueType.Boolean, NpcValueType.Symbol }.Where(t => anyType || t == expected))
            {
                var captured = type;
                choices.Add(new(type switch { NpcValueType.Number => "数字常量", NpcValueType.Boolean => "布尔常量", _ => "文本常量" }, "常量", type.ToString(), () => Constant(captured)));
            }
            if (anyType || expected == NpcValueType.Number)
                choices.Add(new("数值计算（参数 运算符 参数）", "计算", "加 减 乘 除 最小 最大", () => Operation(NpcExpressionKind.Multiply, Constant(NpcValueType.Number), new() { Number = 1 })));
            var popup = new SearchablePopup<Choice>(choices, c => c.Label, c => c.Group, c => c.Search);
            popup.OnItemSelected += c => selected(c.Create()); popup.ShowBelow(anchor);
        }
    }
}
#endif
