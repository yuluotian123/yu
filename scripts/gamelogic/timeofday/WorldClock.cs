using System;

namespace GameLogic;

public enum MoonPhase { NewMoon, WaxingCrescent, FirstQuarter, WaxingGibbous, FullMoon, WaningGibbous, LastQuarter, WaningCrescent }

public readonly record struct WorldTimeState(int Day, double Hour, double LunarPhase, double MoonIllumination, MoonPhase Phase)
{
    public bool IsDay => Hour >= 6.0 && Hour < 18.0;
}

/// <summary>游戏日历。只接收游戏 delta，不依赖系统日期或 Godot 节点。</summary>
public sealed class WorldClock
{
    public const int MaxDay = 1_000_000;
    private double _totalHours = 12;
    private double _dayLength = 1200;
    private double _lunarPeriod = 29.53059;
    private double _lunarOffset = 14.265295;
    private double _speed = 1;

    public double TotalHours => _totalHours;
    public double DayLengthSeconds { get => _dayLength; set => _dayLength = InRange(value, 1, 864000, nameof(DayLengthSeconds)); }
    public double LunarPeriodDays { get => _lunarPeriod; set => _lunarPeriod = InRange(value, 1, 10000, nameof(LunarPeriodDays)); }
    public double Speed { get => _speed; set => _speed = InRange(value, 0, 10000, nameof(Speed)); }
    public bool Paused { get; set; }
    public double LunarOffsetDays => _lunarOffset;
    public event Action<WorldTimeState> Changed;
    public event Action<int> DayChanged;
    public event Action<MoonPhase> MoonPhaseChanged;

    public WorldTimeState State
    {
        get
        {
            int day = (int)Math.Floor(_totalHours / 24);
            double phase = Wrap(_totalHours / 24 + _lunarOffset, _lunarPeriod) / _lunarPeriod;
            return new(day, _totalHours - day * 24, phase,
                (1 - Math.Cos(phase * Math.Tau)) * 0.5,
                (MoonPhase)((int)Math.Floor(phase * 8 + 0.5) % 8));
        }
    }

    public void Tick(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (Paused || Speed == 0 || seconds == 0) return;
        MoveTo(Math.Clamp(_totalHours + seconds / DayLengthSeconds * 24 * Speed, 0, MaxDay * 24), false);
    }

    public void SetDate(int day, double hour)
    {
        if (day < 0 || day > MaxDay) throw new ArgumentOutOfRangeException(nameof(day));
        InRange(hour, 0, 24, nameof(hour));
        double total = day * 24d + hour;
        if (total > MaxDay * 24) throw new ArgumentOutOfRangeException(nameof(hour));
        MoveTo(total, true);
    }

    public void AdvanceHours(double hours)
    {
        if (!double.IsFinite(hours)) throw new ArgumentOutOfRangeException(nameof(hours));
        MoveTo(Math.Clamp(_totalHours + hours, 0, MaxDay * 24), true);
    }

    public void SetLunarPhase(double phase)
    {
        InRange(phase, 0, 1, nameof(phase));
        var before = State;
        _lunarOffset = Wrap(phase * _lunarPeriod - _totalHours / 24, _lunarPeriod);
        Notify(before, true);
    }

    public void Restore(double totalHours, double dayLength, double lunarPeriod, double lunarOffset, double speed, bool paused)
    {
        // 全部验证后再提交，坏存档不能留下半恢复状态。
        InRange(totalHours, 0, MaxDay * 24, nameof(totalHours));
        InRange(dayLength, 1, 864000, nameof(dayLength));
        InRange(lunarPeriod, 1, 10000, nameof(lunarPeriod));
        InRange(lunarOffset, 0, lunarPeriod, nameof(lunarOffset));
        InRange(speed, 0, 10000, nameof(speed));
        var before = State;
        _totalHours = totalHours;
        _dayLength = dayLength;
        _lunarPeriod = lunarPeriod;
        _lunarOffset = lunarOffset;
        _speed = speed;
        Paused = paused;
        Notify(before, true);
    }

    private void MoveTo(double hours, bool force)
    {
        var before = State;
        _totalHours = hours;
        Notify(before, force);
    }

    private void Notify(WorldTimeState before, bool force)
    {
        var after = State;
        if (after.Day != before.Day) DayChanged?.Invoke(after.Day);
        if (after.Phase != before.Phase) MoonPhaseChanged?.Invoke(after.Phase);
        // 快进只报告最终日期，不为跳过的每一分钟分配事件。
        if (force || before.Day != after.Day || (int)(before.Hour * 60) != (int)(after.Hour * 60)) Changed?.Invoke(after);
    }

    private static double Wrap(double value, double period) => ((value % period) + period) % period;
    private static double InRange(double value, double min, double max, string name) =>
        double.IsFinite(value) && value >= min && value <= max ? value : throw new ArgumentOutOfRangeException(name);
}
