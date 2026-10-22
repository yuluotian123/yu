using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.AI;
using Godot;

namespace GameLogic;

/// <summary>Scene-authored NPC controls and diagnostics, refreshed only while visible.</summary>
[Tool, GlobalClass]
public partial class NpcGmComponent3D : Component3D
{
    public override int Priority => ComponentPriority.VFX;
    Control _page;
    Label _summary, _feedback;
    TabContainer _tabs;
    OptionButton _actors, _needs, _points, _rain;
    SpinBox _needValue;
    Button _running, _allRunning;
    double _elapsed;
    readonly List<(GodotObject Source, StringName Signal, Callable Callback)> _bindings = new();
    AiWorldComponent3D World => AiWorldComponent3D.Find(Owner);
    AiAgentComponent3D Actor => World?.Actors.GetValueOrDefault(Selected(_actors));
    WorldInteractionComponent3D Point => World?.Points.GetValueOrDefault(Selected(_points));
    T Find<T>(string name) where T : Node => Owner.GetNodeOrNull<T>("%" + name);
    static string Selected(OptionButton menu) => menu?.Selected >= 0 ? menu.GetItemMetadata(menu.Selected).AsString() : "";
    static string Name(AiAgentComponent3D a) => a.Character?.Profile?.Identity?.DisplayName is { Length: > 0 } name ? name : a.ActorId;

