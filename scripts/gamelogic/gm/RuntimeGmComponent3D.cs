using System;
using System.Collections.Generic;
using System.Text.Json;
using Framework;
using Godot;

namespace GameLogic;

/// <summary>统一 GM 外壳。各调试页由场景中的控件和组件组成。</summary>
[GlobalClass]
public partial class RuntimeGmComponent3D : Component3D
{
    public override int Priority => ComponentPriority.VFX + 1;
    [Export] public bool StartOpen { get; set; }
    private IInputModule _input;
    private ISaveModule _saves;
    private Control _panel;
    private Button _toggle;
    private TextEdit _json;
    private LineEdit _slot;
    private OptionButton _slots;
    private Label _status, _source;
    private ConfirmationDialog _confirm;
    private string _loadedSlot, _baseline;
    private bool _dirty;
    private Action _confirmedAction;
    private double _elapsed;
    private readonly Dictionary<string, bool> _inputStates = new();
    private readonly List<(GodotObject Source, StringName Signal, Callable Callback)> _bindings = new();

    public override void OnInit()
    {
        _input = ModuleSystem.GetModule<IInputModule>();
        _saves = ModuleSystem.GetModule<ISaveModule>();
        _panel = Find<Control>("Panel");
        _toggle = Find<Button>("Toggle");
        _json = Find<TextEdit>("SaveJson");
        _slot = Find<LineEdit>("SlotName");
        _slots = Find<OptionButton>("SaveSlots");
        _status = Find<Label>("SaveStatus");
        _source = Find<Label>("SaveSource");
        _confirm = Find<ConfirmationDialog>("Confirm");
        Button("Toggle", () => ShowPanel(!_panel.Visible));
        Button("Close", () => ShowPanel(false));
        Button("RefreshSlots", () => Try(RefreshSlots));
        Button("ReadSave", () => GuardDiscard(() => ReadSlot(_slot.Text)));
        Button("ValidateSave", () => Try(() => { SaveModule.ValidateEditedDocument(_json.Text); Status("JSON 和已知存档字段校验通过。"); }));
        Button("WriteSave", () => Try(WriteEdits));
        Button("CaptureSave", RequestCapture);
        Button("DeleteSave", RequestDelete);
        Button("DumpResources", () => ModuleSystem.GetModule<IResourceModule>().DumpProfilerToLog());
        Button("CollectGc", () => { GC.Collect(); RefreshResources(); });
        Bind(_slots, OptionButton.SignalName.ItemSelected, Callable.From<long>(index => _slot.Text = _slots.GetItemText((int)index)));
        Bind(_json, TextEdit.SignalName.TextChanged, Callable.From(() => { _dirty = _json.Text != (_baseline ?? "").Replace("\r\n", "\n"); Status(_dirty ? "有未写入的修改。" : "内容未修改。"); }));
        Bind(_confirm, ConfirmationDialog.SignalName.Confirmed, Callable.From(() => { var action = _confirmedAction; _confirmedAction = null; Try(() => action?.Invoke()); }));
        Bind(_confirm, ConfirmationDialog.SignalName.Canceled, Callable.From(() => { _confirmedAction = null; }));
        var tabs = Find<TabContainer>("Tabs");
        tabs.SetTabTitle(0, "时间与天空");
        tabs.SetTabTitle(1, "资源监控");
        tabs.SetTabTitle(2, "存档");
        _panel.Visible = false;
        Owner.SetMeta("runtime_gm", this);
        Try(() => { RefreshSlots(); ReadSlot("default"); });
        ShowPanel(StartOpen);
    }

    public void ShowPanel(bool visible)
    {
        if (_panel == null || _panel.Visible == visible) return;
        _panel.Visible = visible;
        _toggle.SetPressedNoSignal(visible);
        if (visible)
        {
            foreach (string layer in new[] { "Global", "Combat", "Camera", "UI" })
            {
                _inputStates[layer] = _input.IsLayerEnabled(layer);
                _input.DisableLayer(layer);
            }
            ModuleSystem.GetModule<IResourceModule>().SetProfilerOverlayVisible(false);
            _input.ClearBuffer();
            Try(RefreshResources);
        }
        else
        {
            Owner.GetComponent<TimeOfDayGmComponent3D>()?.EndInteraction();
            _confirm.Hide();
            _confirmedAction = null;
            RestoreInput();
            Owner.GetViewport().GuiReleaseFocus();
        }
    }

