using Godot;

namespace GameLogic;

[Tool,GlobalClass]
public partial class CharacterNeedDefinition : Resource
{
    [Export] public string NeedId {get;set;}="";
    [Export] public string DisplayName {get;set;}="";
    [Export] public double Minimum {get;set;}
    [Export] public double Maximum {get;set;}=1;
    [Export] public double Initial {get;set;}=.2;
    [Export] public double ChangePerHour {get;set;}=.04;
    [Export] public CharacterExpressionDefinition RateMultiplier {get;set;}
}