    public override void OnInit()
    {
        _page = Find<Control>("NpcPage"); _summary = Find<Label>("NpcSummary"); _feedback = Find<Label>("NpcFeedback");
        _tabs = Find<TabContainer>("NpcInspector");
        _actors = Find<OptionButton>("NpcActor"); _needs = Find<OptionButton>("NpcNeed");
        _points = Find<OptionButton>("NpcPoint"); _rain = Find<OptionButton>("NpcRain");
        _needValue = Find<SpinBox>("NpcNeedValue");
        _running = Find<Button>("NpcRunning"); _allRunning = Find<Button>("NpcAllRunning");
        Button("NpcRunning", () => { if (Actor is {} a) a.SetRunning(!a.Running); });
        Button("NpcReplan", () => Actor?.RequestReplan());
        Button("NpcAllReplan", ReplanAll);
        Button("NpcAllRunning", () =>
        {
            if (World == null) return;
            var eligible = World.Actors.Values.Where(a => a.Program != null && a.State != null && a.IsActive).ToArray();
            bool run = !eligible.All(a => a.Running);
            foreach (var a in eligible) a.SetRunning(run);
        });
        Button("NpcApplyNeed", ApplyNeed); Button("NpcReadNeed", ReadNeed);
        Button("NpcOpenPoint", () => SetPoint(true)); Button("NpcClosePoint", () => SetPoint(false));
        Button("NpcAutoPoint", () => { Point?.Owner.GetComponent<WorldOpeningHoursComponent3D>()?.ClearOverride(); ReplanAll(); });
        Button("NpcRestock", () => { Point?.Owner.GetComponent<WorldStockComponent3D>()?.Restock(); ReplanAll(); });
        Button("NpcCopy", () => DisplayServer.ClipboardSet(BuildSnapshot()));
        Bind(_actors, OptionButton.SignalName.ItemSelected, Callable.From<long>(_ => { Refresh(); ReadNeed(); }));
        Bind(_needs, OptionButton.SignalName.ItemSelected, Callable.From<long>(_ => ReadNeed()));
        Bind(_points, OptionButton.SignalName.ItemSelected, Callable.From<long>(_ => Refresh()));
        Bind(_tabs, TabContainer.SignalName.TabChanged, Callable.From<long>(_ => Refresh()));
        Bind(_page, CanvasItem.SignalName.VisibilityChanged, Callable.From(() => { if (_page.IsVisibleInTree()) Refresh(); }));
        Bind(_rain, OptionButton.SignalName.ItemSelected, Callable.From<long>(index =>
        {
            if (World == null) return;
            World.RainOverride = index == 0 ? null : index == 1 ? 0 : 1;
            ReplanAll(); Refresh();
        }));
        Refresh();
    }
    void Bind(GodotObject source, StringName signal, Callable callback)
    {
        if (source == null) return;
        source.Connect(signal, callback); _bindings.Add((source, signal, callback));
    }
    void Button(string name, Action action) => Bind(Find<Button>(name), Godot.Button.SignalName.Pressed, Callable.From(() =>
    {
        try { action(); _feedback.Text = "已执行：" + Find<Button>(name).Text; }
        catch (Exception error) { _feedback.Text = "操作失败：" + error.Message; }
        Refresh();
    }));
    public override void OnUpdate(double delta)
    {
        if (_page?.IsVisibleInTree() != true) return;
        _elapsed += delta;
        if (_elapsed < .3) return;
        _elapsed = 0; Refresh();
    }
    static void Menu(OptionButton menu, IEnumerable<(string Id, string Label)> source)
    {
        var items = source.ToArray(); string selected = Selected(menu);
        if (menu.ItemCount == items.Length && items.Select((x, i) => menu.GetItemMetadata(i).AsString() == x.Id).All(x => x)) return;
        menu.Clear();
        foreach (var item in items) { menu.AddItem(item.Label); menu.SetItemMetadata(menu.ItemCount - 1, item.Id); }
        int index = Array.FindIndex(items, x => x.Id == selected);
        if (items.Length > 0) menu.Select(Math.Max(0, index));
    }
    void Text(string name, string value) { if (Find<RichTextLabel>(name) is {} label) label.Text = value; }
    void ReplanAll() { if (World != null) foreach (var a in World.Actors.Values.Where(a => a.Program != null)) a.RequestReplan(); }
    void SetPoint(bool open) { Point?.Owner.GetComponent<WorldOpeningHoursComponent3D>()?.SetOpen(open); ReplanAll(); }
    void Refresh()
    {
        if (_summary == null) return;
        var w = World;
        var actors = w?.Actors.Values.OrderBy(a => a.ActorId, StringComparer.Ordinal).ToArray() ?? Array.Empty<AiAgentComponent3D>();
        Menu(_actors, actors.Select(a => (a.ActorId, Name(a) + " · " + a.ActorId)));
        Menu(_points, w?.Points.Values.OrderBy(p => p.PointId, StringComparer.Ordinal).Select(p => (p.PointId, string.IsNullOrWhiteSpace(p.DisplayName) ? p.PointId : p.DisplayName + " · " + p.PointId)) ?? Enumerable.Empty<(string, string)>());
        var a = Actor;
        string oldNeed = Selected(_needs);
        Menu(_needs, a?.Program?.Needs.Select(n => (n.Id, NeedName(a, n.Id))) ?? Enumerable.Empty<(string, string)>());
        if (oldNeed != Selected(_needs)) ReadNeed();
        foreach (string name in new[] { "NpcRunning", "NpcReplan", "NpcApplyNeed", "NpcReadNeed" }) Find<Button>(name).Disabled = a?.Program == null;
        Find<Button>("NpcCopy").Disabled = a == null;
        _needValue.Editable = a?.State != null && _needs.ItemCount > 0;
        _running.Disabled |= a?.IsActive != true;
        foreach (string name in new[] { "NpcOpenPoint", "NpcClosePoint", "NpcAutoPoint" }) Find<Button>(name).Disabled = Point?.Owner.GetComponent<WorldOpeningHoursComponent3D>()==null;
        Find<Button>("NpcRestock").Disabled=Point?.Owner.GetComponent<WorldStockComponent3D>()==null;
        var eligible = actors.Where(x => x.Program != null && x.State != null && x.IsActive).ToArray();
        _allRunning.Disabled = eligible.Length == 0; Find<Button>("NpcAllReplan").Disabled = eligible.Length == 0;
        _rain.Disabled = w == null;
        _rain.Select(w?.RainOverride == null ? 0 : w.RainOverride <= .05f ? 1 : 2);
        _allRunning.Text = eligible.Length > 0 && eligible.All(x => x.Running) ? "暂停全部 AI" : "恢复全部 AI";
        _running.Text = a?.Running == true ? "暂停此 NPC" : "恢复此 NPC";
        _summary.Text = w == null ? "当前场景没有 NPC 世界。"
            : $"NPC {actors.Length}  ·  运行 {eligible.Count(x => x.Running)}  ·  暂停 {eligible.Count(x => !x.Running)}  ·  待机 {actors.Count(x => x.RuntimeStatus == "待机")}  ·  交互点 {w.Points.Count}  ·  规划 {w.LastPlanningMilliseconds:0.000} ms\n"
            + $"游戏时间 {Math.Floor(w.Clock.TotalHours % 24):00}:{Math.Floor(w.Clock.TotalHours % 1 * 60):00}  ·  时钟{(w.Clock.Paused ? "已暂停（需求不增长）" : "推进中")}  ·  AI 降雨 {w.RainAmount:0.00}  ·  世界{(!w.IsActive ? "已停用" : Owner.GetTree().Paused ? "游戏已暂停" : "更新中")}";
        if (a == null)
        {
            foreach (string name in new[] { "NpcDetails", "NpcGoals", "NpcState", "NpcTrace" }) Text(name, "没有可检查的 NPC。请确认场景配置了 AiAgent、CharacterData 与 NPC 世界。");
            Text("NpcPoints", "选择 NPC 后查看交互点可用性。"); return;
        }
        RefreshOverview(a);
        // Path queries and full text formatting run only for the active page.
        switch (_tabs.CurrentTab)
        {
            case 1: Text("NpcGoals", Goals(a)); break;
            case 2: Text("NpcState", State(a)); break;
            case 3: Text("NpcPoints", Points(a)); break;
            case 4: Text("NpcTrace", "最近 128 条事件 · 最新在前 · 时间为世界运行秒数\n\n" + string.Join("\n", a.Trace.Reverse().Select(TraceLine))); break;
        }
    }
    internal string BuildSnapshot() => Actor is {} a ? _summary.Text + "\n\n" + Find<RichTextLabel>("NpcDetails").Text + "\n\n" + Goals(a) + "\n\n" + State(a) + "\n\n" + Points(a) + "\n\n" + string.Join("\n", a.Trace) : "无 NPC。";
    void ReadNeed()
    {
        var n = Actor?.State?.Profile.Needs.FirstOrDefault(x => x.Id == Selected(_needs));
        _needValue.Editable = n != null;
        if (n == null) return;
        _needValue.MinValue = n.Min; _needValue.MaxValue = n.Max;
        _needValue.Step = (n.Max - n.Min) / 100;
        _needValue.Value = Actor.State.Needs.Values[n.Id];
    }
    void ApplyNeed()
    {
        var a = Actor;
        if (a?.State == null || !a.State.Needs.Values.ContainsKey(Selected(_needs))) return;
        a.State.Apply(new[] { new KeyValuePair<string, CharacterValue>("needs." + Selected(_needs) + ".value", CharacterValue.Numeric(_needValue.Value)) });
        a.Decision.Refresh(); a.RequestReplan();
    }
    public override void OnDestroy()
    {
        foreach (var (source, signal, callback) in _bindings)
            if (GodotObject.IsInstanceValid(source) && source.IsConnected(signal, callback)) source.Disconnect(signal, callback);
        _bindings.Clear();
    }
}
