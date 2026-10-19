using Godot;

namespace GameLogic;

[Tool,GlobalClass]
public partial class CharacterScalarDefinition : Resource
{
    [Export] public string Key {get;set;}="";
    CharacterValueType _valueType=CharacterValueType.Number;
    [Export] public CharacterValueType ValueType {get=>_valueType;set{_valueType=value;NotifyPropertyListChanged();}}
    [Export] public double Number {get;set;}
    [Export] public bool Boolean {get;set;}
    [Export] public string Symbol {get;set;}="";
    public CharacterValue Value=>ValueType switch {CharacterValueType.Number=>CharacterValue.Numeric(Number),CharacterValueType.Boolean=>CharacterValue.Bool(Boolean),CharacterValueType.Symbol=>CharacterValue.Text(Symbol),_=>default};
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        string name=property["name"].AsString();
        if(name==nameof(Number)&&ValueType!=CharacterValueType.Number||name==nameof(Boolean)&&ValueType!=CharacterValueType.Boolean||name==nameof(Symbol)&&ValueType!=CharacterValueType.Symbol)
            property["usage"]=(long)PropertyUsageFlags.Storage;
    }
}
