#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic;
using Godot;

/// <summary>面向动画状态的属性面板；动画库来自编辑器中的序列化组件，不依赖运行时初始化。</summary>
[Tool]
public partial class GraphAnimationStateInspector : VBoxContainer
{
    private HfsmAnimationStateNodeData _state;
    private GraphEditorContext _context;
    private Button _animationButton;
    private Label _animationInfo;
    private LineEdit _manualAnimation;
    private double _refreshTime;
    private bool _canvasRefreshQueued;
    private SearchablePopup<string> _picker;

    public GraphAnimationStateInspector() { }

    public GraphAnimationStateInspector(HfsmAnimationStateNodeData state, GraphEditorContext context)
    {
        _state = state;
        _context = context;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 8);
        AddChild(GraphEditorUi.BuildLineEditRow("状态名称", state.StateName, "例如：待机、奔跑", value =>
        {
            state.StateName = value;
            Changed();
        }));
        AddChild(GraphEditorUi.BuildCheckRow("默认状态", state.IsDefault, value =>
        {
            state.IsDefault = value;
            Changed();
        }));
        AddChild(new HSeparator());
        AddChild(new Label { Text = "播放动画" });
        _animationButton = new Button { Name = "AnimationPicker", Text = "选择动画…", SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        _animationButton.Pressed += OpenPicker;
        AddChild(_animationButton);
        _animationInfo = new Label { Name = "AnimationInfo", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_animationInfo);
        AddChild(GraphEditorUi.BuildSpinRow("播放速度", state.Speed, -20, 20, 0.05, value => { state.Speed = (float)value; Changed(); }));

        var advanced = new FoldableContainer { Name = "AdvancedPlayback", Title = "高级播放设置", Folded = true };
        var fields = new VBoxContainer();
        fields.AddThemeConstantOverride("separation", 6);
        advanced.AddChild(fields);
        fields.AddChild(GraphEditorUi.BuildSpinRow("播放优先级", state.AnimationPriority, -1000, 1000, 1,
            value => { state.AnimationPriority = (int)value; Changed(); }));
        fields.AddChild(Hint("多个播放请求同时存在时，优先级较高的动画生效。普通移动状态保持 0 即可。"));
        fields.AddChild(GraphEditorUi.BuildCheckRow("从末尾开始", state.FromEnd, value => { state.FromEnd = value; Changed(); }));
        fields.AddChild(GraphEditorUi.BuildCheckRow("切入时重播同名动画", state.RestartIfPlaying, value => { state.RestartIfPlaying = value; Changed(); }));
        fields.AddChild(GraphEditorUi.BuildLineEditRow("请求标识覆盖", state.RequestKey, "留空：自动按状态生成", value => { state.RequestKey = value; Changed(); }));
        fields.AddChild(Hint("请求标识（RequestKey）用于更新和清除本状态的播放请求，不是动画名。通常留空；只有需要多个状态共用同一请求时才填写。"));
        fields.AddChild(GraphEditorUi.BuildLineEditRow("行为绑定", state.BehaviourKey, "可选：自定义状态行为", value => { state.BehaviourKey = value; Changed(); }));
        fields.AddChild(new HSeparator());
        fields.AddChild(new Label { Text = "离线配置 / 手动动画名" });
        _manualAnimation = new LineEdit { Name = "ManualAnimation", Text = state.AnimationName, PlaceholderText = "未绑定角色时使用；留空跟随状态名" };
        _manualAnimation.TextChanged += value => { state.AnimationName = value; Changed(); };
        fields.AddChild(_manualAnimation);
        fields.AddChild(Hint("留空时播放与状态名称完全相同的动画（区分大小写）。绑定角色后建议直接从动画列表选择。"));
        AddChild(advanced);
        RefreshLibrary();
    }

    public override void _Process(double delta)
    {
        _refreshTime += delta;
        if (_state == null || _refreshTime < 0.35) return;
        _refreshTime = 0;
        RefreshLibrary();
    }

    private static Label Hint(string text) => new() { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };

    private void Changed()
    {
        _context.CurrentGraph?.MarkDirty();
        RefreshLibrary();
        if (_canvasRefreshQueued) return;
        _canvasRefreshQueued = true;
        Callable.From(() =>
        {
            _canvasRefreshQueued = false;
            if (!GodotObject.IsInstanceValid(this) || IsQueuedForDeletion() || !IsInsideTree() ||
                !GodotObject.IsInstanceValid(_context.GraphNode) || !_context.GraphNode.IsInsideTree() ||
                _context.CurrentGraph?.FindNodeById(_state.Id) != _state) return;
            GraphNodeViewBuilder.RefreshNodeUI(_state, _context.GraphNode, _context);
        }).CallDeferred();
    }

