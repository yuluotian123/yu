using System.Collections.Generic;
using Godot;

namespace GameLogic;

[GlobalClass]
public partial class WeatherGmComponent3D : Component3D
{
    public override int Priority => ComponentPriority.VFX;
    [Export] public NodePath EnvironmentRigPath { get; set; } = new("../EnvironmentRig");
    private WeatherComponent3D _weather;
    private WeatherSurfaceComponent3D _surfaces;
    private Control _page;
    private Label _status;
    private OptionButton _kind;
    private CheckButton _automatic;
    private SpinBox _intensity;
    private SpinBox _wetness, _puddles, _snow, _rippleStrength;
    private SpinBox _rainOpacity;
    private double _elapsed;
    private readonly List<(GodotObject Source, StringName Signal, Callable Callback)> _bindings = new();

    public override void OnInit()
    {
        _page = Owner.GetNode<Control>("%WeatherPage");
        _status = Owner.GetNode<Label>("%WeatherStatus");
        _kind = Owner.GetNode<OptionButton>("%WeatherKind");
        _automatic = Owner.GetNode<CheckButton>("%WeatherAutomatic");
        _intensity = Owner.GetNode<SpinBox>("%WeatherIntensity");
        _wetness = Owner.GetNode<SpinBox>("%WeatherWetness");
        _puddles = Owner.GetNode<SpinBox>("%WeatherPuddles");
        _snow = Owner.GetNode<SpinBox>("%WeatherSnow");
        _rippleStrength = Owner.GetNode<SpinBox>("%RippleStrength");
        _rainOpacity = Owner.GetNode<SpinBox>("%RainOpacity");
        _kind.Clear();
        foreach (string name in new[] { "晴天", "阴天", "雨天", "雪天" }) _kind.AddItem(name);
        Bind(_kind, OptionButton.SignalName.ItemSelected, Callable.From<long>(index => { _weather?.SetWeather((int)index); Refresh(); }));
        Bind(_automatic, BaseButton.SignalName.Toggled, Callable.From<bool>(enabled => { _weather?.SetAutomatic(enabled); Refresh(); }));
        Bind(_intensity, Godot.Range.SignalName.ValueChanged, Callable.From<double>(value =>
        {
            if (_weather?.Profile != null) _weather.Profile.Intensity = (float)value;
        }));
        Bind(_wetness, Godot.Range.SignalName.ValueChanged, Callable.From<double>(value => _surfaces?.SetSurfaceState((float)value, _surfaces.Puddles)));
        Bind(_puddles, Godot.Range.SignalName.ValueChanged, Callable.From<double>(value => _surfaces?.SetSurfaceState(_surfaces.Wetness, (float)value)));
        Bind(_snow, Godot.Range.SignalName.ValueChanged, Callable.From<double>(value => _surfaces?.SetSnowState((float)value)));
        Bind(_rippleStrength, Godot.Range.SignalName.ValueChanged, Callable.From<double>(value =>
        {
            if (_weather?.Profile != null) _weather.Profile.RippleStrength = (float)value;
        }));
        Bind(_rainOpacity, Godot.Range.SignalName.ValueChanged, Callable.From<double>(value =>
        {
            if (_weather?.Profile != null) _weather.Profile.RainOpacity = (float)value;
        }));
        ResolveWeather();
        Refresh();
    }

    public override void OnUpdate(double delta)
    {
        if (!_page.IsVisibleInTree()) return;
        _elapsed += delta;
        if (_elapsed < .1) return;
        _elapsed = 0;
        // 环境宿主回收再激活时重新绑定运行时实例。
        ResolveWeather();
        Refresh();
    }

    private void ResolveWeather()
    {
        var rig = Owner.GetNodeOrNull<GameObject3D>(EnvironmentRigPath);
        _weather = rig?.GetComponent<WeatherComponent3D>();
        _surfaces = rig?.GetComponent<WeatherSurfaceComponent3D>();
    }

    private void Refresh()
    {
        bool available = _weather?.Profile != null;
        _kind.Disabled = _automatic.Disabled = !available;
        _intensity.Editable = available;
        _rainOpacity.Editable = available;
        _rippleStrength.Editable = available;
        _wetness.Editable = _puddles.Editable = _snow.Editable = _surfaces != null;
        _wetness.SetValueNoSignal(_surfaces?.Wetness ?? 0);
        _puddles.SetValueNoSignal(_surfaces?.Puddles ?? 0);
        _snow.SetValueNoSignal(_surfaces?.Snow ?? 0);
        if (!available) { _status.Text = "此关卡没有天气组件。"; return; }
        _kind.Select((int)_weather.TargetWeather);
        _automatic.SetPressedNoSignal(_weather.Automatic);
        _intensity.SetValueNoSignal(_weather.Profile.Intensity);
        _rainOpacity.SetValueNoSignal(_weather.Profile.RainOpacity);
        _rippleStrength.SetValueNoSignal(_weather.Profile.RippleStrength);
        using var state = _weather.GetWeatherState();
        _status.Text = $"当前目标：{_kind.GetItemText((int)_weather.TargetWeather)}  ·  过渡 {_weather.TransitionProgress:P0}\n"
            + (_weather.Automatic ? $"自动天气 · 距下次抽取 {state["next_change_hours"].AsDouble():0.00} 游戏小时" : "手动覆盖 · 开启自动天气后恢复日历调度");
    }

    private void Bind(GodotObject source, StringName signal, Callable callback)
    {
        source.Connect(signal, callback);
        _bindings.Add((source, signal, callback));
    }

    public override void OnDestroy()
    {
        foreach (var binding in _bindings)
            if (GodotObject.IsInstanceValid(binding.Source) && binding.Source.IsConnected(binding.Signal, binding.Callback))
                binding.Source.Disconnect(binding.Signal, binding.Callback);
        _bindings.Clear();
        _weather = null;
        _surfaces = null;
    }
}
