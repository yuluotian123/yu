using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GameLogic.Goap;

public enum GoapValueKind { Unknown, Boolean, Integer, Symbol }

/// <summary>Unknown is distinct from false and zero. Symbols use ordinal identity.</summary>
public readonly record struct GoapValue
{
    public GoapValueKind Kind { get; }
    public long Integer { get; }
    public string Symbol { get; }
    private GoapValue(GoapValueKind kind, long integer = 0, string symbol = null)
        => (Kind, Integer, Symbol) = (kind, integer, symbol);
    public static GoapValue Bool(bool value) => new(GoapValueKind.Boolean, value ? 1 : 0);
    public static GoapValue Number(long value) => new(GoapValueKind.Integer, value);
    public static GoapValue Id(string value) => new(GoapValueKind.Symbol, symbol: value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Kind switch
    {
        GoapValueKind.Boolean => Integer != 0 ? "true" : "false",
        GoapValueKind.Integer => Integer.ToString(CultureInfo.InvariantCulture),
        GoapValueKind.Symbol => Symbol,
        _ => "unknown"
    };
}

public enum GoapComparison { Equal, NotEqual, AtLeast, AtMost, Known }

public readonly record struct GoapCondition(string Key, GoapComparison Comparison, GoapValue Value)
{
    public bool IsMet(GoapWorldState state)
    {
        GoapValue actual = state[Key];
        if (Comparison == GoapComparison.Known) return actual.Kind != GoapValueKind.Unknown;
        if (actual.Kind == GoapValueKind.Unknown || actual.Kind != Value.Kind) return false;
        return Comparison switch
        {
            GoapComparison.Equal => actual == Value,
            GoapComparison.NotEqual => actual != Value,
            GoapComparison.AtLeast => actual.Kind == GoapValueKind.Integer && actual.Integer >= Value.Integer,
            GoapComparison.AtMost => actual.Kind == GoapValueKind.Integer && actual.Integer <= Value.Integer,
            _ => false
        };
    }
    public static GoapCondition Is(string key, GoapValue value) => new(key, GoapComparison.Equal, value);
    public static GoapCondition Min(string key, long value) => new(key, GoapComparison.AtLeast, GoapValue.Number(value));
}

public enum GoapEffectKind { Set, Add, Forget }

public readonly record struct GoapEffect(string Key, GoapEffectKind Kind, GoapValue Value,
    long Minimum = long.MinValue, long Maximum = long.MaxValue)
{
    public static GoapEffect Set(string key, GoapValue value) => new(key, GoapEffectKind.Set, value);
    public static GoapEffect Add(string key, long amount, long minimum = 0, long maximum = long.MaxValue)
        => new(key, GoapEffectKind.Add, GoapValue.Number(amount), minimum, maximum);
}

/// <summary>Immutable per-search state. Applying effects never mutates the source snapshot.</summary>
public sealed class GoapWorldState
{
    private readonly SortedDictionary<string, GoapValue> _facts;
    private string _key;
    public GoapWorldState(IEnumerable<KeyValuePair<string, GoapValue>> facts = null)
    {
        _facts = new(StringComparer.Ordinal);
        if (facts == null) return;
        foreach (var entry in facts)
        {
            if (string.IsNullOrWhiteSpace(entry.Key)) throw new ArgumentException("Fact keys must be nonempty.");
            if (entry.Value.Kind != GoapValueKind.Unknown) _facts[entry.Key] = entry.Value;
        }
    }
    public GoapValue this[string key] => _facts.TryGetValue(key, out var value) ? value : default;
    public IEnumerable<KeyValuePair<string, GoapValue>> Facts => Enumerate();
    private IEnumerable<KeyValuePair<string, GoapValue>> Enumerate()
    {
        foreach (var entry in _facts) yield return entry;
    }
    public bool Satisfies(IEnumerable<GoapCondition> conditions)
    {
        foreach (var condition in conditions) if (!condition.IsMet(this)) return false;
        return true;
    }
    public GoapWorldState With(params GoapEffect[] effects) => TryApply(effects, out var result)
        ? result : throw new InvalidOperationException("Effects violate a fact type or numeric range.");
    public bool TryApply(IEnumerable<GoapEffect> effects, out GoapWorldState result)
    {
        result = null;
        var next = new GoapWorldState(_facts);
        foreach (var effect in effects)
        {
            if (string.IsNullOrWhiteSpace(effect.Key)) return false;
            if (effect.Kind == GoapEffectKind.Forget || effect.Kind == GoapEffectKind.Set && effect.Value.Kind == GoapValueKind.Unknown)
                next._facts.Remove(effect.Key);
            else if (effect.Kind == GoapEffectKind.Set) next._facts[effect.Key] = effect.Value;
            else
            {
                GoapValue current = next[effect.Key];
                if (current.Kind != GoapValueKind.Integer || effect.Value.Kind != GoapValueKind.Integer) return false;
                long value;
                try { value = checked(current.Integer + effect.Value.Integer); }
                catch (OverflowException) { return false; }
                if (value < effect.Minimum || value > effect.Maximum) return false;
                next._facts[effect.Key] = GoapValue.Number(value);
            }
        }
        result = next;
        return true;
    }
    public string StateKey
    {
        get
        {
            if (_key != null) return _key;
            var key = new StringBuilder();
            foreach (var entry in _facts)
            {
                string value = entry.Value.ToString();
                key.Append(entry.Key.Length).Append(':').Append(entry.Key).Append((int)entry.Value.Kind)
                    .Append(':').Append(value.Length).Append(':').Append(value).Append(';');
            }
            return _key = key.ToString();
        }
    }
}
