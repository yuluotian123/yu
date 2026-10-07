using System;
using System.Collections.Generic;
using Framework;
using Godot;

namespace GameLogic;

/// <summary>绑定场景内的 GM 控件，直接操作当前环境的世界时钟。</summary>
[GlobalClass]
public partial class TimeOfDayGmComponent3D : Component3D
{
    public override int Priority => ComponentPriority.VFX;
    [Export] public NodePath EnvironmentRigPath { get; set; } = new("../EnvironmentRig");
    [Export] public bool Embedded { get; set; }
    [Export] public bool StartOpen { get; set; }
    [Export] public bool BlockGameplayInput { get; set; } = true;

    private DayNightEnvironmentComponent3D _environment;
    private WorldClock _clock;
    private IInputModule _input;
    private Control _panel;
    private Button _toggle;
    private Label _clockLabel, _moonLabel;
    private HSlider _timeSlider, _moonSlider;
    private SpinBox _day, _hour, _minute, _speed;
    private CheckButton _pause;
    private OptionButton _phase;
    private readonly List<(GodotObject Source, StringName Signal, Callable Callback)> _connections = new();
    private readonly Dictionary<string, bool> _inputStates = new();
    private bool _scrubbing, _resumeAfterScrub;
    private double _elapsed;

    public override void OnInit()
    {
        _environment = Owner.GetNodeOrNull<GameObject3D>(EnvironmentRigPath)?.GetComponent<DayNightEnvironmentComponent3D>();
        if (_environment == null)
        {
            if (!Embedded)
            {
                GD.PushError("[TimeOfDayGM] EnvironmentRigPath must point to a day/night EC host.");
                Owner.GetNode<CanvasLayer>("CanvasLayer").Visible = false;
                return;
            }
        }
        _clock = ModuleSystem.GetModule<ITimeOfDayModule>().Clock;
        _input = ModuleSystem.GetModule<IInputModule>();
        _panel = Find<Control>("Panel");
        _toggle = Find<Button>("Toggle");
        _clockLabel = Find<Label>("ClockLabel");
        _moonLabel = Find<Label>("MoonLabel");
        _timeSlider = Find<HSlider>("TimeSlider");
        _moonSlider = Find<HSlider>("MoonSlider");
        _day = Find<SpinBox>("Day");
        _hour = Find<SpinBox>("Hour");
        _minute = Find<SpinBox>("Minute");
        _speed = Find<SpinBox>("Speed");
        _pause = Find<CheckButton>("Pause");
        _phase = Find<OptionButton>("Phase");
        if (!Embedded)
        {
            _panel.Visible = false;
            BindButton("Toggle", TogglePanel);
            BindButton("Close", () => ShowPanel(false));
        }
        BindButton("Dawn", () => SetHour(6));
        BindButton("Noon", () => SetHour(12));
        BindButton("Dusk", () => SetHour(18));
        BindButton("Midnight", () => SetHour(0));
        Bind(_timeSlider, Godot.Range.SignalName.ValueChanged, Callable.From<double>(value => SetHour(value / 60)));
        Bind(_timeSlider, Slider.SignalName.DragStarted, Callable.From(BeginScrub));
        Bind(_timeSlider, Slider.SignalName.DragEnded, Callable.From<bool>(_ => EndScrub()));
        Bind(_day, Godot.Range.SignalName.ValueChanged, Callable.From<double>(value => SetDate((int)value, _clock.State.Hour)));
        Bind(_hour, Godot.Range.SignalName.ValueChanged, Callable.From<double>(_ => SetHour(_hour.Value + _minute.Value / 60)));
        Bind(_minute, Godot.Range.SignalName.ValueChanged, Callable.From<double>(_ => SetHour(_hour.Value + _minute.Value / 60)));
        Bind(_speed, Godot.Range.SignalName.ValueChanged, Callable.From<double>(value => { _clock.Speed = value; Refresh(); }));
        Bind(_pause, BaseButton.SignalName.Toggled, Callable.From<bool>(value => { _clock.Paused = value; Refresh(); }));
        Bind(_moonSlider, Godot.Range.SignalName.ValueChanged, Callable.From<double>(SetMoonPhase));
        Bind(_phase, OptionButton.SignalName.ItemSelected, Callable.From<long>(index => SetMoonPhase(index / 8.0)));
        Owner.SetMeta("time_gm", this);
        if (!Embedded) ShowPanel(StartOpen);
        Refresh();
    }

