using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Framework;
using Godot;

namespace GameLogic;

/// <summary>全局日历与存档；没有环境宿主或游戏暂停时不推进时间。</summary>
public sealed class TimeOfDayModule : Module, IProcessModule, ITimeOfDayModule, ISaveSection
{
    private readonly HashSet<Node> _owners = new();
    private ISaveModule _saves;
    private bool _hasCalendar;
    public WorldClock Clock { get; } = new();
    public bool HasEnvironment => _owners.Count > 0;
    public override int Priority => 20;
    public string SectionKey => "world";
    public string EntryKey => "time_of_day";
    public int SchemaVersion => 1;

    public override void OnInit()
    {
        _saves = ModuleSystem.GetModule<ISaveModule>();
        _saves.RegisterSection(this);
    }

    public void Attach(Node owner, TimeOfDayProfile profile)
    {
        if (!GodotObject.IsInstanceValid(owner) || !owner.IsInsideTree())
            throw new ArgumentException("A time-of-day owner must be in the scene tree.", nameof(owner));
        ArgumentNullException.ThrowIfNull(profile);
        if (!_hasCalendar)
        {
            Clock.DayLengthSeconds = profile.DayLengthSeconds;
            Clock.LunarPeriodDays = profile.LunarPeriodDays;
            Clock.SetDate(profile.StartDay, profile.StartHour);
            Clock.SetLunarPhase(profile.StartLunarPhase);
            Clock.Paused = profile.StartPaused;
            _hasCalendar = true;
        }
        _owners.Add(owner);
    }

    public void Detach(Node owner) { if (owner != null) _owners.Remove(owner); }

    public void Process(double elapseSeconds, double realElapseSeconds)
    {
        if (Engine.GetMainLoop() is not SceneTree tree || tree.Paused) return;
        _owners.RemoveWhere(n => !GodotObject.IsInstanceValid(n) || !n.IsInsideTree() || n.IsQueuedForDeletion());
        if (HasEnvironment) Clock.Tick(elapseSeconds);
    }

    public JsonObject Capture() => new()
    {
        ["initialized"] = _hasCalendar,
        ["total_hours"] = Clock.TotalHours,
        ["day_length_seconds"] = Clock.DayLengthSeconds,
        ["lunar_period_days"] = Clock.LunarPeriodDays,
        ["lunar_offset_days"] = Clock.LunarOffsetDays,
        ["speed"] = Clock.Speed,
        ["paused"] = Clock.Paused
    };

    public void Restore(JsonObject state, int schemaVersion)
    {
        if (state == null || schemaVersion != SchemaVersion) return;
        if (state["initialized"] is JsonValue initialized && initialized.TryGetValue<bool>(out bool valid) && !valid) return;
        try
        {
            Clock.Restore(state["total_hours"].GetValue<double>(), state["day_length_seconds"].GetValue<double>(),
                state["lunar_period_days"].GetValue<double>(), state["lunar_offset_days"].GetValue<double>(),
                state["speed"].GetValue<double>(), state["paused"].GetValue<bool>());
            _hasCalendar = true;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or FormatException or NullReferenceException)
        {
            GD.PushWarning($"[TimeOfDayModule] Invalid saved calendar ignored: {e.Message}");
        }
    }

    public override void Shutdown()
    {
        _saves?.UnregisterSection(this);
        _saves = null;
        _owners.Clear();
    }
}
