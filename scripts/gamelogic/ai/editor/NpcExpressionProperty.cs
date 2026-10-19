#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic.AI;

public delegate void NpcExpressionPick(Control anchor,NpcValueType expected,bool anyType,Action<NpcExpressionDefinition> selected);

[Tool]
public partial class NpcExpressionProperty : EditorProperty
{
    readonly NpcGraphAsset _graph;
    readonly GodotObject _owner;
    readonly string _property;
    NpcValueType _type;
    readonly VBoxContainer _body = new();
    NpcExpressionDefinition _shown;
    string _catalog;
    bool _built;

    public NpcExpressionProperty()
    {
        AddChild(_body); SetBottomEditor(_body);
    }
    public NpcExpressionProperty(NpcGraphAsset graph, GodotObject owner, string property, NpcValueType type) : this()
    {
        _graph = graph; _owner = owner; _property = property; _type = type;
        Label = FieldLabel;
    }
    string FieldLabel=>_property switch
    {
        nameof(NpcGoalDefinition.Activation)=>"启用条件",nameof(NpcGoalDefinition.Completion)=>"完成条件",nameof(NpcGoalDefinition.Utility)=>"评分公式",
        nameof(NpcActionDefinition.Preconditions)=>"前置条件",nameof(NpcActionDefinition.SuccessConditions)=>"成功条件",nameof(NpcActionDefinition.Cost)=>"行动成本",
        nameof(NpcTargetSelectorDefinition.Filter)=>"候选筛选",nameof(NpcTargetSelectorDefinition.Score)=>"候选评分",nameof(NpcNeedDefinition.RateMultiplier)=>"变化倍率",
        nameof(NpcEffectDefinition.Value)=>_owner is NpcEffectDefinition?"效果值":"输入值",_=>_property
    };
    bool Required=>_owner switch
    {
        NpcGoalDefinition=>_property is nameof(NpcGoalDefinition.Utility) or nameof(NpcGoalDefinition.Completion),
        NpcEffectDefinition=>true,NpcBindingDefinition binding=>binding.Source==NpcBindingSource.Expression,
        NpcTargetSelectorDefinition selector=>_property==nameof(NpcTargetSelectorDefinition.Score)&&selector.Selection is NpcTargetSelection.LowestScore or NpcTargetSelection.HighestScore,
        NpcActionDefinition action=>_property==nameof(NpcActionDefinition.SuccessConditions)&&action.Predictions.Any(e=>e.Key=="self.location"),_=>false
    };

