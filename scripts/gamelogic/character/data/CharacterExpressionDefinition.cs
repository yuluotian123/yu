using System;
using System.Linq;
using Godot;

namespace GameLogic;

[Tool,GlobalClass]
public partial class CharacterExpressionDefinition : Resource
{
    private CharacterExpressionKind _kind=CharacterExpressionKind.Literal;
    private CharacterValueType _valueType=CharacterValueType.Number;
    [Export] public CharacterExpressionKind Kind {get=>_kind;set{_kind=value;NotifyPropertyListChanged();}}
    [Export] public string Key {get;set;}="";
    [Export] public CharacterValueType ValueType {get=>_valueType;set{_valueType=value;NotifyPropertyListChanged();}}
    [Export] public double Number {get;set;}
    [Export] public bool Boolean {get;set;}
    [Export] public string Symbol {get;set;}="";
    [Export] public Godot.Collections.Array<CharacterExpressionDefinition> Inputs {get;set;}=new();
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        string name=property["name"].AsString();
        bool literal=Kind==CharacterExpressionKind.Literal;
        bool visible=name switch
        {
            nameof(Key)=>Kind is CharacterExpressionKind.Fact or CharacterExpressionKind.Parameter,
            nameof(Inputs)=>Kind is not (CharacterExpressionKind.Literal or CharacterExpressionKind.Fact or CharacterExpressionKind.Parameter),
            nameof(ValueType)=>literal,
            nameof(Number)=>literal&&ValueType==CharacterValueType.Number,
            nameof(Boolean)=>literal&&ValueType==CharacterValueType.Boolean,
            nameof(Symbol)=>literal&&ValueType==CharacterValueType.Symbol,
            _=>true
        };
        if(!visible)property["usage"]=(long)PropertyUsageFlags.Storage;
    }
    public CharacterExpression Freeze(int depth=0)
    {
        if(depth>32||!double.IsFinite(Number)||Inputs.Any(x=>x==null))throw new ArgumentException("Invalid expression or recursive expression resource.");
        var literal=ValueType switch {CharacterValueType.Boolean=>CharacterValue.Bool(Boolean),CharacterValueType.Number=>CharacterValue.Numeric(Number),CharacterValueType.Symbol=>CharacterValue.Text(Symbol),_=>default};
        if(Kind is CharacterExpressionKind.Fact or CharacterExpressionKind.Parameter && string.IsNullOrWhiteSpace(Key))throw new ArgumentException("Expression reference needs a key.");
        int count=Inputs.Count;
        if(Kind is CharacterExpressionKind.Not or CharacterExpressionKind.Known or CharacterExpressionKind.Clamp01 && count!=1)throw new ArgumentException("Unary expression needs one input.");
        if(Kind is CharacterExpressionKind.Equal or CharacterExpressionKind.NotEqual or CharacterExpressionKind.Greater or CharacterExpressionKind.AtLeast or CharacterExpressionKind.Less or CharacterExpressionKind.AtMost or CharacterExpressionKind.Subtract or CharacterExpressionKind.Divide && count!=2)throw new ArgumentException("Binary expression needs two inputs.");
        return new(Kind,Key,literal,Inputs.Select(x=>x.Freeze(depth+1)));
    }
}
