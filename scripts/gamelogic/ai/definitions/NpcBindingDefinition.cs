using Godot;
namespace GameLogic.AI;

public enum NpcBindingSource { Expression, Selector, PlanVariable }

/// <summary>A named action input or plan variable. Object providers extend primitive value types.</summary>
[Tool, GlobalClass]
public partial class NpcBindingDefinition : Resource
{
    [Export] public string Name { get; set; } = "参数";
    CharacterValueType _valueType=CharacterValueType.Symbol;
    [Export] public CharacterValueType ValueType { get=>_valueType;set{if(_valueType==value)return;_valueType=value;NotifyPropertyListChanged();} }
    string _adapter="";
    [Export] public string Adapter { get=>_adapter;set{if(_adapter==value)return;_adapter=value;NotifyPropertyListChanged();} }
    NpcBindingSource _source;
    [Export] public NpcBindingSource Source { get=>_source;set{if(_source==value)return;_source=value;NotifyPropertyListChanged();} }
    [Export] public string SelectorId { get; set; } = "";
    [Export] public string Variable { get; set; } = "";
    [Export] public CharacterExpressionDefinition Value { get; set; }
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        string name=property["name"].AsString();
        if(name==nameof(SelectorId)&&Source!=NpcBindingSource.Selector || name==nameof(Variable)&&Source!=NpcBindingSource.PlanVariable ||
            name==nameof(Value)&&Source!=NpcBindingSource.Expression || name==nameof(Adapter)&&ValueType!=CharacterValueType.Symbol)
            property["usage"]=(long)PropertyUsageFlags.Storage;
    }
}
