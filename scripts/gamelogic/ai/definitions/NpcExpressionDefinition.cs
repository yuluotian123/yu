using System;
using System.Linq;
using Godot;

namespace GameLogic.AI;

[Tool,GlobalClass]
public partial class NpcExpressionDefinition : Resource
{
    private NpcExpressionKind _kind=NpcExpressionKind.Literal;
    private NpcValueType _valueType=NpcValueType.Number;
    [Export] public NpcExpressionKind Kind {get=>_kind;set{_kind=value;NotifyPropertyListChanged();}}
    [Export] public string Key {get;set;}="";
    [Export] public NpcValueType ValueType {get=>_valueType;set{_valueType=value;NotifyPropertyListChanged();}}
    [Export] public double Number {get;set;}
    [Export] public bool Boolean {get;set;}
    [Export] public string Symbol {get;set;}="";
    [Export] public Godot.Collections.Array<NpcExpressionDefinition> Inputs {get;set;}=new();
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        string name=property["name"].AsString();
        bool literal=Kind==NpcExpressionKind.Literal;
        bool visible=name switch
        {
            nameof(Key)=>Kind is NpcExpressionKind.Fact or NpcExpressionKind.Parameter,
            nameof(Inputs)=>Kind is not (NpcExpressionKind.Literal or NpcExpressionKind.Fact or NpcExpressionKind.Parameter),
            nameof(ValueType)=>literal,
            nameof(Number)=>literal&&ValueType==NpcValueType.Number,
            nameof(Boolean)=>literal&&ValueType==NpcValueType.Boolean,
            nameof(Symbol)=>literal&&ValueType==NpcValueType.Symbol,
            _=>true
        };
        if(!visible)property["usage"]=(long)PropertyUsageFlags.Storage;
    }
    public NpcExpression Freeze(int depth=0)
    {
        if(depth>32||!double.IsFinite(Number)||Inputs.Any(x=>x==null))throw new ArgumentException("Invalid expression or recursive expression resource.");
        var literal=ValueType switch {NpcValueType.Boolean=>NpcValue.Bool(Boolean),NpcValueType.Number=>NpcValue.Numeric(Number),NpcValueType.Symbol=>NpcValue.Text(Symbol),_=>default};
        if(Kind is NpcExpressionKind.Fact or NpcExpressionKind.Parameter && string.IsNullOrWhiteSpace(Key))throw new ArgumentException("Expression reference needs a key.");
        int count=Inputs.Count;
        if(Kind is NpcExpressionKind.Not or NpcExpressionKind.Known or NpcExpressionKind.Clamp01 && count!=1)throw new ArgumentException("Unary expression needs one input.");
        if(Kind is NpcExpressionKind.Equal or NpcExpressionKind.NotEqual or NpcExpressionKind.Greater or NpcExpressionKind.AtLeast or NpcExpressionKind.Less or NpcExpressionKind.AtMost or NpcExpressionKind.Subtract or NpcExpressionKind.Divide && count!=2)throw new ArgumentException("Binary expression needs two inputs.");
        return new(Kind,Key,literal,Inputs.Select(x=>x.Freeze(depth+1)));
    }
}
