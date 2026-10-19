using Godot;

namespace GameLogic;

[Tool,GlobalClass]
public partial class CharacterNeedDefinition : Resource
{
    [ExportGroup("基础数值")]
    [Export] public string NeedId {get;set;}="";
    [Export] public string DisplayName {get;set;}="";
    [Export] public double Minimum {get;set;}
    [Export] public double Maximum {get;set;}=1;
    [Export] public double Initial {get;set;}=.2;
    [Export] public double ChangePerHour {get;set;}=.04;
    [Export] public CharacterExpressionDefinition RateMultiplier {get;set;}

    [ExportGroup("紧迫度规则")]
    [Export] public bool HigherIsUrgent {get;set;}=true;
    bool _useDefaultThresholds=true;
    [Export] public bool UseDefaultThresholds
    {
        get=>_useDefaultThresholds;
        set{_useDefaultThresholds=value;NotifyPropertyListChanged();}
    }
    [Export] public double EnterThreshold {get;set;}=.65;
    [Export] public double ExitThreshold {get;set;}=.25;
    [Export(PropertyHint.Range,"0.01,10,0.01,or_greater")] public double UrgencyExponent {get;set;}=1;

    public double UrgencyEnter=>UseDefaultThresholds?Minimum+(Maximum-Minimum)*(HigherIsUrgent?.65:.35):EnterThreshold;
    public double UrgencyExit=>UseDefaultThresholds?Minimum+(Maximum-Minimum)*(HigherIsUrgent?.25:.75):ExitThreshold;
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if(UseDefaultThresholds&&property["name"].AsString() is nameof(EnterThreshold) or nameof(ExitThreshold))
            property["usage"]=(long)PropertyUsageFlags.Storage;
    }
}
