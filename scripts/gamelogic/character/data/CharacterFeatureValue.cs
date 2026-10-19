using Godot;
namespace GameLogic;

/// <summary>A library reference and optional per-character overrides.</summary>
[Tool,GlobalClass]
public partial class CharacterFeatureValue : Resource
{
    [Export] public string DefinitionId {get;set;}="";
    bool _useDefaultValue=true,_useDefaultRecovery=true;
    [Export] public bool UseDefaultValue {get=>_useDefaultValue;set{_useDefaultValue=value;NotifyPropertyListChanged();}}
    [Export] public double Number {get;set;}
    [Export] public bool UseDefaultRecovery {get=>_useDefaultRecovery;set{_useDefaultRecovery=value;NotifyPropertyListChanged();}}
    [Export] public double RecoveryPerHour {get;set;}=.1;
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        string name=property["name"].AsString();
        if(name==nameof(Number)&&UseDefaultValue||name==nameof(RecoveryPerHour)&&UseDefaultRecovery)
            property["usage"]=(long)PropertyUsageFlags.Storage;
    }
}