    private void RefreshLibrary()
    {
        SpriteFrames frames = ResolveFrames(_context, out string source);
        string animation = string.IsNullOrWhiteSpace(_state.AnimationName) ? _state.StateName : _state.AnimationName;
        _animationButton.Text = string.IsNullOrWhiteSpace(animation) ? "选择动画…" : animation;
        _animationButton.Disabled = frames == null || frames.GetAnimationNames().Length == 0;
        _animationButton.TooltipText = "从当前角色的 SpriteFrames 中搜索并选择动画";
        bool valid = frames != null && frames.HasAnimation(animation);
        _animationInfo.Modulate = valid ? new Color(0.7f, 0.8f, 0.9f) : new Color(1f, 0.73f, 0.4f);
        if (frames == null) _animationInfo.Text = source;
        else if (frames.GetAnimationNames().Length == 0) _animationInfo.Text = source + "\n此 SpriteFrames 还没有动画，请先添加动画。";
        else if (string.IsNullOrWhiteSpace(animation)) _animationInfo.Text = source + "\n请选择要播放的动画。";
        else if (!valid) _animationInfo.Text = source + $"\n找不到动画“{animation}”，请重新选择。";
        else _animationInfo.Text = source + $"\n{Describe(frames, animation)}" +
            (string.IsNullOrWhiteSpace(_state.AnimationName) ? " · 跟随状态名" : "");
    }

    private static string Describe(SpriteFrames frames, string animation) =>
        !GodotObject.IsInstanceValid(frames) || !frames.HasAnimation(animation) ? "动画已移除" :
        $"{frames.GetFrameCount(animation)} 帧 · {frames.GetAnimationSpeed(animation):0.##} FPS · {(frames.GetAnimationLoop(animation) ? "循环" : "单次")}";

    private void OpenPicker()
    {
        SpriteFrames frames = ResolveFrames(_context, out _);
        if (frames == null) { RefreshLibrary(); return; }
        string[] names = frames.GetAnimationNames().OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        _picker = new SearchablePopup<string>(names, name => $"{name}  ·  {Describe(frames, name)}", getSearchText: name => name);
        // A popup can outlive the Inspector, or the selected host may change while it is open.
        _picker.OnItemSelected += name =>
        {
            if (!GodotObject.IsInstanceValid(this) || IsQueuedForDeletion() || !IsInsideTree() ||
                _context.CurrentGraph?.FindNodeById(_state.Id) != _state) return;
            SpriteFrames current = ResolveFrames(_context, out _);
            if (current != frames || !current.HasAnimation(name)) { RefreshLibrary(); return; }
            _state.AnimationName = name;
            _manualAnimation.Text = name;
            Changed();
        };
        _picker.ShowBelow(_animationButton);
    }

    internal static SpriteFrames ResolveFrames(GraphEditorContext context, out string description)
    {
        Node host = context.ResolveHost?.Invoke();
        description = "未绑定角色。请在左侧 Components 中选择角色，或从角色的动画组件打开此图。";
        if (!GodotObject.IsInstanceValid(host)) return null;
        var components = new List<GodotObject>();
        if (TryProperty(host, "Components", out Variant exported) && exported.VariantType == Variant.Type.Array)
            foreach (Variant item in exported.AsGodotArray())
            {
                GodotObject component = item.AsGodotObject();
                if (!GodotObject.IsInstanceValid(component)) continue;
                Script script = component.GetScript().AsGodotObject() as Script;
                if (component is CharacterAnimationComponent2D || script?.ResourcePath == "res://scripts/gamelogic/character/animation/CharacterAnimationComponent2D.cs")
                    components.Add(component);
            }
        GodotObject source = context.ResolveSource?.Invoke();
        GodotObject selected = components.FirstOrDefault(component => component == source);
        GraphAsset graph = context.RootGraph ?? context.CurrentGraph;
        var matching = components.Where(component => TryProperty(component, "LocomotionGraph", out Variant value) &&
            value.AsGodotObject() is GraphAsset candidate && (candidate == graph ||
            (!string.IsNullOrEmpty(candidate.ResourcePath) && candidate.ResourcePath == graph?.ResourcePath))).ToList();
        selected ??= matching.Count == 1 ? matching[0] : components.Count == 1 ? components[0] : null;
        if (selected == null)
        {
            description = components.Count == 0 ? "当前角色没有动画组件。请添加 CharacterAnimationComponent2D。" : "此角色有多个动画组件，请从需要编辑的动画组件打开此图。";
            return null;
        }
        if (!TryProperty(selected, "SpritePath", out Variant path)) { description = "动画组件没有配置 SpritePath。"; return null; }
        var sprite = host.GetNodeOrNull<AnimatedSprite2D>(path.AsNodePath());
        if (sprite == null) { description = $"找不到动画节点：{path.AsNodePath()}。请检查动画组件的 SpritePath。"; return null; }
        description = $"动画来源：{host.Name} / {sprite.Name}";
        if (sprite.SpriteFrames == null) description += "\n请为该节点指定 SpriteFrames。";
        return sprite.SpriteFrames;
    }

    private static bool TryProperty(GodotObject target, string name, out Variant value)
    {
        value = default;
        foreach (var property in target.GetPropertyList())
            if (property["name"].AsString() == name) { value = target.Get(name); return true; }
        return false;
    }
}
#endif
