#if TOOLS
using System;
using Godot;

namespace GameLogic;

[Tool]
public partial class CharacterNeedsProperty : EditorProperty
{
    readonly VBoxContainer _body = new();
    CharacterNeedsDefinition _owner, _preset;
    Godot.Collections.Array<CharacterNeedDefinition> _shown;
    bool _built;

    public CharacterNeedsProperty() { AddChild(_body); SetBottomEditor(_body); }
    public CharacterNeedsProperty(CharacterNeedsDefinition owner) : this()
    {
        _owner = owner;
        _preset = GD.Load<CharacterNeedsDefinition>(CharacterNeedsDefinition.CommonPresetPath);
    }
    public override void _UpdateProperty()
    {
        if (!GodotObject.IsInstanceValid(_owner)) return;
        if (_built && ReferenceEquals(_shown, _owner.Needs)) return;
        _shown = _owner.Needs; Rebuild();
    }
    void Commit(Godot.Collections.Array<CharacterNeedDefinition> needs)
    {
        _shown = needs;
        EmitChanged(nameof(CharacterNeedsDefinition.Needs), Variant.From(needs));
        Callable.From(Rebuild).CallDeferred();
    }
    void Rebuild()
    {
        if (!GodotObject.IsInstanceValid(_body) || _body.IsQueuedForDeletion()) return;
        foreach (Node child in _body.GetChildren())
        { GraphEditorSignalCleanup.PrepareForRemoval(child); _body.RemoveChild(child); }
        _built = true;
        var hint = new Label { Text = "预设只补充缺少的需求，保留已有配置。", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _body.AddChild(hint);
        var preset = new EditorResourcePicker { BaseType = nameof(CharacterNeedsDefinition), EditedResource = _preset };
        preset.ResourceChanged += resource => _preset = resource as CharacterNeedsDefinition;
        preset.ResourceSelected += (resource, _) => EditorInterface.Singleton.EditResource(resource);
        _body.AddChild(preset);
        var buttons = new HFlowContainer();
        var apply = new Button { Text = "补充所选预设" };
        var custom = new Button { Text = "+ 自定义需求" };
        var error = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        apply.Pressed += () =>
        {
            try { Commit(_owner.MergePreset(_preset)); }
            catch (ArgumentException exception) { error.Text = exception.Message; }
        };
        custom.Pressed += () => Commit(_owner.AddCustomNeed());
        buttons.AddChild(apply); buttons.AddChild(custom); _body.AddChild(buttons); _body.AddChild(error);
        for (int i = 0; i < _shown.Count; i++)
        {
            int index = i;
            var need = _shown[i];
            var entry = new VBoxContainer();
            entry.AddChild(new Label { Text = need == null ? "空需求" : (string.IsNullOrWhiteSpace(need.DisplayName) ? need.NeedId : need.DisplayName) + "  [" + need.NeedId + "]",
                AutowrapMode = TextServer.AutowrapMode.WordSmart });
            var row = new HBoxContainer();
            var picker = new EditorResourcePicker { BaseType = nameof(CharacterNeedDefinition), EditedResource = need, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            picker.ResourceChanged += resource =>
            {
                var next = new Godot.Collections.Array<CharacterNeedDefinition>(_owner.Needs);
                next[index] = resource as CharacterNeedDefinition; Commit(next);
            };
            picker.ResourceSelected += (resource, _) => EmitSignal(SignalName.ResourceSelected, nameof(CharacterNeedsDefinition.Needs), resource);
            var remove = new Button { Text = "×", TooltipText = "移除这项需求；相关条件引用需要一并调整。" };
            remove.Pressed += () => { var next = new Godot.Collections.Array<CharacterNeedDefinition>(_owner.Needs); next.RemoveAt(index); Commit(next); };
            row.AddChild(picker); row.AddChild(remove); entry.AddChild(row); _body.AddChild(entry);
        }
        _body.AddChild(new Label { Text = "点击需求资源编辑基础数值与紧迫度规则；\n默认阈值随需求范围缩放，取消 Use Default Thresholds 可自定义。\n新增项会自动注册到条件参数选择器。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
    }
}
#endif