    public override void _UpdateProperty()
    {
        if (!GodotObject.IsInstanceValid(_owner)) return;
        Label = FieldLabel;
        var value = _owner.Get(_property).AsGodotObject() as NpcExpressionDefinition;
        var type = NpcExpressionEditorCatalog.ExpectedType(_graph, _owner, _property) ?? _type;
        string catalog=string.Join("|",NpcExpressionEditorCatalog.For(_graph,_owner).Select(s=>s.Key+":"+s.Label+":"+s.Type));
        if (_built && value == _shown && type == _type&&catalog==_catalog) return;
        _catalog=catalog;
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
        var symbols=NpcExpressionEditorCatalog.For(_graph,_owner);
        string text=NpcExpressionPresentation.Format(_shown,symbols);
        var preview=new Label{Text=text.Length>180?text[..180]+"…":text,TooltipText=text,AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        _body.AddChild(preview);
        var edit=new Button{Text=_shown==null?"添加公式…":"编辑公式…",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        edit.Pressed+=()=>CreateEditor().PopupCenteredClamped(new Vector2I(1040,700),.9f);
        _body.AddChild(edit);
        string error=_shown==null&&Required?"此处必须配置公式。":NpcExpressionPresentation.Validate(_shown,_type,symbols);
        if(error.Length>0)_body.AddChild(new Label{Text=error,AutowrapMode=TextServer.AutowrapMode.WordSmart,TooltipText=error});
    }
    internal NpcExpressionDialog CreateEditor()
    {
        var original=_owner.Get(_property).AsGodotObject() as NpcExpressionDefinition;
        var dialog=new NpcExpressionDialog(_graph,_owner,_type,original,FieldLabel,value=>
        {
            if(!GodotObject.IsInstanceValid(_owner))return;
            _shown=value;EmitChanged(_property,value);Callable.From(Rebuild).CallDeferred();
        },()=>GodotObject.IsInstanceValid(_owner)&&_owner.Get(_property).AsGodotObject()==original,Required);
        AddChild(dialog);return dialog;
    }
}

/// <summary>Edits immutable replacements so native Inspector undo retains the previous expression.</summary>
public static class NpcExpressionEditor
{
    static readonly NpcExpressionKind[] Comparisons = { NpcExpressionKind.Equal, NpcExpressionKind.NotEqual,
        NpcExpressionKind.Greater, NpcExpressionKind.AtLeast, NpcExpressionKind.Less, NpcExpressionKind.AtMost };
    static readonly NpcExpressionKind[] Arithmetic = { NpcExpressionKind.Add, NpcExpressionKind.Subtract,
        NpcExpressionKind.Multiply, NpcExpressionKind.Divide, NpcExpressionKind.Minimum, NpcExpressionKind.Maximum, NpcExpressionKind.Clamp01 };
    static string Op(NpcExpressionKind kind)=>NpcExpressionPresentation.Operator(kind);
    public static NpcExpressionDefinition Constant(NpcValueType type) => new() { ValueType = type, Boolean = true };
    public static NpcExpressionDefinition Operation(NpcExpressionKind kind, params NpcExpressionDefinition[] inputs) =>
        new() { Kind = kind, Inputs = new(inputs) };
    public static NpcExpressionDefinition Reference(NpcEditorSymbol symbol) => new()
        { Kind = symbol.Parameter ? NpcExpressionKind.Parameter : NpcExpressionKind.Fact, Key = symbol.Key };

    public static Control Build(NpcGraphAsset graph, GodotObject owner, NpcValueType expected,
        NpcExpressionDefinition value, Action<NpcExpressionDefinition, bool> changed,NpcExpressionPick picker=null)
    {
        var view = new Builder(() => NpcExpressionEditorCatalog.For(graph,owner),picker);
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        if (value == null)
        {
            var create = new Button { Text = expected == NpcValueType.Boolean ? "添加条件" : expected==NpcValueType.Symbol?"添加文本或参数":"添加数值公式" };
            create.Pressed += () => changed(expected == NpcValueType.Boolean ? view.DefaultCondition() : Constant(expected), true);
            root.AddChild(create);
        }
        else
        {
            root.AddChild(expected == NpcValueType.Boolean
                ? view.Group(value.Kind is NpcExpressionKind.All or NpcExpressionKind.Any ? value : Operation(NpcExpressionKind.All, value), changed, 0)
                : view.Value(value, expected, changed, 0));
            if(picker==null)
            {
                var clear = new Button { Text = "清空表达式", Flat = true, TooltipText = "必填字段清空后，需要重新配置才能通过图验证。" };
                clear.Pressed += () => changed(null, true); root.AddChild(clear);
            }
        }
        return root;
    }

    sealed record Choice(string Label, string Group, string Search, Func<NpcExpressionDefinition> Create);
    sealed class Builder
    {
        readonly Func<IReadOnlyList<NpcEditorSymbol>> _collect;
        readonly NpcExpressionPick _picker;
        public Builder(Func<IReadOnlyList<NpcEditorSymbol>> collect,NpcExpressionPick picker) => (_collect,_picker)=(collect,picker);
        NpcValueType Type(NpcExpressionDefinition value) => NpcExpressionEditorCatalog.TypeOf(value, _collect());
        public NpcExpressionDefinition DefaultCondition()
        {
            var first = _collect().FirstOrDefault(s => s.Type == NpcValueType.Number);
            return Operation(NpcExpressionKind.AtLeast, first == null ? Constant(NpcValueType.Number) : Reference(first), Constant(NpcValueType.Number));
        }
        static NpcExpressionDefinition Replace(NpcExpressionDefinition source, int index, NpcExpressionDefinition value) =>
            Operation(source.Kind, source.Inputs.Select((x, i) => i == index ? value : x).ToArray());
        static HFlowContainer Row() => new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        static Button Button(string text) => new() { Text = text, CustomMinimumSize = new(116, 0),
            ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

        public Control Group(NpcExpressionDefinition group, Action<NpcExpressionDefinition, bool> set, int depth)
        {
            var root = new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};root.AddThemeConstantOverride("separation",8);
            if (depth > 24) { root.AddChild(new Label { Text = "条件嵌套过深" }); return root; }
            var header = Row();
            var mode = new OptionButton(); mode.AddItem("全部满足（AND）"); mode.AddItem("任一满足（OR）");
            mode.Select(group.Kind == NpcExpressionKind.Any ? 1 : 0);
            mode.ItemSelected += i => set(Operation(i == 0 ? NpcExpressionKind.All : NpcExpressionKind.Any, group.Inputs.ToArray()), true);
            header.AddChild(mode);root.AddChild(header);
            var panel=new PanelContainer();var style=new StyleBoxFlat{BgColor=new Color(.12f,.14f,.17f,.55f),BorderColor=new Color(.34f,.44f,.52f,.8f),BorderWidthLeft=2,ContentMarginLeft=10,ContentMarginRight=8,ContentMarginTop=8,ContentMarginBottom=8};
            panel.AddThemeStyleboxOverride("panel",style);var lines=new VBoxContainer();lines.AddThemeConstantOverride("separation",10);panel.AddChild(lines);root.AddChild(panel);
            for (int i = 0; i < group.Inputs.Count; i++)
            {
                int index = i;
                var line = new HBoxContainer();
                line.AddThemeConstantOverride("separation",8);
                line.AddChild(new Label{Text=(i+1).ToString(),SizeFlagsVertical=Control.SizeFlags.ShrinkBegin});
                var content = Condition(group.Inputs[i], (e, rebuild) => { group = Replace(group, index, e); set(group, rebuild); }, depth + 1);
                content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; line.AddChild(content);
                var remove = new Button { Text = "×", TooltipText = "删除条件", SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
                remove.Pressed += () => set(Operation(group.Kind, group.Inputs.Where((_, n) => n != index).ToArray()), true);
                line.AddChild(remove); lines.AddChild(line);
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
            if(Arithmetic.Contains(expression.Kind))
            {
                var math=Row();math.AddThemeConstantOverride("h_separation",6);math.AddThemeConstantOverride("v_separation",6);
                if(depth>0)math.AddChild(new Label{Text="("});
                var operation = Operator(Arithmetic, expression.Kind, op =>
                {
                    if(op==NpcExpressionKind.Clamp01&&expression.Kind!=NpcExpressionKind.Clamp01){set(Operation(op,expression),true);return;}
                    if(op is NpcExpressionKind.Subtract or NpcExpressionKind.Divide&&expression.Inputs.Count>2)
                    {
                        var folded=Operation(op,expression.Inputs[0],expression.Inputs[1]);
                        foreach(var operand in expression.Inputs.Skip(2))folded=Operation(op,folded,operand);
                        set(folded,true);return;
                    }
                    bool variadic=op is NpcExpressionKind.Add or NpcExpressionKind.Multiply or NpcExpressionKind.Minimum or NpcExpressionKind.Maximum;
                    int count = op == NpcExpressionKind.Clamp01 ? 1 : variadic?Math.Max(2,expression.Inputs.Count):2;
                    set(Operation(op, Enumerable.Range(0, count).Select(i => i < expression.Inputs.Count ? expression.Inputs[i] : Constant(NpcValueType.Number)).ToArray()), true);
                });
                if(expression.Inputs.Count<2)math.AddChild(operation);
                for(int i=0;i<expression.Inputs.Count;i++)
                {
                    int index=i;
                    if(i==1)math.AddChild(operation);else if(i>1)math.AddChild(new Label{Text=Op(expression.Kind)});
                    var operand=Value(expression.Inputs[i],NpcValueType.Number,(e,b)=>{expression=Replace(expression,index,e);set(expression,b);},depth+1);
                    if(expression.Inputs.Count>2&&expression.Kind is NpcExpressionKind.Add or NpcExpressionKind.Multiply or NpcExpressionKind.Minimum or NpcExpressionKind.Maximum)
                    {
                        var term=new HBoxContainer();term.AddChild(operand);var remove=new Button{Text="×",TooltipText="删除此运算项"};
                        remove.Pressed+=()=>set(Operation(expression.Kind,expression.Inputs.Where((_,n)=>n!=index).ToArray()),true);term.AddChild(remove);math.AddChild(term);
                    }
                    else math.AddChild(operand);
                }
                if(depth>0)math.AddChild(new Label{Text=")"});
                var replace=new Button{Text="⋯",TooltipText="将此计算替换成参数、常量或新的计算"};
                replace.Pressed+=()=>Pick(replace,expected,anyType,e=>set(e,true));math.AddChild(replace);
                box.AddChild(math);
                if(expression.Kind is NpcExpressionKind.Add or NpcExpressionKind.Multiply or NpcExpressionKind.Minimum or NpcExpressionKind.Maximum)
                {
                    var add=new Button{Text="＋ 运算项",Flat=true,SizeFlagsHorizontal=Control.SizeFlags.ShrinkBegin};
                    add.Pressed+=()=>set(Operation(expression.Kind,expression.Inputs.Append(Constant(NpcValueType.Number)).ToArray()),true);box.AddChild(add);
                }
                return box;
            }
            var row=new HBoxContainer();
            var symbol = _collect().FirstOrDefault(s => s.Key == expression.Key && s.Parameter == (expression.Kind == NpcExpressionKind.Parameter));
            if(expression.Kind!=NpcExpressionKind.Literal)
            {
                var choose=Button(symbol?.Label??"未注册："+expression.Key);
                choose.TooltipText=symbol==null?expression.Key:symbol.Group+" / "+symbol.Label+"\n"+symbol.Key;
                choose.Pressed+=()=>Pick(choose,expected,anyType,e=>set(e,true));row.AddChild(choose);box.AddChild(row);return box;
            }
            if(expression.ValueType==NpcValueType.Number)
            {
                var number=new SpinBox{MinValue=-1000000,MaxValue=1000000,AllowGreater=true,AllowLesser=true,Step=.001,Value=expression.Number,CustomMinimumSize=new(96,0),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
                number.ValueChanged+=n=>set(new(){Number=n},false);row.AddChild(number);
            }
            else if(expression.ValueType==NpcValueType.Boolean)
            {
                var boolean=new OptionButton();boolean.AddItem("真");boolean.AddItem("假");boolean.Select(expression.Boolean?0:1);
                boolean.ItemSelected+=i=>set(new(){ValueType=NpcValueType.Boolean,Boolean=i==0},false);row.AddChild(boolean);
            }
            else
            {
                var text=new LineEdit{Text=expression.Symbol,CustomMinimumSize=new(120,0),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,PlaceholderText="文本常量"};
                text.TextChanged+=v=>set(new(){ValueType=NpcValueType.Symbol,Symbol=v},false);row.AddChild(text);
            }
            var source=new Button{Text="⋯",TooltipText="改用参数、其他常量或计算"};
            source.Pressed+=()=>Pick(source,expected,anyType,e=>set(e,true));row.AddChild(source);box.AddChild(row);return box;
        }

        void Pick(Control anchor, NpcValueType expected, bool anyType, Action<NpcExpressionDefinition> selected)
        {
            if(_picker!=null){_picker(anchor,expected,anyType,selected);return;}
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
