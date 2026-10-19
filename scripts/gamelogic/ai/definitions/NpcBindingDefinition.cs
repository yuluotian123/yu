using Godot;
namespace GameLogic.AI;

public enum NpcBindingSource { Expression, Selector, PlanVariable }

/// <summary>A named action input or plan variable. Object providers extend primitive value types.</summary>
[Tool, GlobalClass]
public partial class NpcBindingDefinition : Resource
{
    [Export] public string Name { get; set; } = "参数";
    NpcValueType _valueType=NpcValueType.Symbol;
    [Export] public NpcValueType ValueType { get=>_valueType;set{if(_valueType==value)return;_valueType=value;NotifyPropertyListChanged();} }
    [Export] public string Adapter { get; set; } = "";
    NpcBindingSource _source;
    [Export] public NpcBindingSource Source { get=>_source;set{if(_source==value)return;_source=value;NotifyPropertyListChanged();} }
    [Export] public string SelectorId { get; set; } = "";
    [Export] public string Variable { get; set; } = "";
    [Export] public NpcExpressionDefinition Value { get; set; }
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        string name=property["name"].AsString();
        if(name==nameof(SelectorId)&&Source!=NpcBindingSource.Selector || name==nameof(Variable)&&Source!=NpcBindingSource.PlanVariable ||
            name==nameof(Value)&&Source!=NpcBindingSource.Expression || name==nameof(Adapter)&&ValueType!=NpcValueType.Symbol)
            property["usage"]=(long)PropertyUsageFlags.Storage;
    }
}