    public override void OnUpdate(double delta)
    {
        if (_clock == null || !_panel.Visible) return;
        _elapsed += delta;
        if (_elapsed < .1) return;
        _elapsed = 0;
        Refresh();
    }

    public void TogglePanel() { if (_panel != null) ShowPanel(!_panel.Visible); }

    public void ShowPanel(bool visible)
    {
        if (Embedded || _panel == null || _panel.Visible == visible) return;
        _panel.Visible = visible;
        _toggle.SetPressedNoSignal(visible);
        if (visible)
        {
            if (BlockGameplayInput)
            {
                // 原生 GUI 仍正常收键；项目输入层暂时关闭，避免拖动滑条时触发攻击/镜头。
                foreach (string layer in new[] { InputLayerManager.LayerName.Global, InputLayerManager.LayerName.Combat,
                    InputLayerManager.LayerName.Camera, InputLayerManager.LayerName.UI })
                {
                    _inputStates[layer] = _input.IsLayerEnabled(layer);
                    _input.DisableLayer(layer);
                }
                _input.ClearBuffer();
            }
            Refresh();
        }
        else
        {
            EndScrub();
            RestoreInput();
            Owner.GetViewport().GuiReleaseFocus();
        }
    }

    private void SetHour(double hour) => SetDate(_clock.State.Day, hour);
    private void SetDate(int day, double hour)
    {
        if (_environment != null) _environment.SetDate(day, day == WorldClock.MaxDay ? 0 : hour);
        else _clock.SetDate(day, day == WorldClock.MaxDay ? 0 : hour);
        Refresh();
    }
    private void SetMoonPhase(double phase) { if (_environment != null) _environment.SetLunarPhase(phase); else _clock.SetLunarPhase(phase); Refresh(); }
    private void BeginScrub()
    {
        _scrubbing = true;
        _resumeAfterScrub = !_clock.Paused;
        _clock.Paused = true;
    }
    internal void EndInteraction() => EndScrub();
    private void EndScrub()
    {
        if (!_scrubbing) return;
        _scrubbing = false;
        _clock.Paused = !_resumeAfterScrub;
        Refresh();
    }

    private void Refresh()
    {
        var state = _clock.State;
        int minutes = Math.Min(1439, (int)Math.Floor(state.Hour * 60 + 1e-7));
        _clockLabel.Text = $"第 {state.Day} 天   {minutes / 60:00}:{minutes % 60:00}";
        if (!_scrubbing) _timeSlider.SetValueNoSignal(minutes);
        Sync(_day, state.Day);
        Sync(_hour, minutes / 60);
        Sync(_minute, minutes % 60);
        Sync(_speed, _clock.Speed);
        _pause.SetPressedNoSignal(_clock.Paused);
        _moonSlider.SetValueNoSignal(state.LunarPhase);
        _phase.Select((int)state.Phase);
        _moonLabel.Text = $"月相 {state.LunarPhase:P1}  ·  受光 {state.MoonIllumination:P0}";
    }

    private static void Sync(SpinBox field, double value)
    {
        if (!field.GetLineEdit().HasFocus()) field.SetValueNoSignal(value);
    }
    private T Find<T>(string name) where T : Node => Owner.GetNode<T>("%" + name);
    private void BindButton(string name, Action callback) => Bind(Find<Button>(name), BaseButton.SignalName.Pressed, Callable.From(callback));
    private void Bind(GodotObject source, StringName signal, Callable callback)
    {
        source.Connect(signal, callback);
        _connections.Add((source, signal, callback));
    }
    private void RestoreInput()
    {
        foreach (var entry in _inputStates)
            if (entry.Value) _input.EnableLayer(entry.Key);
        _inputStates.Clear();
        _input?.ClearBuffer();
    }
    public override void OnDestroy()
    {
        // 子节点可能已离树；恢复时钟和输入无需访问已销毁的 GUI。
        if (_scrubbing && _clock != null) _clock.Paused = !_resumeAfterScrub;
        _scrubbing = false;
        RestoreInput();
        foreach (var binding in _connections)
            if (GodotObject.IsInstanceValid(binding.Source) && binding.Source.IsConnected(binding.Signal, binding.Callback))
                binding.Source.Disconnect(binding.Signal, binding.Callback);
        _connections.Clear();
        if (GodotObject.IsInstanceValid(Owner)) Owner.RemoveMeta("time_gm");
        _clock = null;
        _environment = null;
        _input = null;
        _panel = null;
    }
}
