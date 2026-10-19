using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLogic;

public enum CharacterValueType { Unknown, Boolean, Number, Symbol }
public readonly record struct CharacterValue(CharacterValueType Type, double Number = 0, string Symbol = "")
{
    public static CharacterValue Bool(bool value) => new(CharacterValueType.Boolean, value ? 1 : 0);
    public static CharacterValue Numeric(double value) => double.IsFinite(value) ? new(CharacterValueType.Number, value) : default;
    public static CharacterValue Text(string value) => new(CharacterValueType.Symbol, Symbol: value ?? "");
    public bool IsTrue => Type == CharacterValueType.Boolean && Number != 0;
    public override string ToString() => Type switch { CharacterValueType.Unknown => "unknown", CharacterValueType.Boolean => IsTrue ? "true" : "false", CharacterValueType.Number => Number.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture), _ => Symbol };
}
public enum CharacterExpressionKind { Literal, Fact, Parameter, Add, Subtract, Multiply, Divide, Minimum, Maximum, Clamp01, Equal, NotEqual, Greater, AtLeast, Less, AtMost, All, Any, Not, Known }

/// <summary>Frozen, pure expression. Unknown remains unknown through boolean negation.</summary>
public sealed class CharacterExpression
{
    public CharacterExpressionKind Kind { get; }
    public string Key { get; }
    public CharacterValue Literal { get; }
    public IReadOnlyList<CharacterExpression> Inputs { get; }
    public CharacterExpression(CharacterExpressionKind kind, string key, CharacterValue literal, IEnumerable<CharacterExpression> inputs)
        => (Kind,Key,Literal,Inputs)=(kind,key ?? "",literal,Array.AsReadOnly(inputs.ToArray()));
    public CharacterValue Evaluate(Func<string,CharacterValue> facts, Func<string,CharacterValue> parameters = null)
    {
        if(Kind==CharacterExpressionKind.Literal)return Literal;
        if(Kind==CharacterExpressionKind.Fact)return facts(Key);
        if(Kind==CharacterExpressionKind.Parameter)return parameters?.Invoke(Key) ?? default;
        var values=Inputs.Select(x=>x.Evaluate(facts,parameters)).ToArray();
        if(Kind==CharacterExpressionKind.Known)return CharacterValue.Bool(values.Length==1&&values[0].Type!=CharacterValueType.Unknown);
        if(Kind is CharacterExpressionKind.All or CharacterExpressionKind.Any)
        {
            bool all=Kind==CharacterExpressionKind.All;
            if(values.Any(v=>v.Type==CharacterValueType.Boolean && v.IsTrue!=all))return CharacterValue.Bool(!all);
            if(values.Any(v=>v.Type!=CharacterValueType.Boolean))return default;
            return CharacterValue.Bool(all);
        }
        if(Kind==CharacterExpressionKind.Not)return values.Length==1&&values[0].Type==CharacterValueType.Boolean?CharacterValue.Bool(!values[0].IsTrue):default;
        if(values.Length==0||values.Any(v=>v.Type==CharacterValueType.Unknown))return default;
        if(Kind is CharacterExpressionKind.Equal or CharacterExpressionKind.NotEqual)
        {
            if(values.Length!=2||values[0].Type!=values[1].Type)return default;
            bool equal=values[0].Type==CharacterValueType.Symbol?values[0].Symbol==values[1].Symbol:Math.Abs(values[0].Number-values[1].Number)<1e-8;
            return CharacterValue.Bool(Kind==CharacterExpressionKind.Equal?equal:!equal);
        }
        if(values.Any(v=>v.Type!=CharacterValueType.Number))return default;
        double a=values[0].Number,b=values.Length>1?values[1].Number:0;
        return Kind switch
        {
            CharacterExpressionKind.Add=>CharacterValue.Numeric(values.Sum(v=>v.Number)),
            CharacterExpressionKind.Multiply=>CharacterValue.Numeric(values.Aggregate(1d,(x,v)=>x*v.Number)),
            CharacterExpressionKind.Subtract when values.Length==2=>CharacterValue.Numeric(a-b),
            CharacterExpressionKind.Divide when values.Length==2&&Math.Abs(b)>1e-12=>CharacterValue.Numeric(a/b),
            CharacterExpressionKind.Minimum=>CharacterValue.Numeric(values.Min(v=>v.Number)),
            CharacterExpressionKind.Maximum=>CharacterValue.Numeric(values.Max(v=>v.Number)),
            CharacterExpressionKind.Clamp01 when values.Length==1=>CharacterValue.Numeric(Math.Clamp(a,0,1)),
            CharacterExpressionKind.Greater when values.Length==2=>CharacterValue.Bool(a>b),
            CharacterExpressionKind.AtLeast when values.Length==2=>CharacterValue.Bool(a+1e-8>=b),
            CharacterExpressionKind.Less when values.Length==2=>CharacterValue.Bool(a<b),
            CharacterExpressionKind.AtMost when values.Length==2=>CharacterValue.Bool(a<=b+1e-8),
            _=>default
        };
    }
    public IEnumerable<string> FactKeys()=> (Kind==CharacterExpressionKind.Fact?new[]{Key}:Array.Empty<string>()).Concat(Inputs.SelectMany(x=>x.FactKeys()));
}