    public override void OnUpdate(double delta)
    {
        if (_panel == null || !_panel.Visible) return;
        _elapsed += delta;
        if (_elapsed < .5) return;
        _elapsed = 0;
        if (Find<TabContainer>("Tabs").CurrentTab == 1) RefreshResources();
    }

    private void RefreshResources()
    {
        Find<RichTextLabel>("ResourcesText").Text =
            ResourceProfilerOverlay.FormatSnapshot(ModuleSystem.GetModule<IResourceModule>().GetProfilerSnapshot(), 100);
    }

    private void RefreshSlots()
    {
        _slots.Clear();
        foreach (string slot in _saves.ListSlots()) _slots.AddItem(slot);
    }

    private void ReadSlot(string slot)
    {
        string json = _saves.ReadSlot(slot, out string path);
        _loadedSlot = string.IsNullOrWhiteSpace(slot) ? "default" : slot.Trim();
        _slot.Text = _loadedSlot;
        _baseline = json;
        _json.Text = json ?? "";
        _dirty = false;
        _source.Text = $"正在编辑：{_loadedSlot}  |  {path}";
        Status(json == null ? "该槽位没有存档。可将当前游戏状态存入此槽位。" : "已读取。修改只写入文件，下次进入关卡时生效。");
    }

    private void WriteEdits()
    {
        if (_loadedSlot == null) throw new InvalidOperationException("请先读取一个存档槽位。");
        _saves.WriteSlot(_loadedSlot, _json.Text, _baseline);
        ReadSlot(_loadedSlot);
        RefreshSlots();
        Status("修改已写入；原有效存档保留为 .bak。下次进入关卡生效。");
    }

    private void RequestCapture() => Try(() =>
    {
        string slot = _slot.Text;
        string expected = _saves.ReadSlot(slot, out _);
        Confirm($"将当前运行状态保存到「{slot}」？\n已有文件和编辑区中的未写入修改将被替换。", () =>
        {
            if (_saves.ReadSlot(slot, out _) != expected) throw new InvalidOperationException("存档已改变，请重新读取。");
            _saves.Save(slot);
            ReadSlot(slot);
            RefreshSlots();
            Status("当前运行状态已保存。");
        });
    });

    private void RequestDelete() => Try(() =>
    {
        if (_loadedSlot == null || _baseline == null) throw new InvalidOperationException("请先读取要删除的存档。");
        string slot = _loadedSlot, expected = _baseline;
        Confirm($"删除存档「{slot}」及其 .bak 备份？\n当前游戏状态保留，下次进入关卡将不再读取此存档。", () =>
        {
            _saves.DeleteSlot(slot, expected);
            ReadSlot(slot);
            RefreshSlots();
            Status("存档已删除。当前游戏状态未改变；下次进入关卡使用初始状态。");
        });
    });

    private void GuardDiscard(Action action)
    {
        if (_dirty) Confirm("放弃编辑区中尚未写入的修改，并重新读取？", action);
        else Try(action);
    }
    private void Confirm(string message, Action action)
    {
        _confirmedAction = action;
        _confirm.DialogText = message;
        _confirm.PopupCentered();
    }
    private void Try(Action action)
    {
        try { action(); }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException
            or FormatException or System.IO.IOException or UnauthorizedAccessException or OverflowException)
        { Status("操作失败：" + error.Message); }
    }
    private void Status(string message) => _status.Text = message;
    private T Find<T>(string name) where T : Node => Owner.GetNode<T>("%" + name);
    private void Button(string name, Action action) => Bind(Find<Button>(name), BaseButton.SignalName.Pressed, Callable.From(action));
    private void Bind(GodotObject source, StringName signal, Callable callback)
    {
        source.Connect(signal, callback);
        _bindings.Add((source, signal, callback));
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
        RestoreInput();
        foreach (var binding in _bindings)
            if (GodotObject.IsInstanceValid(binding.Source) && binding.Source.IsConnected(binding.Signal, binding.Callback))
                binding.Source.Disconnect(binding.Signal, binding.Callback);
        _bindings.Clear();
        _confirmedAction = null;
        if (GodotObject.IsInstanceValid(Owner)) Owner.RemoveMeta("runtime_gm");
    }
}
