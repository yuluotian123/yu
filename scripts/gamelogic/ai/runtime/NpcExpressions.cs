using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLogic.AI;

public enum NpcValueType { Unknown, Boolean, Number, Symbol }
public readonly record struct NpcValue(NpcValueType Type, double Number = 0, string Symbol = "")
{
    public static NpcValue Bool(bool value) => new(NpcValueType.Boolean, value ? 1 : 0);
    public static NpcValue Numeric(double value) => double.IsFinite(value) ? new(NpcValueType.Number, value) : default;
    public static NpcValue Text(string value) => new(NpcValueType.Symbol, Symbol: value ?? "");
    public bool IsTrue => Type == NpcValueType.Boolean && Number != 0;
    public override string ToString() => Type switch { NpcValueType.Unknown => "unknown", NpcValueType.Boolean => IsTrue ? "true" : "false", NpcValueType.Number => Number.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture), _ => Symbol };
}
public enum NpcExpressionKind { Literal, Fact, Parameter, Add, Subtract, Multiply, Divide, Minimum, Maximum, Clamp01, Equal, NotEqual, Greater, AtLeast, Less, AtMost, All, Any, Not, Known }

/// <summary>Frozen, pure expression. Unknown remains unknown through boolean negation.</summary>
public sealed class NpcExpression
{
    public NpcExpressionKind Kind { get; }
    public string Key { get; }
    public NpcValue Literal { get; }
    public IReadOnlyList<NpcExpression> Inputs { get; }
    public NpcExpression(NpcExpressionKind kind, string key, NpcValue literal, IEnumerable<NpcExpression> inputs)
        => (Kind,Key,Literal,Inputs)=(kind,key ?? "",literal,Array.AsReadOnly(inputs.ToArray()));
    public NpcValue Evaluate(Func<string,NpcValue> facts, Func<string,NpcValue> parameters = null)
    {
        if(Kind==NpcExpressionKind.Literal)return Literal;
        if(Kind==NpcExpressionKind.Fact)return facts(Key);
        if(Kind==NpcExpressionKind.Parameter)return parameters?.Invoke(Key) ?? default;
        var values=Inputs.Select(x=>x.Evaluate(facts,parameters)).ToArray();
        if(Kind==NpcExpressionKind.Known)return NpcValue.Bool(values.Length==1&&values[0].Type!=NpcValueType.Unknown);
        if(Kind is NpcExpressionKind.All or NpcExpressionKind.Any)
        {
            bool all=Kind==NpcExpressionKind.All;
            if(values.Any(v=>v.Type==NpcValueType.Boolean && v.IsTrue!=all))return NpcValue.Bool(!all);
            if(values.Any(v=>v.Type!=NpcValueType.Boolean))return default;
            return NpcValue.Bool(all);
        }
        if(Kind==NpcExpressionKind.Not)return values.Length==1&&values[0].Type==NpcValueType.Boolean?NpcValue.Bool(!values[0].IsTrue):default;
        if(values.Length==0||values.Any(v=>v.Type==NpcValueType.Unknown))return default;
        if(Kind is NpcExpressionKind.Equal or NpcExpressionKind.NotEqual)
        {
            if(values.Length!=2||values[0].Type!=values[1].Type)return default;
            bool equal=values[0].Type==NpcValueType.Symbol?values[0].Symbol==values[1].Symbol:Math.Abs(values[0].Number-values[1].Number)<1e-8;
            return NpcValue.Bool(Kind==NpcExpressionKind.Equal?equal:!equal);
        }
        if(values.Any(v=>v.Type!=NpcValueType.Number))return default;
        double a=values[0].Number,b=values.Length>1?values[1].Number:0;
        return Kind switch
        {
            NpcExpressionKind.Add=>NpcValue.Numeric(values.Sum(v=>v.Number)),
            NpcExpressionKind.Multiply=>NpcValue.Numeric(values.Aggregate(1d,(x,v)=>x*v.Number)),
            NpcExpressionKind.Subtract when values.Length==2=>NpcValue.Numeric(a-b),
            NpcExpressionKind.Divide when values.Length==2&&Math.Abs(b)>1e-12=>NpcValue.Numeric(a/b),
            NpcExpressionKind.Minimum=>NpcValue.Numeric(values.Min(v=>v.Number)),
            NpcExpressionKind.Maximum=>NpcValue.Numeric(values.Max(v=>v.Number)),
            NpcExpressionKind.Clamp01 when values.Length==1=>NpcValue.Numeric(Math.Clamp(a,0,1)),
            NpcExpressionKind.Greater when values.Length==2=>NpcValue.Bool(a>b),
            NpcExpressionKind.AtLeast when values.Length==2=>NpcValue.Bool(a+1e-8>=b),
            NpcExpressionKind.Less when values.Length==2=>NpcValue.Bool(a<b),
            NpcExpressionKind.AtMost when values.Length==2=>NpcValue.Bool(a<=b+1e-8),
            _=>default
        };
    }
    public IEnumerable<string> FactKeys()=> (Kind==NpcExpressionKind.Fact?new[]{Key}:Array.Empty<string>()).Concat(Inputs.SelectMany(x=>x.FactKeys()));
}
