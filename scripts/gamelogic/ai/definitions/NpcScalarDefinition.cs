using Godot;

namespace GameLogic.AI;

[Tool,GlobalClass]
public partial class NpcScalarDefinition : Resource
{
    [Export] public string Key {get;set;}="";
    [Export] public NpcValueType ValueType {get;set;}=NpcValueType.Number;
    [Export] public double Number {get;set;}
    [Export] public bool Boolean {get;set;}
    [Export] public string Symbol {get;set;}="";
    public NpcValue Value=>ValueType switch {NpcValueType.Number=>NpcValue.Numeric(Number),NpcValueType.Boolean=>NpcValue.Bool(Boolean),NpcValueType.Symbol=>NpcValue.Text(Symbol),_=>default};
}
