#if TOOLS
using System.Linq;
using Godot;

namespace GameLogic.AI;

[Tool]
public partial class NpcEffectProperty : EditorProperty
{
    readonly VBoxContainer _body = new();
    readonly NpcGraphAsset _graph;
    readonly NpcEffectDefinition _owner;
    readonly string _property;
    string _key;
    NpcEffectMode _mode;
    bool _built;

    public NpcEffectProperty() { AddChild(_body); SetBottomEditor(_body); }
    public NpcEffectProperty(NpcGraphAsset graph, NpcEffectDefinition owner, string property) : this()
    {
        _graph = graph; _owner = owner; _property = property;
        Label = property == nameof(NpcEffectDefinition.Key) ? "修改参数" : "修改方式";
    }

    public override void _UpdateProperty()
    {
        if (!GodotObject.IsInstanceValid(_owner)) return;
        Label = _property == nameof(NpcEffectDefinition.Key) ? "修改参数" : "修改方式";
        if (_built && _key == _owner.Key && _mode == _owner.Mode) return;
        bool refresh = _built;
        _key = _owner.Key; _mode = _owner.Mode; Rebuild();
        // Refresh the value editor's expected type after a key/mode change, including undo.
        if (refresh) Callable.From(() => { if (GodotObject.IsInstanceValid(_owner)) _owner.NotifyPropertyListChanged(); }).CallDeferred();
    }

    void Rebuild()
    {
        if (!GodotObject.IsInstanceValid(_body) || _body.IsQueuedForDeletion()) return;
        foreach (Node child in _body.GetChildren())
        { GraphEditorSignalCleanup.PrepareForRemoval(child); _body.RemoveChild(child); }
        _built = true;
        var target = NpcExpressionEditorCatalog.WritableTargets(_graph,_owner).FirstOrDefault(s => s.Key == _key);
        if (_property == nameof(NpcEffectDefinition.Key))
        {
            var choose = new Button
            {
                Text = target?.Label ?? (string.IsNullOrWhiteSpace(_key) ? "选择可修改参数" : "未注册或不可写：" + _key),
                TooltipText = _key, ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            choose.Pressed += () =>
            {
                var choices = NpcExpressionEditorCatalog.WritableTargets(_graph,_owner)
                    .Where(s => _owner.Mode != NpcEffectMode.Add || s.Type == CharacterValueType.Number).ToArray();
                var popup = new SearchablePopup<NpcEditorSymbol>(choices,
                    s => s.Label == s.Key ? s.Label : s.Label + "  [" + s.Key + "]",
                    s => s.Group, s => s.Key + " " + s.Label + " " + s.Type);
                popup.OnItemSelected += s => EmitChanged(nameof(NpcEffectDefinition.Key), s.Key);
                popup.ShowBelow(choose);
            };
            _body.AddChild(choose);
            if (target == null) _body.AddChild(new Label { Text = "请选择已注册的可写参数；原引用已保留。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        }
        else
        {
            var mode = new OptionButton(); mode.AddItem("设为（Set）"); mode.AddItem("增加/减少（Add）");
            mode.SetItemDisabled(1, target?.Type != CharacterValueType.Number); mode.Select((int)_mode);
            mode.ItemSelected += index => EmitChanged(nameof(NpcEffectDefinition.Mode), index);
            _body.AddChild(mode);
            _body.AddChild(new Label { Text = "设为：使用效果值替换；增加/减少：正数增加、负数减少。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        }
    }
}
#endif
